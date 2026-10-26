using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>The target of a worm resolved to live objects for one simulation step (spec §5.3): a player, a creature / tamed animal, or a structure.</summary>
    internal struct TargetView
    {
        public TargetKind Kind;
        public Player Player;           // Kind Player
        public Character Character;     // Player, Creature, Tamed
        public Component Object;        // the Character, or the WearNTear / Destructible of a structure
        public Collider Collider;       // Structure: its collider closest to the worm
        public Vector3 Position;        // a character: its position (feet); a structure: the point of the collider closest to the worm's eye
        public Vector3 Center;          // a character: its centre point; a structure: same as Position
        public float Radius;            // a character: its radius; a structure: 0 (the distance is already measured to its surface)
        public bool Contact;            // a structure: Position is a point of its real surface (a concave mesh may show none from here); always true otherwise

        public bool IsStructure => Kind == TargetKind.Structure;
    }

    /// <summary>Resolution of the stored target of a worm: players by id, the other kinds by ZDOID (spec §11.3), with a per-slot cache of the objects.</summary>
    internal sealed partial class WormColony
    {
        private Component[] _tgtObject;
        private Character[] _tgtCharacter;
        private Collider[] _tgtCollider;
        private long[] _tgtKey;
        private TargetBlacklist _blacklist;     // per worm slot: targets set aside after a stall (decision 163)
        private ProgressWatch[] _watch;
        private const float ObstacleCastRadius = 0.3f, ObstacleCastHeight = 0.4f;   // the sweep toward a living target (decision 166)
        private float[] _livingSeen;            // per worm slot: last time a player or a creature was perceived (a structure is a fallback only after ForgetTime)
        private long[] _obstacleKey;            // per worm slot: key of the structure taken as an obstacle on the way to a living target (decision 164), 0 = none

        private void InitTargetCache(int capacity)
        {
            _blacklist = new TargetBlacklist(capacity);
            _watch = new ProgressWatch[capacity];
            _obstacleKey = new long[capacity];
            _livingSeen = new float[capacity];
            for (int k = 0; k < capacity; k++) _livingSeen[k] = -1000f;
            _tgtObject = new Component[capacity];
            _tgtCharacter = new Character[capacity];
            _tgtCollider = new Collider[capacity];
            _tgtKey = new long[capacity];
        }

        /// <summary>
        /// The live target of worm <paramref name="i"/>. False when it has none or it is gone, dead, or no longer targetable; the caller then loses the target.
        /// A creature or a structure is found through <c>ZNetScene.FindInstance</c> once, then kept in the slot cache while it lives.
        /// </summary>
        private bool ResolveTarget(int i, out TargetView view)
        {
            view = default;
            WormStore s = Store;
            var kind = (TargetKind)s.targetKind[i];
            view.Kind = kind;
            if (kind == TargetKind.None || s.targetId[i] == 0) return false;

            if (kind == TargetKind.Player)
            {
                Player p = FindPlayer(s.targetId[i]);
                if (!Targetable(p)) return false;
                view.Player = p;
                view.Character = p;
                view.Object = p;
                view.Position = p.transform.position;
                view.Center = p.GetCenterPoint();
                view.Radius = p.GetRadius();
                view.Contact = true;
                return true;
            }

            Vector3 eye = s.pos[i] + Vector3.up * EyeHeight;
            if (_tgtObject[i] == null || _tgtKey[i] != s.targetId[i])
            {
                _tgtKey[i] = s.targetId[i];
                var id = new ZDOID(s.targetZdoUser[i], s.targetZdoId[i]);
                if (!TargetScanner.Resolve(kind, id, eye, out Component obj, out Character character, out Collider collider))
                {
                    _tgtObject[i] = null;
                    return false;
                }
                _tgtObject[i] = obj;
                _tgtCharacter[i] = character;
                _tgtCollider[i] = collider;
            }

            Component target = _tgtObject[i];
            if (target == null) return false;
            view.Object = target;
            if (kind == TargetKind.Structure)
            {
                Collider c = _tgtCollider[i];
                if (c == null) { _tgtObject[i] = null; return false; }
                view.Collider = c;
                view.Contact = TargetScanner.TryClosestOn(c, eye, out view.Position);
                view.Center = view.Position;
                return true;
            }

            Character ch = _tgtCharacter[i];
            if (ch == null || ch.IsDead()) return false;
            view.Character = ch;
            view.Position = ch.transform.position;
            view.Center = ch.GetCenterPoint();
            view.Radius = ch.GetRadius();
            view.Contact = true;
            return true;
        }

        /// <summary>
        /// Distance from the eye of worm <paramref name="i"/> to the target's surface: a character's capsule centre minus its radius, a structure's closest point (spec §3.3).
        /// A structure of which no real surface is seen from here (a concave mesh) is out of reach: infinite.
        /// </summary>
        private float SurfaceDistance(int i, in TargetView target) =>
            target.Contact ? Vector3.Distance(Store.pos[i] + Vector3.up * EyeHeight, target.Center) - target.Radius : float.MaxValue;

        /// <summary>
        /// Progress watch (decisions 163, 164): the distance to the target must shrink by 0.5 m in 4 s while the worm hunts it (not striking, not in strike range).
        /// When it does not, a structure within cobra range in the half space toward a living target is taken as an obstacle (the worm gnaws the door the player
        /// hides behind) and the hunt goes on; else a creature or a structure is set aside for 10 s and the worm gives it up (true). A player is never set aside.
        /// </summary>
        private bool WatchProgress(int i, in TargetView target, float now)
        {
            WormStore s = Store;
            if (target.Kind == TargetKind.None) return false;
            float distance = SurfaceDistance(i, target);
            bool exempt = distance <= MWConfig.Worm.CobraRange.Value;
            if (!_watch[i].Update(s.targetId[i], distance, now, exempt)) return false;

            if (!target.IsStructure && TakeObstacle(i, target, now)) return false;
            if (target.Kind == TargetKind.Player) return false;
            _blacklist.Add(i, s.targetId[i], now);
            _obstacleKey[i] = 0L;
#if MW_DEV
            Plugin.DevInfo($"[Target] worm {i} gives up {target.Kind} {(target.Object != null ? target.Object.name : "?")} (no progress, distance {distance:0.00} m)");
#endif
            Plugin.DevDebug($"[Target] worm {i} gives up {target.Kind} {(target.Object != null ? target.Object.name : "?")}: no progress for {ProgressWatch.Window:0} s, set aside {TargetBlacklist.DefaultSeconds:0} s");
            return true;
        }

        /// <summary>The structure that blocks the way to a living target becomes the target of worm <paramref name="i"/> (kept until it is gone or out of reach).</summary>
        private bool TakeObstacle(int i, in TargetView target, float now)
        {
            WormStore s = Store;
            Vector3 pos = s.pos[i];
            Vector3 center = pos + Vector3.up * ObstacleCastHeight;
            if (!_scanner.PickObstacle(center, ObstacleCastRadius, pos.y, target.Center - center, MWConfig.Worm.CobraRange.Value, now,
                    TargetReach.WormVerticalReach(EyeHeight, MWConfig.Worm.CobraRange.Value), _blacklist, i, out TargetChoice pick) || pick.Id == ZDOID.None)
                return false;
            s.SetObjectTarget(i, pick.Kind, pick.Id.UserID, pick.Id.ID);
            s.lastSeen[i] = now;
            s.hasLos[i] = true;
            _obstacleKey[i] = s.targetId[i];
            _tgtObject[i] = null;
#if MW_DEV
            Plugin.DevInfo($"[Target] worm {i} takes {(pick.Object != null ? pick.Object.name : "?")} as an obstacle on the way to its {target.Kind} target");
#endif
            Plugin.DevDebug($"[Target] worm {i} takes {(pick.Object != null ? pick.Object.name : "?")} as an obstacle on the way to its {target.Kind} target");
            return true;
        }

        /// <summary>The obstacle structure of worm <paramref name="i"/> is still the target, standing and within a metre of cobra range: it is kept over any other choice.</summary>
        private bool KeepObstacle(int i, float now)
        {
            WormStore s = Store;
            if (_obstacleKey[i] == 0L) return false;
            if (s.targetKind[i] == (byte)TargetKind.Structure && s.targetId[i] == _obstacleKey[i]
                && ResolveTarget(i, out TargetView view) && SurfaceDistance(i, view) <= MWConfig.Worm.CobraRange.Value + 1f)
            {
                s.lastSeen[i] = now;
                return true;
            }
            _obstacleKey[i] = 0L;
            return false;
        }
    }
}
