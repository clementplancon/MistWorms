using System;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Drops
{
    /// <summary>Who receives the loot of something a mass or a free worm killed: the pool of a mass (<c>MW_Consumed</c>) or of a worm (<c>MW_WormConsumed</c>, its colony's reserve if it is gone).</summary>
    internal enum CaptureKind : byte
    {
        None = 0,
        Mass = 1,
        Worm = 2,
    }

    internal struct CaptureTarget
    {
        public CaptureKind Kind;
        public ZDOID Id;          // the mass, or the colony of the worm
        public int Slot;
        public int Generation;

        public override string ToString() => Kind == CaptureKind.Mass ? $"mass {Id}" : Kind == CaptureKind.Worm ? $"worm {Slot}/{Generation} of colony {Id}" : "nobody";
    }

    /// <summary>
    /// Capture of what a creature or a structure would have dropped when a mass or a free worm killed it (spec §7.4, §10.3, plan 6, A2, A3, A13): the loot goes
    /// into the pool of the killer instead of the ground. The decision is made by the Harmony patches of <c>DropPatches</c>, which only change anything when
    /// <see cref="Attribute"/> says the kill is ours; the rest is vanilla. Runs on the owner of the victim; the loot is sent to the owner of the pool by the
    /// routed RPCs of <see cref="PoolRpc"/>. Main thread only; the lists are reused.
    /// </summary>
    internal static class DropCapture
    {
        /// <summary>"Last worm hit" (spec §7.4): written on the peer that owns the victim, by <c>WormBite</c>.</summary>
        public static readonly LastWormHitRegistry Registry = new LastWormHitRegistry();

        private static readonly CaptureBook s_book = new CaptureBook();
        private static readonly ConsumedList s_list = new ConsumedList();
        private static readonly int s_massHash = MWNames.MassPrefab.GetStableHashCode();
        private static readonly Func<KeyValuePair<GameObject, int>, bool> s_isItem = IsItemEntry;

        // The structure being destroyed by one of ours (set around WearNTear.Destroy / Destructible.Destroy, valid for the frame it was set in).
        private static GameObject s_ctxObject;
        private static CaptureTarget s_ctxWho;
        private static int s_ctxFrame;

#if MW_DEV
        /// <summary>What the patches captured since the last <see cref="ResetCounters"/> (read by S12 and S13): the units by item prefab hash of the last capture, its victim and its killer.</summary>
        public static readonly Dictionary<int, int> LastUnits = new Dictionary<int, int>();
        public static string LastVictim, LastKiller;
        /// <summary>ZDOID of the creature of the last creature capture: a scenario that places its own victim checks it was that one and not a creature that was already there.</summary>
        public static ZDOID LastVictimId = ZDOID.None;
        public static int CreatureCaptures, StructureCaptures, DoubleRolls, Refused;

        public static void ResetCounters()
        {
            LastUnits.Clear();
            LastVictim = LastKiller = null;
            LastVictimId = ZDOID.None;
            CreatureCaptures = StructureCaptures = DoubleRolls = Refused = 0;
        }

        private static void Note(string victim, in CaptureTarget who, ConsumedList list, bool structure)
        {
            LastUnits.Clear();
            for (int i = 0; i < list.Count; i++) LastUnits[list.Hash[i]] = (int)Math.Round(list.Qty[i]);
            LastVictim = victim;
            LastKiller = who.ToString();
            if (structure) StructureCaptures++;
            else CreatureCaptures++;
        }
#endif

        // ------------------------------------------------------------------ attribution

        /// <summary>True when the ZDO <paramref name="id"/> is a writhing mass (read from the ZDO: the mass need not be loaded on this peer).</summary>
        public static bool IsMass(ZDOID id)
        {
            if (id == ZDOID.None || ZDOMan.instance == null) return false;
            ZDO zdo = ZDOMan.instance.GetZDO(id);
            return zdo != null && zdo.GetPrefab() == s_massHash;
        }

        /// <summary>
        /// Who killed <paramref name="victim"/> with a hit whose attacker is <paramref name="attacker"/> (spec §7.4): a mass when the attacker is one; when the hit
        /// has no attacker and is a marked cobra hit (<see cref="CobraMark"/>: the last hit applied to the victim is the cobra, decisions 20 and 163), the free worm the
        /// "last worm hit" registry names; otherwise nobody (vanilla). A burn, a poison, a fall or a drowning that kills after a cobra hit carries no mark.
        /// </summary>
        public static bool Attribute(ZDOID victim, ZDOID attacker, bool lethalHitIsCobra, float now, out CaptureTarget who)
        {
            who = default;
            if (attacker != ZDOID.None)
            {
                if (!IsMass(attacker)) return false;
                who.Kind = CaptureKind.Mass;
                who.Id = attacker;
                return true;
            }
            if (!lethalHitIsCobra) return false;
            if (!Registry.TryGet(victim, now, out ZDOID colony, out int slot, out int generation)) return false;
            who.Kind = CaptureKind.Worm;
            who.Id = colony;
            who.Slot = slot;
            who.Generation = generation;
            return true;
        }

        private static bool Send(in CaptureTarget who, ConsumedList list)
        {
            if (who.Kind == CaptureKind.Mass) return PoolRpc.SendConsumed(who.Id, list);
            if (who.Kind == CaptureKind.Worm) return PoolRpc.SendWormConsumed(who.Id, who.Slot, who.Generation, list);
            return false;
        }

        // ------------------------------------------------------------------ creatures (CharacterDrop.GenerateDropList postfix)

        /// <summary>
        /// The list rolled for <paramref name="drop"/>'s creature: when a mass or a free worm made the lethal hit, its content goes to the killer's pool and
        /// <paramref name="list"/> loses what was captured (the ragdoll then stores no item, no item falls). Only the entries that are items (prefabs with an
        /// <c>ItemDrop</c>) are captured: the other entries (the Blobs of a BlobElite, the BlobMorkMini of a BlobMork) are creatures that the pool cannot hold and
        /// stay in the vanilla list. Returns true when the list was handled.
        /// </summary>
        public static bool CaptureCreature(CharacterDrop drop, List<KeyValuePair<GameObject, int>> list)
        {
            if (drop == null || list == null) return false;
            // A prefab that was never instantiated (no Awake, no ZNetView): vanilla, and no Character.GetZDOID (it dereferences m_nview).
            ZNetView view = drop.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            Character c = drop.GetComponent<Character>();
            if (c == null || c.IsPlayer()) return false;
            ZDOID id = view.GetZDO().m_uid;
            if (id == ZDOID.None) return false;
            float now = Time.time;

            if (s_book.Seen(id, now))
            {
                // The same death rolls a second list (Ragdoll.Setup, then CharacterDrop.OnDeath): it was already credited once.
                CaptureMath.RemoveWhere(list, s_isItem);
#if MW_DEV
                DoubleRolls++;
#endif
                return true;
            }

            HitData last = c.m_lastHit;
            if (last == null || !Attribute(id, last.m_attacker, CobraMark.Is(last.m_skillRaiseAmount), now, out CaptureTarget who))
            {
#if MW_DEV
                Plugin.DevInfo($"[Capture] {Utils.GetPrefabName(c.gameObject)} {id} died, not attributed: last hit {(last == null ? "none" : $"attacker {last.m_attacker}, mark {CobraMark.Is(last.m_skillRaiseAmount)} (skill {last.m_skillRaiseAmount}), type {last.m_hitType}")}, " +
                                   $"registry {(Registry.TryGet(id, now, out ZDOID rc, out int rs, out _) ? $"worm {rs} of {rc}" : "no entry")} ({Registry.Count} live)");
#endif
                return false;
            }

            s_list.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                if (s_isItem(list[i])) CaptureMath.AddUnits(s_list, list[i].Key.name.GetStableHashCode(), list[i].Value);
            }
            if (s_list.Count == 0) return false;   // nothing the pool can hold: the list stays vanilla and the kill is not consumed
            if (!Send(who, s_list))
            {
#if MW_DEV
                Refused++;
#endif
                Plugin.Log.LogWarning($"[Capture] {c.name}: loot not captured, {who} cannot receive it: vanilla drop");
                return false;
            }

            s_book.Mark(id, now);   // the registry entry is left to expire: a saddle of the same death is attributed through it
            string victim = Utils.GetPrefabName(c.gameObject);
            Plugin.DevInfo($"[Capture] {victim} {id} killed by {who}: {Describe(s_list)} go to the pool instead of the ground");
#if MW_DEV
            Note(victim, who, s_list, false);
            LastVictimId = id;
#endif
            CaptureMath.RemoveWhere(list, s_isItem);
            return true;
        }

        private static bool IsItemEntry(KeyValuePair<GameObject, int> entry) => entry.Key != null && entry.Key.GetComponent<ItemDrop>() != null;

        /// <summary>
        /// <c>Tameable.OnDeath</c> of a tamed animal that carries a saddle and was killed by one of ours (same attribution as <c>GenerateDropList</c>): the saddle
        /// goes into the pool of the killer instead of falling to the ground. Returns true when the vanilla method must not run (it only spawns the saddle).
        /// </summary>
        public static bool CaptureSaddle(Tameable tameable)
        {
            if (tameable == null || !tameable.m_dropSaddleOnDeath || tameable.m_saddleItem == null) return false;
            Character c = tameable.GetComponent<Character>();
            ZNetView view = tameable.GetComponent<ZNetView>();
            if (c == null || view == null || !view.IsValid() || !view.GetZDO().GetBool(ZDOVars.s_haveSaddleHash)) return false;
            ZDOID id = view.GetZDO().m_uid;
            HitData last = c.m_lastHit;
            if (last == null || !Attribute(id, last.m_attacker, CobraMark.Is(last.m_skillRaiseAmount), Time.time, out CaptureTarget who)) return false;

            s_list.Clear();
            CaptureMath.AddUnits(s_list, tameable.m_saddleItem.gameObject.name.GetStableHashCode(), 1);
            if (!Send(who, s_list))
            {
#if MW_DEV
                Refused++;
#endif
                Plugin.Log.LogWarning($"[Capture] saddle of {c.name}: not captured, {who} cannot receive it: vanilla drop");
                return false;
            }
            string victim = Utils.GetPrefabName(c.gameObject);
            Plugin.DevInfo($"[Capture] saddle of {victim} {id} killed by {who}: {Describe(s_list)} go to the pool instead of the ground");
#if MW_DEV
            Note(victim, who, s_list, false);
#endif
            return true;
        }

        // ------------------------------------------------------------------ structures (WearNTear.Destroy / Destructible.Destroy context)

        /// <summary>
        /// Opens the context of the destruction of <paramref name="go"/> by <paramref name="hit"/> (prefix of <c>WearNTear.Destroy</c> and <c>Destructible.Destroy</c>).
        /// A destruction with no hit (a collapse, a deconstruction) is never ours (spec §7.4).
        /// </summary>
        public static void BeginStructure(GameObject go, HitData hit)
        {
            s_ctxObject = null;
            if (go == null || hit == null) return;
            ZNetView view = go.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            ZDOID id = view.GetZDO().m_uid;
            if (!Attribute(id, hit.m_attacker, CobraMark.Is(hit.m_skillRaiseAmount), Time.time, out CaptureTarget who)) return;
            Registry.Remove(id);
            s_ctxObject = go;
            s_ctxWho = who;
            s_ctxFrame = Time.frameCount;
        }

        public static void EndStructure() => s_ctxObject = null;

        private static bool TryContext(GameObject go, out CaptureTarget who)
        {
            who = s_ctxWho;
            return s_ctxObject != null && s_ctxObject == go && s_ctxFrame == Time.frameCount;
        }

        /// <summary>
        /// <c>Piece.DropResources</c> of a piece destroyed by one of ours: what the vanilla method would have dropped (Piece.cs:339-400: free build key, recoverable
        /// requirements, Feast scaling, drop conversion, the "not placed by a player" third) goes to the killer's pool. Returns true when the vanilla method must
        /// not run. The destroyed loot container branch is not reproduced: the units go to the pool, whole.
        /// </summary>
        public static bool CapturePiece(Piece piece, HitData hitData)
        {
            if (piece == null || !TryContext(piece.gameObject, out CaptureTarget who)) return false;
            if (ZoneSystem.instance == null || ObjectDB.instance == null || Game.instance == null) return false;
            if (ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())) return false;   // vanilla gives nothing

            s_list.Clear();
            Feast feast = piece.GetComponent<Feast>();
            bool placed = piece.IsPlacedByPlayer();
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null || !requirement.m_recover) continue;
                GameObject prefab = ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(requirement.m_resItem.name));
                if (prefab == null) continue;
                int count = requirement.m_amount;
                if (feast != null) count = (int)Math.Floor(count * feast.GetStackPercentige());
                if (count <= 0) continue;
                prefab = Game.instance.CheckDropConversion(hitData, requirement.m_resItem, prefab, ref count);
                count = CaptureMath.PieceUnits(count, placed);
                CaptureMath.AddUnits(s_list, prefab.name.GetStableHashCode(), count);
            }
            return Finish(piece.gameObject, who, "piece" + (placed ? " (placed by a player)" : " (not placed by a player: a third)"));
        }

        /// <summary>
        /// <c>DropOnDestroyed.OnDestroyed</c> of a dvergr Destructible (or piece) destroyed by one of ours: the vanilla drop list is rolled and goes to the killer's pool.
        /// Inert on every other object, nests included (spec §5.3). Returns true when the vanilla method must not run.
        /// </summary>
        public static bool CaptureDropOnDestroyed(DropOnDestroyed component)
        {
            if (component == null || !TryContext(component.gameObject, out CaptureTarget who)) return false;
            if (!TargetRules.IsDvergrStructure(Utils.GetPrefabName(component.gameObject))) return false;

            s_list.Clear();
            List<GameObject> drops = component.m_dropWhenDestroyed.GetDropList();
            for (int i = 0; i < drops.Count; i++)
                if (drops[i] != null) CaptureMath.AddUnits(s_list, drops[i].name.GetStableHashCode(), 1);
            return Finish(component.gameObject, who, "dvergr destructible");
        }

        private static bool Finish(GameObject go, in CaptureTarget who, string what)
        {
            if (!Send(who, s_list))
            {
#if MW_DEV
                Refused++;
#endif
                Plugin.Log.LogWarning($"[Capture] {go.name}: loot not captured, {who} cannot receive it: vanilla drop");
                return false;
            }
            string victim = Utils.GetPrefabName(go);
            Plugin.DevInfo($"[Capture] {what} {victim} destroyed by {who}: {Describe(s_list)} go to the pool instead of the ground");
#if MW_DEV
            Note(victim, who, s_list, true);
#endif
            return true;
        }

        private static string Describe(ConsumedList list)
        {
            if (list.Count == 0) return "nothing";
            var parts = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(list.Hash[i]) : null;
                parts.Add($"{(prefab != null ? prefab.name : list.Hash[i].ToString())} x{(int)Math.Round(list.Qty[i])}");
            }
            return string.Join(", ", parts);
        }
    }
}
