using System;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Merging of two masses that touch (spec §4.1, decisions.md 77, 79, 92; phase 4B-4). Each owner looks, every 0.25 s, at the masses its own
    /// mass touches. The smaller one (at equal count the lower ZDOID) is the one that moves, and only its owner acts:
    /// - the two fit in Mass.MaxCount: the small mass goes Merging, slides onto the big one in 0.5 s (its heap is resorbed meanwhile), then the
    ///   big one receives all its worms (<c>MW_MassAdd</c> when it is owned elsewhere) and the small one is destroyed;
    /// - otherwise the small mass hands over exactly <c>MaxCount - n1</c> worms at once and keeps the rest, without dissolving even if the rest is
    ///   under Mass.DissolveThreshold (only a loss dissolves a mass, decision 92). A full mass absorbs nothing.
    /// </summary>
    internal sealed partial class WritheMassState
    {
        /// <summary>Dev switch: masses that touch do not merge while false.</summary>
        public static bool MergeEnabled = true;

        /// <summary>Complete merges since the game was loaded (dev checks).</summary>
        public static int MergesCompleted { get; private set; }

        /// <summary>Worms handed over by partial merges since the game was loaded (dev checks).</summary>
        public static int WormsTransferred { get; private set; }

        private const float MergePeriod = 0.25f;
        private const int TransferGlideMax = 8;
        private static readonly Vector3[] s_transferPoints = new Vector3[TransferGlideMax];

        private int _slidersIn;       // masses of this peer that slide onto this one right now: it does not slide itself meanwhile
        private bool _merging;
        private WritheMassState _mergeInto;
        private Vector3 _mergeFrom;
        private float _mergeT0;
        private float _nextMerge;
        private float _transferCooldownUntil;
        private WritheMassState _seekTarget;
        private float _seekUntil, _seekCooldownUntil;
#if MW_DEV
        private float _dbgNext;
#endif

        /// <summary>A mass sees another one of its colony this close (metres) and goes to join it when it is the smaller (spec 4.1, coordinator 4B-4).</summary>
        public const float MergeSeekRadius = 8f;
        public const float MergeSeekMaxSeconds = 6f;
        public const float MergeSeekCooldown = 4f;

        /// <summary>True while this mass slides onto a bigger one.</summary>
        public bool Merging => _merging;

        // ------------------------------------------------------------------ detection (owner)

        private void MergeTick(float now)
        {
            if (!MergeEnabled || !IsOwner || _dissolving || _merging || _slidersIn > 0 || now < _nextMerge) return;
            _nextMerge = now + MergePeriod;
            if (_character == null || _character.IsDead()) return;
            byte myCode = StateCode;
            if (MassStateCodes.IsDissolving(myCode) || MassStateCodes.IsMerging(myCode)) return;
            int n = N;
            if (n <= 0) return;

            int max = MWConfig.Mass.MaxCount.Value;
            Vector3 p = transform.position;
            float r = MassMath.Radius(n);
            ZDOID mine = ZdoId;
            long colonyKey = ColonyKey;
            if (_seekTarget != null && (!_seekTarget.Valid || _seekTarget._dissolving || _seekTarget._merging || now >= _seekUntil))
            {
                // Gave up (or arrived): the hunt resumes, and the next search waits a little.
                if (now >= _seekUntil) _seekCooldownUntil = now + MergeSeekCooldown;
                _seekTarget = null;
            }
            WritheMassState seekBest = null;
            float seekBestD = MergeSeekRadius;
            bool mayEngulf = MassStateCodes.Mode(myCode) == MassStateCodes.Engulf;

            for (int k = 0; k < s_all.Count; k++)
            {
                WritheMassState b = s_all[k];
                if (b == null || b == this || !b.Valid || b._dissolving || b._merging) continue;
                byte code = b.StateCode;
                if (MassStateCodes.IsDissolving(code) || MassStateCodes.IsMerging(code)) continue;
                int nb = b.N;
                if (nb <= 0 || nb >= max) continue;         // a full mass absorbs nothing

                Vector3 q = b.transform.position;
                float dx = q.x - p.x, dz = q.z - p.z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                ZDOID theirs = b.ZdoId;
                bool iAmSmaller = MassMergeMath.IsSmaller(n, mine.UserID, mine.ID, nb, theirs.UserID, theirs.ID);
#if MW_DEV
                if (now >= _dbgNext) { _dbgNext = now + 1f; Plugin.DevInfo($"[MergeDbg] {mine} n={n} sees {theirs} n={nb} d={d:0.00} smaller={iAmSmaller} key={colonyKey} theirKey={b.ColonyKey} mode={MassStateCodes.Describe(myCode)} seekT={(_seekTarget != null)} cooldown={Math.Max(0f, _seekCooldownUntil - now):0.0}"); }
#endif
                if (!MassMergeMath.Touching(d, q.y - p.y, r, MassMath.Radius(nb)))
                {
                    // Not touching: a smaller mass that sees a bigger one of its colony close by goes to it (spec 4.1; the hunts alone would never
                    // bring two masses that stop at the same target face to face).
                    if (iAmSmaller && !mayEngulf && colonyKey != 0L && b.ColonyKey == colonyKey && d < seekBestD && Math.Abs(q.y - p.y) <= MassMergeMath.MaxContactDy
                        && MassMergeMath.Transfer(n, nb, max, out _) > 0)
                    {
                        seekBest = b;
                        seekBestD = d;
                    }
                    continue;
                }
                if (!iAmSmaller) continue;   // the other one slides, its owner acts

                MassMergeMath.MergePlan plan = MassMergeMath.Decide(n, mine.UserID, mine.ID, nb, theirs.UserID, theirs.ID, max, out int moved);
                if (plan == MassMergeMath.MergePlan.None) continue;
                _seekTarget = null;
                if (plan == MassMergeMath.MergePlan.Complete) BeginSlide(b, now);
                else if (now >= _transferCooldownUntil) TransferPartial(b, moved, now);
                return;
            }

            if (_seekTarget == null && seekBest != null && now >= _seekCooldownUntil)
            {
                _seekTarget = seekBest;
                _seekUntil = now + MergeSeekMaxSeconds;
            }
        }

        /// <summary>The position the AI heads for while this mass goes to join a bigger one of its colony (at most 6 s, then the hunt resumes).</summary>
        public bool TryGetSeekGoal(out Vector3 goal)
        {
            goal = default;
            WritheMassState t = _seekTarget;
            if (t == null || !t.Valid || _merging || Time.time >= _seekUntil) return false;
            goal = t.transform.position;
            return true;
        }

        // ------------------------------------------------------------------ complete merge

        private void BeginSlide(WritheMassState big, float now)
        {
            _merging = true;
            _mergeInto = big;
            big._slidersIn++;
            _mergeFrom = transform.position;
            _mergeT0 = now;
            MergeFrozen = true;
            WriteState(MassStateCodes.With(StateCode, MassStateCodes.Merging, true));
            SetBodiesIgnoring(big, true);
            Plugin.DevInfo($"[Mass] merge: {ZdoId} (n={N}) slides onto {big.ZdoId} (n={big.N}), the smaller of the two goes to the bigger");
        }

        private void SetBodiesIgnoring(WritheMassState other, bool ignore)
        {
            if (other != null && _capsule != null && other._capsule != null) Physics.IgnoreCollision(_capsule, other._capsule, ignore);
        }

        private void StepMerge(float now)
        {
            WritheMassState big = _mergeInto;
            if (big == null || !big.Valid || big._dissolving || _dissolving || _character == null || _character.IsDead())
            {
                CancelMerge();
                return;
            }
            float t = Mathf.Clamp01((now - _mergeT0) / MassMergeMath.MergeSeconds);
            float e = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(_mergeFrom, big.transform.position, e);
            if (_body != null)
            {
                _body.position = pos;
                _body.linearVelocity = Vector3.zero;
            }
            transform.position = pos;
            if (t >= 1f) FinishMerge(big);
        }

        private void FinishMerge(WritheMassState big)
        {
            int k = N;
            int bigBefore = big.N;
            NestCounts own = OwnerNests();                 // the nests of origin go with the worms, like the pool
            int accepted = big.AddWorms(k, OwnerPool(), own);   // the pool (quantities and carries) goes with the worms; a cut group takes the fraction accepted/k
            Plugin.DevInfo($"[Mass] merge: {ZdoId} (n={k}) into {big.ZdoId}: the big one {bigBefore} -> {big.N}, accepted {accepted}");
            if (accepted < 0 || accepted >= k)
            {
                // Done (a remote big mass takes the RPC and applies its own cap).
                MergesCompleted++;
                DestroyReason = $"merged into {big.ZdoId} (n={k} given, accepted {accepted})";
                _merging = false;
                ClearNests();                    // handed over: no double count while the small mass is still alive this frame
                if (big._slidersIn > 0) big._slidersIn--;
                SetBodiesIgnoring(big, false);
                DestroyMass();
                return;
            }
            // The big mass grew meanwhile and cannot take everyone: this one keeps the rest, with no dissolution.
            if (accepted > 0)
            {
                own.CopyProrata(accepted, s_nestShare);      // exactly what the big mass took (same rule, same input)
                BeginTransferFlag();
                RemoveWorms(accepted, s_nestShare);
                WormsTransferred += accepted;
            }
            SavePool();   // what the big mass did not take stays here, with the worms that stay
            CancelMerge();
            _transferCooldownUntil = Time.time + MassMergeMath.TransferCooldown;
        }

        private void CancelMerge()
        {
            if (!_merging) return;
            _merging = false;
            if (_mergeInto != null && _mergeInto._slidersIn > 0) _mergeInto._slidersIn--;
            SetBodiesIgnoring(_mergeInto, false);
            _mergeInto = null;
            MergeFrozen = false;
            WriteState(MassStateCodes.With(StateCode, MassStateCodes.Merging, false));
        }

        // ------------------------------------------------------------------ partial merge

        private void TransferPartial(WritheMassState big, int k, float now)
        {
            // The pool of the worms that leave is cut off first (prorata k/n, decisions.md 79) and handed over with them; what is not taken comes back.
            MassPool own = OwnerPool();
            s_poolTransfer.Clear();
            own.MoveFraction(s_poolTransfer, k, N);
            OwnerNests().CopyProrata(k, s_nestOffer);       // the nests of the k worms that leave
            int accepted = big.AddWorms(k, s_poolTransfer, s_nestOffer);
            if (accepted != -1 && !s_poolTransfer.IsEmpty) own.Merge(s_poolTransfer);
            s_poolTransfer.Clear();
            SavePool();
            _transferCooldownUntil = now + MassMergeMath.TransferCooldown;
            if (accepted == 0) return;
            int moved = accepted > 0 ? accepted : k;     // a remote big mass applies its own cap on the RPC
            Plugin.DevInfo($"[Mass] {ZdoId} ({N}) hands {moved} worms to {big.ZdoId} ({big.N}) and keeps the rest");

            // The worms that leave slide onto the big heap (a few of them: the ring of glides is shared).
            int glides = Math.Min(moved, TransferGlideMax);
            WritheMassVisual from = GetComponent<WritheMassVisual>();
            WritheMassVisual to = big.GetComponent<WritheMassVisual>();
            if (from != null && to != null)
            {
                from.GetReleasePoints(s_transferPoints, glides);
                to.SeedArrivals(s_transferPoints, glides);
            }

            s_nestOffer.CopyProrata(moved, s_nestShare);    // what the big mass took of the offer (or applies on its own cap, remote)
            BeginTransferFlag();
            int removed = RemoveWorms(moved, s_nestShare);
            WormsTransferred += removed;
        }

        private void BeginTransferFlag()
        {
            WriteState(MassStateCodes.With(StateCode, MassStateCodes.Transferring, true));
            CancelInvoke(nameof(EndTransferFlag));
            Invoke(nameof(EndTransferFlag), MassMergeMath.TransferFlagSeconds);
        }

        private void EndTransferFlag()
        {
            if (!Valid || !_nview.IsOwner()) return;
            WriteState(MassStateCodes.With(StateCode, MassStateCodes.Transferring, false));
        }

        /// <summary>
        /// Takes <paramref name="k"/> worms out of this mass without any loss effect (a partial merge hands them to another mass). Owner only; at
        /// least one worm stays. Never dissolves. Returns the number removed.
        /// </summary>
        public int RemoveWorms(int k, NestCounts composition = null)
        {
            if (k <= 0 || !IsOwner || _character == null) return 0;
            int n = N;
            int removed = Math.Min(k, n - 1);
            if (removed <= 0) return 0;
            NestCounts nests = OwnerNests();              // fitted to the old n, before the count changes
            if (composition != null && composition.Total == removed) nests.Subtract(composition);
            else nests.TakeProrata(removed, null);
            SaveNests();
            float partial = Partial;
            ImposeHealth(n - removed, partial);
            WriteCount(n - removed, partial);
            return removed;
        }
    }
}
