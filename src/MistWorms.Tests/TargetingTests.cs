using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Pass 6-2: the pure rules of the target choice (spec §5.3, §5.4, plan 6, A8, A11, A12) and the storage of a non player target.</summary>
    public class TargetingTests
    {
        // ------------------------------------------------------------------ score = distance x weight

        [Fact]
        public void ScoreIsDistanceTimesWeight()
        {
            Assert.Equal(9f, TargetRules.Score(15f, 0.6f), 3);     // a player at 15 m
            Assert.Equal(8f, TargetRules.Score(10f, 0.8f), 3);     // a tamed animal at 10 m
            Assert.Equal(3f, TargetRules.Score(3f, 1f), 3);        // a creature at 3 m
            Assert.Equal(7.5f, TargetRules.Score(5f, 1.5f), 3);    // a structure at 5 m
        }

        [Fact]
        public void ACreatureGluedToTheMassIsPreferredToAPlayerAtFifteenMetres()
        {
            // The example of spec 5.3: a Seeker against the mass is eaten before a player at 15 m.
            float player = TargetRules.Score(15f, 0.6f);
            float seeker = TargetRules.Score(1.2f, 1f);
            Assert.True(seeker < player);
        }

        [Fact]
        public void WeightsFollowTheKind()
        {
            Assert.Equal(0.6f, TargetRules.Weight(TargetKind.Player, 0.6f, 0.8f, 1f, 1.5f));
            Assert.Equal(0.8f, TargetRules.Weight(TargetKind.Tamed, 0.6f, 0.8f, 1f, 1.5f));
            Assert.Equal(1f, TargetRules.Weight(TargetKind.Creature, 0.6f, 0.8f, 1f, 1.5f));
            Assert.Equal(1.5f, TargetRules.Weight(TargetKind.Structure, 0.6f, 0.8f, 1f, 1.5f));
            Assert.Equal(float.MaxValue, TargetRules.Weight(TargetKind.None, 0.6f, 0.8f, 1f, 1.5f));
        }

        [Fact]
        public void AttackSwitchesGateTheirKind()
        {
            Assert.True(TargetRules.Allowed(TargetKind.Player, true, false, false, false));
            Assert.False(TargetRules.Allowed(TargetKind.Tamed, true, false, true, true));
            Assert.False(TargetRules.Allowed(TargetKind.Creature, true, true, false, true));
            Assert.False(TargetRules.Allowed(TargetKind.Structure, true, true, true, false));
            Assert.False(TargetRules.Allowed(TargetKind.None, true, true, true, true));
            Assert.Equal(TargetKind.Tamed, TargetRules.KindOfCharacter(true));
            Assert.Equal(TargetKind.Creature, TargetRules.KindOfCharacter(false));
        }

        // ------------------------------------------------------------------ best candidate by score, hidden ones skipped

        [Fact]
        public void NextBestWalksTheCandidatesInOrderOfScore()
        {
            float[] scores = { 5f, 2f, 9f, 2.5f };
            var used = new bool[4];
            Assert.Equal(1, TargetRules.NextBest(scores, used, 4, float.MaxValue));
            used[1] = true;                                   // hidden behind a wall
            Assert.Equal(3, TargetRules.NextBest(scores, used, 4, float.MaxValue));
            used[3] = true;
            Assert.Equal(0, TargetRules.NextBest(scores, used, 4, float.MaxValue));
            used[0] = true;
            Assert.Equal(2, TargetRules.NextBest(scores, used, 4, float.MaxValue));
            used[2] = true;
            Assert.Equal(-1, TargetRules.NextBest(scores, used, 4, float.MaxValue));
        }

        [Fact]
        public void NextBestStopsAtTheLimitOfTheBestPlayer()
        {
            float[] scores = { 5f, 2f };
            var used = new bool[2];
            Assert.Equal(1, TargetRules.NextBest(scores, used, 2, 3f));
            used[1] = true;
            Assert.Equal(-1, TargetRules.NextBest(scores, used, 2, 3f));   // 5 does not beat a player scored 3
            Assert.Equal(-1, TargetRules.NextBest(scores, used, 0, float.MaxValue));
        }

        [Fact]
        public void TheCurrentTargetIsKeptUpToTwiceTheRange()
        {
            Assert.True(TargetRules.InRange(20f, 20f, false));
            Assert.False(TargetRules.InRange(25f, 20f, false));
            Assert.True(TargetRules.InRange(25f, 20f, true));
            Assert.False(TargetRules.InRange(41f, 20f, true));
        }

        // ------------------------------------------------------------------ exclusions

        [Fact]
        public void ExcludedPrefabsAreParsedOnceAndMatchByName()
        {
            var set = new ExcludedSet();
            set.Refresh("SW_DivineWhale, Troll ,,Player");
            Assert.Equal(3, set.Count);
            Assert.True(set.ContainsName("SW_DivineWhale"));
            Assert.True(set.ContainsName("Troll"));
            Assert.True(set.ContainsName("Player"));
            Assert.False(set.ContainsName("Seeker"));
            Assert.False(set.ContainsName("sw_divinewhale"));      // names are case sensitive like prefab names
            Assert.True(set.Contains("Troll".GetStableHashCode()));
            Assert.False(set.Contains(0));

            set.Refresh(string.Empty);
            Assert.Equal(0, set.Count);
            Assert.False(set.ContainsName("Troll"));
            set.Refresh(null);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void TheDefaultExclusionCoversTheSkyWhaleOnly()
        {
            var set = new ExcludedSet();
            set.Refresh("SW_DivineWhale");
            Assert.True(set.ContainsName("SW_DivineWhale"));
            Assert.False(set.ContainsName("Seeker"));
            Assert.False(set.ContainsName("wood_door"));
        }

        [Theory]
        [InlineData("dvergrtown_wood_wall01", true)]
        [InlineData("dvergrtown_arch", true)]
        [InlineData("dvergrprops_barrel", true)]
        [InlineData("dvergrprops_pickaxe", true)]
        [InlineData("SeekerEgg", false)]               // a nest candidate: never
        [InlineData("CreepProp_egg_hanging01", false)]
        [InlineData("wood_door", false)]               // a WearNTear, accepted for another reason
        [InlineData("MW_WormNest", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void DvergrStructuresAreRecognisedByPrefix(string name, bool expected)
        {
            Assert.Equal(expected, TargetRules.IsDvergrStructure(name));
        }

        // ------------------------------------------------------------------ factions (spec 5.4)

        [Theory]
        [InlineData((int)FactionId.PlayerSpawned)]
        [InlineData((int)FactionId.TrainingDummy)]
        public void ExemptFactionsAreNeverTargets(int faction)
        {
            Assert.False(TargetRules.FactionTargetable((FactionId)faction));
        }

        [Theory]
        [InlineData((int)FactionId.Players)]
        [InlineData((int)FactionId.AnimalsVeg)]
        [InlineData((int)FactionId.ForestMonsters)]
        [InlineData((int)FactionId.MistlandsMonsters)]
        [InlineData((int)FactionId.Dverger)]
        [InlineData((int)FactionId.Boss)]
        [InlineData((int)FactionId.DeepNorth)]
        public void EveryOtherFactionIsATarget(int faction)
        {
            Assert.True(TargetRules.FactionTargetable((FactionId)faction));
        }

        [Fact]
        public void SpecMatrixFollowsTheVanillaMethod()
        {
            foreach (FactionId f in System.Enum.GetValues(typeof(FactionId)))
            {
                FactionMatrix.Spec(f, out bool a, out bool b);
                switch (f)
                {
                    case FactionId.PlayerSpawned:      // same faction
                        Assert.False(a);
                        Assert.False(b);
                        break;
                    case FactionId.TrainingDummy:      // BaseAI.cs:1232 and 1285-1286
                        Assert.True(a);
                        Assert.False(b);
                        break;
                    default:
                        Assert.True(a);
                        Assert.True(b);
                        break;
                }
            }
            FactionMatrix.Tamed(out bool ta, out bool tb);
            Assert.True(ta && tb);
        }

        // ------------------------------------------------------------------ storage of a non player target (spec 11.3, A11)

        [Fact]
        public void ObjectTargetKeepsTheZdoIdAndAKeyThatIsNeverZero()
        {
            var s = new WormStore(2);
            int i = s.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            s.SetObjectTarget(i, TargetKind.Structure, 4242L, 17u);
            Assert.Equal((byte)TargetKind.Structure, s.targetKind[i]);
            Assert.Equal(4242L, s.targetZdoUser[i]);
            Assert.Equal(17u, s.targetZdoId[i]);
            Assert.NotEqual(0L, s.targetId[i]);

            long key = s.targetId[i];
            s.SetObjectTarget(i, TargetKind.Creature, 4242L, 18u);
            Assert.NotEqual(key, s.targetId[i]);                       // another object, another key
            Assert.Equal(TargetRules.ObjectKey(4242L, 18u), s.targetId[i]);
            Assert.NotEqual(0L, TargetRules.ObjectKey(0L, 0u));
        }

        [Fact]
        public void PlayerTargetClearsTheObjectFieldsAndClearTargetResetsEverything()
        {
            var s = new WormStore(2);
            int i = s.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            s.SetObjectTarget(i, TargetKind.Tamed, 9L, 3u);
            s.SetPlayerTarget(i, 555L);
            Assert.Equal((byte)TargetKind.Player, s.targetKind[i]);
            Assert.Equal(555L, s.targetId[i]);
            Assert.Equal(0L, s.targetZdoUser[i]);
            Assert.Equal(0u, s.targetZdoId[i]);

            s.SetObjectTarget(i, TargetKind.Creature, 9L, 3u);
            s.ClearTarget(i);
            Assert.Equal((byte)TargetKind.None, s.targetKind[i]);
            Assert.Equal(0L, s.targetId[i]);
            Assert.Equal(0L, s.targetZdoUser[i]);
            Assert.Equal(0u, s.targetZdoId[i]);
        }

        [Fact]
        public void ARecycledSlotStartsWithoutATarget()
        {
            var s = new WormStore(1);
            int i = s.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            s.SetObjectTarget(i, TargetKind.Structure, 1L, 2u);
            s.Free(i);
            int j = s.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            Assert.Equal(i, j);
            Assert.Equal((byte)TargetKind.None, s.targetKind[j]);
            Assert.Equal(0L, s.targetId[j]);
            Assert.Equal(0u, s.targetZdoId[j]);
        }

        // ------------------------------------------------------------------ decision 164: a structure is a fallback and an obstacle

        /// <summary>The two phases of TargetScanner.Pick on plain arrays: the living candidates first (index 0 to living), the structures after them.</summary>
        private static int PickTiered(float[] scores, int living, float playerLimit)
        {
            var used = new bool[scores.Length];
            int best = TargetRules.NextBest(scores, used, 0, living, playerLimit);
            if (best >= 0) return best;
            if (!TargetRules.StructureFallbackAllowed(playerLimit, false)) return -1;
            return TargetRules.NextBest(scores, used, living, scores.Length, float.MaxValue);
        }

        [Fact]
        public void AWallAtTwoMetresDoesNotBeatAPlayerAtSixMetres()
        {
            // The mine case: score 3.0 for the wall (2 m x 1.5) against 3.6 for the player (6 m x 0.6): the player wins, the wall is not even considered.
            float wall = TargetRules.Score(2f, 1.5f);
            float player = TargetRules.Score(6f, 0.6f);
            Assert.True(wall < player);
            Assert.Equal(-1, PickTiered(new[] { wall }, 0, player));
            Assert.False(TargetRules.StructureFallbackAllowed(player, false));
        }

        [Fact]
        public void ALivingTargetBeatsANearerStructure()
        {
            float[] scores = { TargetRules.Score(9f, 1f), TargetRules.Score(1f, 1.5f) };   // a creature at 9 m, a wall at 1 m
            Assert.Equal(0, PickTiered(scores, 1, float.MaxValue));
        }

        [Fact]
        public void LivingTargetsAreComparedByDistanceTimesWeight()
        {
            float[] scores = { TargetRules.Score(5f, 1f), TargetRules.Score(5f, 0.8f), TargetRules.Score(1f, 1.5f) };
            Assert.Equal(1, PickTiered(scores, 2, float.MaxValue));
        }

        [Fact]
        public void AStructureIsChosenWhenNobodyLivingIsPerceived()
        {
            float[] scores = { TargetRules.Score(7f, 1.5f), TargetRules.Score(3f, 1.5f), TargetRules.Score(5f, 1.5f) };
            Assert.Equal(1, PickTiered(scores, 0, float.MaxValue));   // the weight only orders the structures among themselves
        }

        [Fact]
        public void AStructureIsNeverAFallbackWhileAPlayerIsPerceivedOrALivingTargetWasChosen()
        {
            Assert.True(TargetRules.StructureFallbackAllowed(float.MaxValue, false));
            Assert.False(TargetRules.StructureFallbackAllowed(12f, false));
            Assert.False(TargetRules.StructureFallbackAllowed(float.MaxValue, true));
        }

        [Fact]
        public void TheFloorUnderTheAttackerIsNeverAnObstacle()
        {
            Assert.False(TargetRules.BlocksTheWay(10.1f, 10f));    // a floor whose top is 0.1 m above the feet
            Assert.False(TargetRules.BlocksTheWay(9.5f, 10f));     // a foundation below
            Assert.True(TargetRules.BlocksTheWay(11.8f, 10f));     // a door 1.8 m tall
        }

        [Fact]
        public void TheFirstDoorOnTheWayIsTheObstacleAndWhatIsNotAStructureIsSkipped()
        {
            // Sweep hits: a floor under the attacker (0.2 m), a rock (not a structure, 1 m), a far door (3 m), a nearer door (2 m).
            float[] distance = { 0.2f, 1f, 3f, 2f };
            float[] top = { 10.1f, 11f, 12f, 12f };
            bool[] candidate = { true, false, true, true };
            Assert.Equal(3, TargetRules.FirstObstacle(distance, top, candidate, 4, 10f));
        }

        [Fact]
        public void ASideWallThatTheSweepDoesNotTouchGivesNoObstacle()
        {
            // The sweep toward the target touched nothing but the floor: no obstacle, the hunt goes on.
            Assert.Equal(-1, TargetRules.FirstObstacle(new[] { 0.2f }, new[] { 10.1f }, new[] { true }, 1, 10f));
            Assert.Equal(-1, TargetRules.FirstObstacle(new float[0], new float[0], new bool[0], 0, 10f));
        }

        [Fact]
        public void TheStructureFallbackWaitsForTheForgetTime()
        {
            Assert.False(TargetRules.FallbackDelayElapsed(100.5f, 100f, 5f));   // hidden for one tick
            Assert.False(TargetRules.FallbackDelayElapsed(105f, 100f, 5f));
            Assert.True(TargetRules.FallbackDelayElapsed(105.1f, 100f, 5f));
            Assert.True(TargetRules.FallbackDelayElapsed(0f, -1000f, 5f));      // never perceived: allowed at once
        }

        [Fact]
        public void ASetAsideDoorIsNotAnObstacleAgain()
        {
            var list = new TargetBlacklist(2);
            long door = TargetReach.Key(7L, 9u);
            list.Add(1, door, 10f);
            Assert.True(list.Contains(1, door, 12f));    // PickObstacle skips it for that owner
            Assert.False(list.Contains(0, door, 12f));   // another owner is not concerned
            Assert.False(list.Contains(1, door, 21f));   // after the 10 s
        }

        [Fact]
        public void AStalledHuntOfAPlayerFiresTheObstacleSearchButNeverSetsTheTargetAside()
        {
            // The watch fires for a player after the window (the callers then look for an obstacle); they never put a player on the blacklist.
            var w = new ProgressWatch();
            long key = 42L;
            Assert.False(w.Update(key, 6f, 0f, false));
            Assert.False(w.Update(key, 5.9f, 1.4f, false));
            Assert.False(w.Update(key, 5.8f, 2.8f, false));
            Assert.True(w.Update(key, 5.8f, ProgressWatch.Window + 0.1f, false));
            var list = new TargetBlacklist(1);
            Assert.False(list.Contains(0, key, 5f));   // nothing was added: the callers add only creatures and structures
        }

        [Fact]
        public void TheFearOfAFireSuspendsTheStallVerdict()
        {
            Assert.True(TargetRules.AfraidOfFire(10f, 14f));    // fled at 10 s, the grace lasts to 14 s
            Assert.False(TargetRules.AfraidOfFire(14f, 14f));
            Assert.False(TargetRules.AfraidOfFire(5f, 0f));     // never afraid
        }
    }
}
