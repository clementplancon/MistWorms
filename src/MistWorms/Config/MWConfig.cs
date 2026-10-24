using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace MistWorms.Config
{
    /// <summary>
    /// Every tunable of the mod (spec §14). Gameplay sections are server-synced and admin-locked through Jötunn;
    /// Performance and Dev stay local. Callers read <c>.Value</c> each time (no caching) so a synced change applies
    /// on the next tick; the only exception is array capacities such as <see cref="Colony.MaxAlivePerMine"/>.
    /// </summary>
    public static class MWConfig
    {
        private static ConfigFile _cfg;
        private static readonly List<ConfigEntryBase> Entries = new List<ConfigEntryBase>();
        private static int _order;

        public static void Bind(ConfigFile cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (_cfg != null) return;
            _cfg = cfg;

            // One write at the end instead of one per key; also keeps the raw file intact until we have checked it.
            bool saveOnSet = cfg.SaveOnConfigSet;
            cfg.SaveOnConfigSet = false;
            try
            {
                General.Bind();
                Worm.Bind();
                Resist.Bind();
                Mass.Bind();
                Engulf.Bind();
                Targets.Bind();
                Fire.Bind();
                Drops.Bind();
                Nest.Bind();
                Colony.Bind();
                Network.Bind();
                Performance.Bind();
                Dev.Bind();

                CheckRanges();
            }
            finally
            {
                cfg.SaveOnConfigSet = saveOnSet;
            }

            cfg.Save();
            cfg.ConfigReloaded += (_, __) => CheckRanges();
        }

        /// <summary>
        /// Builds the shared worm/mass/nest resistance table (spec §3.2) from the live Resist values.
        /// Call it when a hit is processed rather than storing the result, so synced edits take effect immediately.
        /// </summary>
        public static HitData.DamageModifiers BuildDamageModifiers()
        {
            return new HitData.DamageModifiers
            {
                m_blunt = Resist.Blunt.Value,
                m_slash = Resist.Slash.Value,
                m_pierce = Resist.Pierce.Value,
                m_chop = Resist.Chop.Value,
                m_pickaxe = Resist.Pickaxe.Value,
                m_fire = Resist.Fire.Value,
                m_frost = Resist.Frost.Value,
                m_lightning = Resist.Lightning.Value,
                m_poison = Resist.Poison.Value,
                m_spirit = Resist.Spirit.Value,
                m_nonPlayer = HitData.DamageModifier.Normal,
            };
        }

        private static void CheckRanges()
        {
            Dictionary<string, string> raw;
            try
            {
                raw = ConfigHelpers.ReadRawValues(_cfg.ConfigFilePath);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Could not re-read {_cfg.ConfigFilePath} for range checks: {e.Message}");
                return;
            }
            foreach (ConfigEntryBase entry in Entries) ConfigHelpers.WarnIfOutOfRange(entry, raw);
        }

        private static ConfigEntry<T> Synced<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptable = null)
            => BindEntry(section, key, defaultValue, description, acceptable, adminOnly: true);

        private static ConfigEntry<T> Local<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptable = null)
            => BindEntry(section, key, defaultValue, description, acceptable, adminOnly: false);

        private static ConfigEntry<T> BindEntry<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptable, bool adminOnly)
        {
            // Descending Order keeps the ConfigurationManager listing in the same order as the spec table.
            var attributes = new ConfigurationManagerAttributes { IsAdminOnly = adminOnly, Order = --_order };
            ConfigEntry<T> entry = _cfg.Bind(section, key, defaultValue, new ConfigDescription(description, acceptable, attributes));
            Entries.Add(entry);
            return entry;
        }

        private static AcceptableValueRange<float> F(float min, float max) => new AcceptableValueRange<float>(min, max);
        private static AcceptableValueRange<int> I(int min, int max) => new AcceptableValueRange<int>(min, max);

        public static class General
        {
            private const string S = nameof(General);
            public static ConfigEntry<bool> Enabled;

            internal static void Bind()
            {
                Enabled = Synced(S, nameof(Enabled), true,
                    "Master switch. When false nothing spawns (no seeding, laying or releasing) and existing worms, masses and nests stay inert.");
            }
        }

        public static class Worm
        {
            private const string S = nameof(Worm);
            public static ConfigEntry<float> Health;
            public static ConfigEntry<float> MoveSpeed;
            public static ConfigEntry<float> FleeSpeedMultiplier;
            public static ConfigEntry<float> TurnSpeed;
            public static ConfigEntry<float> DurabilityPerBite;
            public static ConfigEntry<float> BiteInterval;
            public static ConfigEntry<float> HealthDamagePerBite;
            public static ConfigEntry<float> CreatureDamagePerBite;
            public static ConfigEntry<float> StructureDamagePerBite;
            public static ConfigEntry<float> CobraDurability;
            public static ConfigEntry<float> CobraCreatureDamage;
            public static ConfigEntry<float> CobraRange;
            public static ConfigEntry<float> CobraWindup;
            public static ConfigEntry<float> CobraCooldown;
            public static ConfigEntry<float> ViewRange;
            public static ConfigEntry<float> ViewAngle;
            public static ConfigEntry<float> HearRange;
            public static ConfigEntry<float> DormantViewRange;
            public static ConfigEntry<float> DormantHearRange;

            internal static void Bind()
            {
                Health = Synced(S, nameof(Health), 10f,
                    "Hit points of a single worm. A mass has n x this value.", F(1f, 200f));
                MoveSpeed = Synced(S, nameof(MoveSpeed), 2.4f,
                    "Crawl speed of a free worm, in m/s.", F(0.5f, 8f));
                FleeSpeedMultiplier = Synced(S, nameof(FleeSpeedMultiplier), 1.35f,
                    "Speed multiplier while fleeing (fire, mass dissolution).", F(1f, 3f));
                TurnSpeed = Synced(S, nameof(TurnSpeed), 360f,
                    "Turn rate of a free worm, in degrees per second.", F(90f, 720f));
                DurabilityPerBite = Synced(S, nameof(DurabilityPerBite), 1f,
                    "Durability points removed from the target's items per engulfing worm per bite tick.", F(0.1f, 20f));
                BiteInterval = Synced(S, nameof(BiteInterval), 0.5f,
                    "Seconds between two aggregated bite ticks of an engulfing mass.", F(0.1f, 5f));
                HealthDamagePerBite = Synced(S, nameof(HealthDamagePerBite), 1f,
                    "Pierce damage per worm per bite on a player who has nothing left to devour.", F(0f, 50f));
                CreatureDamagePerBite = Synced(S, nameof(CreatureDamagePerBite), 1f,
                    "Pierce damage per worm per bite on an engulfed creature.", F(0f, 50f));
                StructureDamagePerBite = Synced(S, nameof(StructureDamagePerBite), 0.5f,
                    "Blunt damage per worm per bite on an engulfed structure.", F(0f, 50f));

                // range chosen: cobra damage/durability share the 0-50 bound of the bite keys.
                CobraDurability = Synced(S, nameof(CobraDurability), 2f,
                    "Durability points removed by the cobra strike of a lone worm.", F(0f, 50f));
                CobraCreatureDamage = Synced(S, nameof(CobraCreatureDamage), 2f,
                    "Pierce damage of the cobra strike against a creature.", F(0f, 50f));
                // range chosen
                CobraRange = Synced(S, nameof(CobraRange), 1.2f,
                    "Distance in metres at which a lone worm starts its cobra strike.", F(0.3f, 5f));
                // range chosen
                CobraWindup = Synced(S, nameof(CobraWindup), 0.6f,
                    "Rear-up time in seconds before the cobra strike lands (the dodge window).", F(0.1f, 5f));
                // range chosen
                CobraCooldown = Synced(S, nameof(CobraCooldown), 1.5f,
                    "Recovery time in seconds after a cobra strike.", F(0.1f, 10f));

                // range chosen: perception keys only state units (m, degrees) in the spec.
                ViewRange = Synced(S, nameof(ViewRange), 20f,
                    "Sight range in metres of an active worm.", F(1f, 100f));
                ViewAngle = Synced(S, nameof(ViewAngle), 360f,
                    "Full field of view in degrees of an active worm (360 = all around).", F(10f, 360f));
                HearRange = Synced(S, nameof(HearRange), 15f,
                    "Hearing range in metres of an active worm (target noise applies as in vanilla).", F(0f, 100f));
                DormantViewRange = Synced(S, nameof(DormantViewRange), 4f,
                    "Sight range in metres of a sleeping worm.", F(0f, 50f));
                DormantHearRange = Synced(S, nameof(DormantHearRange), 6f,
                    "Hearing range in metres of a sleeping worm.", F(0f, 50f));
            }
        }

        public static class Resist
        {
            private const string S = nameof(Resist);
            public static ConfigEntry<HitData.DamageModifier> Fire;
            public static ConfigEntry<HitData.DamageModifier> Pierce;
            public static ConfigEntry<HitData.DamageModifier> Blunt;
            public static ConfigEntry<HitData.DamageModifier> Slash;
            public static ConfigEntry<HitData.DamageModifier> Frost;
            public static ConfigEntry<HitData.DamageModifier> Lightning;
            public static ConfigEntry<HitData.DamageModifier> Poison;
            public static ConfigEntry<HitData.DamageModifier> Spirit;
            public static ConfigEntry<HitData.DamageModifier> Chop;
            public static ConfigEntry<HitData.DamageModifier> Pickaxe;

            // Enums get their acceptable values listed by BepInEx itself; AcceptableValueList<T> needs IEquatable<T>, which enums lack.
            internal static void Bind()
            {
                Fire = Mod(nameof(Fire), HitData.DamageModifier.VeryWeak, "fire");
                Pierce = Mod(nameof(Pierce), HitData.DamageModifier.Resistant, "pierce");
                Blunt = Mod(nameof(Blunt), HitData.DamageModifier.Normal, "blunt");
                Slash = Mod(nameof(Slash), HitData.DamageModifier.Weak, "slash");
                Frost = Mod(nameof(Frost), HitData.DamageModifier.Normal, "frost");
                Lightning = Mod(nameof(Lightning), HitData.DamageModifier.Normal, "lightning");
                Poison = Mod(nameof(Poison), HitData.DamageModifier.Immune, "poison");
                Spirit = Mod(nameof(Spirit), HitData.DamageModifier.Immune, "spirit");
                Chop = Mod(nameof(Chop), HitData.DamageModifier.Ignore, "chop (axe)");
                Pickaxe = Mod(nameof(Pickaxe), HitData.DamageModifier.Ignore, "pickaxe");
            }

            private static ConfigEntry<HitData.DamageModifier> Mod(string key, HitData.DamageModifier defaultValue, string damageName)
                => Synced(S, key, defaultValue, $"Modifier applied to {damageName} damage on worms, masses and nests.");
        }

        public static class Mass
        {
            private const string S = nameof(Mass);
            public static ConfigEntry<int> FormThreshold;
            public static ConfigEntry<float> FormRadius;
            public static ConfigEntry<float> GatherRadius;
            public static ConfigEntry<int> MaxCount;
            public static ConfigEntry<int> DissolveThreshold;
            public static ConfigEntry<float> BaseRadius;
            public static ConfigEntry<float> RadiusExponent;
            public static ConfigEntry<float> MaxColliderRadius;
            public static ConfigEntry<float> MoveSpeed;
            public static ConfigEntry<float> TargetRange;
            public static ConfigEntry<float> EngulfRange;
            public static ConfigEntry<float> DetachDistance;
            public static ConfigEntry<float> DetachDelay;
            public static ConfigEntry<float> KnockbackMinFactor;
            public static ConfigEntry<float> StreamHeightFactor;
            public static ConfigEntry<float> StreamLengthPerWorm;
            public static ConfigEntry<float> StreamMaxLength;
            public static ConfigEntry<float> RegroupRadius;

            internal static void Bind()
            {
                FormThreshold = Synced(S, nameof(FormThreshold), 3,
                    "Minimum number of gathering worms close to their centroid that form a mass.", I(2, 50));
                FormRadius = Synced(S, nameof(FormRadius), 2.5f,
                    "Distance in metres to the group centroid within which gathering worms form a mass.", F(1f, 10f));
                GatherRadius = Synced(S, nameof(GatherRadius), 20f,
                    "Radius in metres within which a worm that spotted a player looks for a mass or group to join.", F(5f, 60f));
                MaxCount = Synced(S, nameof(MaxCount), 200,
                    "Maximum worms in one mass; the surplus forms or keeps a separate mass.", I(10, 1000));
                DissolveThreshold = Synced(S, nameof(DissolveThreshold), 5,
                    "A mass whose count drops below this dissolves into fleeing worms.", I(1, 50));
                BaseRadius = Synced(S, nameof(BaseRadius), 0.83f,
                    "Visual radius in metres of a 10-worm mass (R10 in r(n) = R10 * (n / 10)^e). Calibrated in game (spec §4.5): 10 worms ≈ a tick, 200 ≈ 0.9 × a seeker soldier.", F(0.1f, 2f));
                RadiusExponent = Synced(S, nameof(RadiusExponent), 0.36f,
                    "Exponent e of the mass radius curve r(n) = R10 * (n / 10)^e.", F(0.2f, 1f));
                MaxColliderRadius = Synced(S, nameof(MaxColliderRadius), 0.6f,
                    "Upper bound in metres of the physical collider; the visual dome may be larger.", F(0.2f, 2f));

                // range chosen: same bounds as Worm.MoveSpeed.
                MoveSpeed = Synced(S, nameof(MoveSpeed), 2.4f,
                    "Movement speed of a mass, in m/s.", F(0.5f, 8f));
                // range chosen
                TargetRange = Synced(S, nameof(TargetRange), 20f,
                    "Range in metres within which a mass picks visible targets.", F(2f, 100f));
                // range chosen
                EngulfRange = Synced(S, nameof(EngulfRange), 0.6f,
                    "Extra distance in metres beyond the mass radius at which it engulfs its target.", F(0.1f, 5f));
                // range chosen
                DetachDistance = Synced(S, nameof(DetachDistance), 1.5f,
                    "Extra distance in metres beyond the mass radius past which an engulfed target starts breaking free.", F(0.2f, 10f));
                // range chosen
                DetachDelay = Synced(S, nameof(DetachDelay), 0.75f,
                    "Seconds the target must stay beyond DetachDistance for the mass to let go.", F(0f, 10f));

                KnockbackMinFactor = Synced(S, nameof(KnockbackMinFactor), 0.1f,
                    "Lower bound of the knockback multiplier clamp(10 / n, value, 1).", F(0f, 1f));
                StreamHeightFactor = Synced(S, nameof(StreamHeightFactor), 0.4f,
                    "Height of a mass flowing through a narrow passage, as a fraction of its dome height (spec §4.7).", F(0.1f, 1f));
                StreamLengthPerWorm = Synced(S, nameof(StreamLengthPerWorm), 0.08f,
                    "Metres of ribbon per worm of a flowing mass, up to StreamMaxLength.", F(0.01f, 0.5f));
                StreamMaxLength = Synced(S, nameof(StreamMaxLength), 6f,
                    "Longest ribbon of a flowing mass, in metres.", F(1f, 20f));
                RegroupRadius = Synced(S, nameof(RegroupRadius), 30f,
                    "Radius in metres within which fleeing worms look for a group to regroup with (spec §4.4).", F(5f, 100f));
            }
        }

        public static class Engulf
        {
            private const string S = nameof(Engulf);
            public static ConfigEntry<float> SlowFactor;
            public static ConfigEntry<bool> WisplightLast;
            public static ConfigEntry<bool> IncludeUnequippedInventory;
            public static ConfigEntry<int> ItemsPerTenWorms;
            public static ConfigEntry<bool> BlockReducesBites;
            public static ConfigEntry<bool> ArmorReducesBites;
            public static ConfigEntry<bool> ScaleWithWorldModifiers;
            public static ConfigEntry<bool> ShowDurabilityText;
            public static ConfigEntry<string> WisplightPrefab;
            public static ConfigEntry<float> WisplightVirtualDurability;

            internal static void Bind()
            {
                SlowFactor = Synced(S, nameof(SlowFactor), 0.5f,
                    "Movement speed multiplier (sprint included) on a player under the Infested effect.", F(0.1f, 1f));
                WisplightLast = Synced(S, nameof(WisplightLast), true,
                    "The Wisplight only becomes edible once every other item with durability is broken.");
                IncludeUnequippedInventory = Synced(S, nameof(IncludeUnequippedInventory), true,
                    "Bites also eat unequipped items with durability anywhere in the inventory, not only worn gear.");
                ItemsPerTenWorms = Synced(S, nameof(ItemsPerTenWorms), 1,
                    "Items bitten per tick for every 10 worms of the mass (k = clamp(ceil(n / 10) * value, 1, eligible items)).", I(1, 10));
                BlockReducesBites = Synced(S, nameof(BlockReducesBites), true,
                    "Bite ticks go through vanilla blocking (shield block power, stamina, perfect block).");
                ArmorReducesBites = Synced(S, nameof(ArmorReducesBites), false,
                    "Apply armor to bite ticks. Off by default: bites eat gear, armor does not stop them.");
                ScaleWithWorldModifiers = Synced(S, nameof(ScaleWithWorldModifiers), true,
                    "Scale durability bites with the world's combat difficulty modifier, like health damage.");
                ShowDurabilityText = Synced(S, nameof(ShowDurabilityText), true,
                    "Show the floating orange text with the durability eaten on each bite tick.");
                WisplightPrefab = Synced(S, nameof(WisplightPrefab), "Demister",
                    "Prefab name of the Wisplight item eaten last.");
                // Named in spec §6.3 but missing from the §14 table; only used if the Wisplight has no durability of its own.
                // range chosen
                WisplightVirtualDurability = Synced(S, nameof(WisplightVirtualDurability), 100f,
                    "Durability given to the Wisplight at startup when the vanilla item has none.", F(1f, 10000f));
            }
        }

        public static class Targets
        {
            private const string S = nameof(Targets);
            public static ConfigEntry<bool> AttackPlayers;
            public static ConfigEntry<bool> AttackTamed;
            public static ConfigEntry<bool> AttackCreatures;
            public static ConfigEntry<bool> AttackStructures;
            public static ConfigEntry<float> PlayerWeight;
            public static ConfigEntry<float> TamedWeight;
            public static ConfigEntry<float> CreatureWeight;
            public static ConfigEntry<float> StructureWeight;
            public static ConfigEntry<string> ExcludedPrefabs;
            public static ConfigEntry<bool> WakeOnlyByPlayers;
            public static ConfigEntry<float> ActiveRadius;
            public static ConfigEntry<float> CalmDelay;

            internal static void Bind()
            {
                AttackPlayers = Synced(S, nameof(AttackPlayers), true, "Active worms and masses attack players.");
                AttackTamed = Synced(S, nameof(AttackTamed), true, "Active worms and masses attack tamed creatures.");
                AttackCreatures = Synced(S, nameof(AttackCreatures), true,
                    "Active worms and masses attack wild creatures (seekers, ticks, dvergr...).");
                AttackStructures = Synced(S, nameof(AttackStructures), true,
                    "Active worms and masses attack destructible structures (player pieces, dvergr doors and props).");
                PlayerWeight = Synced(S, nameof(PlayerWeight), 0.6f,
                    "Distance weight of players in target choice (lowest distance x weight wins).", F(0.1f, 5f));
                TamedWeight = Synced(S, nameof(TamedWeight), 0.8f,
                    "Distance weight of tamed creatures in target choice.", F(0.1f, 5f));
                CreatureWeight = Synced(S, nameof(CreatureWeight), 1f,
                    "Distance weight of wild creatures in target choice.", F(0.1f, 5f));
                StructureWeight = Synced(S, nameof(StructureWeight), 1.5f,
                    "Distance weight of structures in target choice.", F(0.1f, 5f));
                ExcludedPrefabs = Synced(S, nameof(ExcludedPrefabs), "SW_DivineWhale",
                    "Comma separated prefab names that worms and masses never target (SkyWhale compatibility, spec §10.4).");
                WakeOnlyByPlayers = Synced(S, nameof(WakeOnlyByPlayers), true,
                    "Only players can wake a dormant nest group; creatures fighting sleeping worms wake nothing.");
                ActiveRadius = Synced(S, nameof(ActiveRadius), 45f,
                    "A nest group stays active while a player is within this many metres of one of its members.", F(10f, 200f));
                CalmDelay = Synced(S, nameof(CalmDelay), 60f,
                    "Seconds without a player in ActiveRadius before a nest group goes back to sleep.", F(5f, 600f));
            }
        }

        public static class Fire
        {
            private const string S = nameof(Fire);
            public static ConfigEntry<bool> AfraidOfFire;
            public static ConfigEntry<float> FearRadius;
            public static ConfigEntry<float> FearDuration;

            internal static void Bind()
            {
                AfraidOfFire = Synced(S, nameof(AfraidOfFire), true,
                    "Active worms and masses flee fire sources (fire areas, torches held by humanoids).");
                FearRadius = Synced(S, nameof(FearRadius), 4f,
                    "Distance in metres from a fire source within which worms and masses flee.", F(0f, 15f));
                FearDuration = Synced(S, nameof(FearDuration), 3f,
                    "Seconds a mass keeps fleeing after taking fire damage, even without a lasting source.", F(0f, 30f));
            }
        }

        public static class Drops
        {
            private const string S = nameof(Drops);
            public static ConfigEntry<float> EquipmentDropRatio;
            public static ConfigEntry<bool> IncludeUpgradeMaterials;
            public static ConfigEntry<float> BaitChance;
            public static ConfigEntry<float> EntrailsChance;
            public static ConfigEntry<float> BloodbagChance;
            public static ConfigEntry<string> BaitWeights;
            public static ConfigEntry<int> MaxItemsPerDropEvent;

            internal static void Bind()
            {
                EquipmentDropRatio = Synced(S, nameof(EquipmentDropRatio), 1f,
                    "Multiplier on the recipe materials added to the loot pool by durability eaten.", F(0f, 1f));
                IncludeUpgradeMaterials = Synced(S, nameof(IncludeUpgradeMaterials), true,
                    "Also count upgrade materials (amount per level x (quality - 1)) when gear is eaten.");
                BaitChance = Synced(S, nameof(BaitChance), 0.08f,
                    "Chance per killed worm to drop a fishing bait, picked by BaitWeights.", F(0f, 1f));
                EntrailsChance = Synced(S, nameof(EntrailsChance), 0.08f,
                    "Chance per killed worm to drop entrails.", F(0f, 1f));
                BloodbagChance = Synced(S, nameof(BloodbagChance), 0.04f,
                    "Chance per killed worm to drop a bloodbag.", F(0f, 1f));
                // Free-form list; left empty here and filled from the ObjectDB on first launch (phase 3).
                BaitWeights = Synced(S, nameof(BaitWeights), string.Empty,
                    "Fishing bait weights as prefab:weight,prefab:weight. Generated on first launch; unknown prefabs are ignored with a warning.");
                MaxItemsPerDropEvent = Synced(S, nameof(MaxItemsPerDropEvent), 60,
                    "Maximum item drops spawned by one kill event; the surplus stays in the pool.", I(1, 500));
            }
        }

        public static class Nest
        {
            private const string S = nameof(Nest);
            public static ConfigEntry<float> Health;
            public static ConfigEntry<int> InitialWorms;
            public static ConfigEntry<int> FreeWormsPerRoom;
            public static ConfigEntry<float> SpawnInterval;
            public static ConfigEntry<int> MaxAlivePerNest;
            public static ConfigEntry<int> ReleaseOnDestroy;
            public static ConfigEntry<float> WakeRadius;
            public static ConfigEntry<float> MinRoomArea;
            public static ConfigEntry<int> MaxNestsPerMine;

            internal static void Bind()
            {
                // range chosen
                Health = Synced(S, nameof(Health), 150f,
                    "Hit points of a nest.", F(1f, 10000f));
                // range chosen
                InitialWorms = Synced(S, nameof(InitialWorms), 8,
                    "Sleeping worms placed around a nest when a mine is seeded.", I(0, 100));
                // range chosen
                FreeWormsPerRoom = Synced(S, nameof(FreeWormsPerRoom), 3,
                    "Average lone sleeping worms scattered in each room when a mine is seeded (+/- 1).", I(0, 50));
                // range chosen
                SpawnInterval = Synced(S, nameof(SpawnInterval), 20f,
                    "Seconds between two worms laid by a nest while a player is in the mine.", F(1f, 600f));
                // range chosen
                MaxAlivePerNest = Synced(S, nameof(MaxAlivePerNest), 50,
                    "A nest stops laying once this many worms from it are alive (free or inside a mass).", I(1, 500));
                // range chosen
                ReleaseOnDestroy = Synced(S, nameof(ReleaseOnDestroy), 5,
                    "Active worms released when a nest is destroyed.", I(0, 100));
                // range chosen
                WakeRadius = Synced(S, nameof(WakeRadius), 10f,
                    "A player within this many metres of a nest, in line of sight, wakes its group.", F(1f, 100f));
                // range chosen
                MinRoomArea = Synced(S, nameof(MinRoomArea), 25f,
                    "Minimum floor area in square metres of a room that gets a nest.", F(1f, 1000f));
                MaxNestsPerMine = Synced(S, nameof(MaxNestsPerMine), 24,
                    "Maximum nests per mine; further rooms only get lone worms.", I(1, 64));
            }
        }

        public static class Colony
        {
            private const string S = nameof(Colony);
            public static ConfigEntry<int> MaxAlivePerMine;
            public static ConfigEntry<float> AlertRadius;

            internal static void Bind()
            {
                MaxAlivePerMine = Synced(S, nameof(MaxAlivePerMine), 400,
                    "Maximum living worms per mine (free and inside masses). Read once when a colony loads.", I(10, 2000));
                AlertRadius = Synced(S, nameof(AlertRadius), 25f,
                    "An alert spreads to every worm and nest within this many metres, nest after nest.", F(5f, 100f));
            }
        }

        public static class Network
        {
            private const string S = nameof(Network);
            public static ConfigEntry<float> SyncRate;
            public static ConfigEntry<float> SnapshotInterval;

            internal static void Bind()
            {
                SyncRate = Synced(S, nameof(SyncRate), 5f,
                    "Rate in Hz of worm delta updates sent to nearby peers while worms are active.", F(1f, 20f));
                SnapshotInterval = Synced(S, nameof(SnapshotInterval), 5f,
                    "Seconds between full colony snapshots written to the ZDO when something changed.", F(1f, 60f));
            }
        }

        public static class Performance
        {
            private const string S = nameof(Performance);
            public static ConfigEntry<float> RenderDistance;
            public static ConfigEntry<int> MaxVisibleSurfaceWorms;
            public static ConfigEntry<int> MaxSoundSources;
            public static ConfigEntry<float> ShadowDistance;
            public static ConfigEntry<float> SimRateNear;
            public static ConfigEntry<float> SimRateFar;
            public static ConfigEntry<bool> FlipbookBlend;
            public static ConfigEntry<string> ShaderCandidates;

            internal static void Bind()
            {
                // range chosen
                RenderDistance = Local(S, nameof(RenderDistance), 60f,
                    "Worms farther than this many metres from the camera are not drawn.", F(10f, 300f));
                // range chosen: one surface worm per worm at most, and Mass.MaxCount defaults to 200.
                MaxVisibleSurfaceWorms = Local(S, nameof(MaxVisibleSurfaceWorms), 40,
                    "Maximum instanced worms drawn crawling on the surface of one mass.", I(0, 200));
                // range chosen
                MaxSoundSources = Local(S, nameof(MaxSoundSources), 4,
                    "Maximum simultaneous crawl/idle sounds from nearby worms.", I(0, 32));
                // range chosen
                ShadowDistance = Local(S, nameof(ShadowDistance), 25f,
                    "Worms cast shadows up to this many metres from the camera; 0 disables worm shadows.", F(0f, 200f));
                // range chosen
                SimRateNear = Local(S, nameof(SimRateNear), 20f,
                    "Simulation rate in Hz for worms within 30 m of a player (on the owning peer).", F(1f, 60f));
                // range chosen
                SimRateFar = Local(S, nameof(SimRateFar), 5f,
                    "Simulation rate in Hz for worms farther than 30 m from any player.", F(0.5f, 30f));
                FlipbookBlend = Local(S, nameof(FlipbookBlend), false,
                    "Blend between flipbook frames for smoother animation, at extra rendering cost.");
                // Free-form ordered list: any shader name the game may ship is valid.
                ShaderCandidates = Local(S, nameof(ShaderCandidates), "Custom/Creature,Custom/Piece,Standard,Legacy Shaders/Diffuse",
                    "Comma separated vanilla shader names tried in order for worm materials.");
            }
        }

        /// <summary>Dev harness settings (spec §15). Bound in every build so the keys never come back null, but only Debug builds read them.</summary>
        public static class Dev
        {
            private const string S = nameof(Dev);
            public static ConfigEntry<int> BridgePort;
            public static ConfigEntry<string> AutoLoadWorld;
            public static ConfigEntry<string> AutoLoadCharacter;
            public static ConfigEntry<string> WorldSeed;
            public static ConfigEntry<bool> AutoDevCommands;

            internal static void Bind()
            {
                // range chosen: unprivileged TCP ports.
                BridgePort = Local(S, nameof(BridgePort), 27781,
                    "Debug builds only: localhost port of the DevBridge HTTP server (overridden by -mwdev bridge=).", I(1024, 65535));
                AutoLoadWorld = Local(S, nameof(AutoLoadWorld), "MWTest",
                    "Debug builds only: world created and loaded by -mwdev autoload when none is given.");
                AutoLoadCharacter = Local(S, nameof(AutoLoadCharacter), "MWTester",
                    "Debug builds only: character created and loaded by -mwdev autoload when none is given.");
                WorldSeed = Local(S, nameof(WorldSeed), "MistWorms",
                    "Debug builds only: seed of the auto-created test world.");
                AutoDevCommands = Local(S, nameof(AutoDevCommands), true,
                    "Debug builds only: enable devcommands automatically when the test world loads.");
            }
        }
    }
}
