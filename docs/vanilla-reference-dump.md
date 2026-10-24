# MistWorms vanilla dump

Generated 2026-10-24 14:30:00 by `VanillaDump` (MistWorms 0.1.0). Valheim `1.0.20`, Unity `6000.0.75f1`. Raw output, to be curated into `docs/vanilla-reference.md`.

## 1. Reference creatures

### Stats

| Prefab | m_name | Health | Speed | Walk | Run | Turn | RunTurn | Faction | Group | Flying | Boss | TolerateFire |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | $enemy_aspect_seekerqueen (Aspect de la Matriarche rampante) | 1700 | 4 | 2 | 8 | 50 | 120 | Boss |  | False | False | False |
| Seeker | $enemy_seeker (Fouineur) | 200 | 2 | 5 | 6 | 150 | 250 | MistlandsMonsters |  | False | False | False |
| SeekerBrood | $enemy_babyseeker (Jeune fouineur) | 20 | 1 | 1 | 3 | 150 | 250 | MistlandsMonsters |  | False | False | False |
| SeekerBrute | $enemy_seekerbrute (Soldat fouineur) | 1500 | 2 | 2 | 6 | 80 | 150 | MistlandsMonsters |  | False | False | False |
| SeekerQueen | $enemy_seekerqueen (La Reine) | 12500 | 4 | 2 | 8 | 50 | 120 | Boss |  | False | True | False |
| Tick | $enemy_tick (Tique) | 50 | 1 | 5 | 8 | 300 | 400 | MistlandsMonsters |  | False | False | False |

### Perception and AI

| Prefab | AI | ViewRange | ViewAngle | HearRange | AlertRange | AfraidOfFire | AvoidFire | PathAgentType | AttackPlayerObjects | IdleSound (interval, chance) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | MonsterAI | 30 | 90 | 9999 | 30 | False | False | SeekerQueen | True | sfx_HiveQueen_idle (5 s, 0.5) |
| Seeker | MonsterAI | 30 | 120 | 9999 | 30 | False | False | Humanoid | True | sfx_seeker_idle, sfx_seeker_overlay (5 s, 0.5) |
| SeekerBrood | MonsterAI | 30 | 120 | 9999 | 30 | False | False | Humanoid | True | sfx_baby_seeker_idle (5 s, 0.5) |
| SeekerBrute | MonsterAI | 30 | 90 | 9999 | 30 | False | False | HumanoidBig | True | sfx_seeker_brute_idle (10 s, 0.5) |
| SeekerQueen | MonsterAI | 30 | 90 | 9999 | 30 | False | False | SeekerQueen | True | sfx_HiveQueen_idle (5 s, 0.5) |
| Tick | MonsterAI | 30 | 90 | 9999 | 20 | False | False | HumanoidNoSwim | True | sfx_tick_idle (6 s, 0.5) |

### Damage modifiers

| Prefab | blunt | slash | pierce | chop | pickaxe | fire | frost | lightning | poison | spirit | nonPlayer |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | Normal | Normal | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Immune |
| Seeker | Resistant | Resistant | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Normal |
| SeekerBrood | Resistant | Resistant | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Normal |
| SeekerBrute | Resistant | Resistant | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Normal |
| SeekerQueen | Normal | Normal | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Normal |
| Tick | Normal | Normal | Weak | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Normal |

### Size

Mesh AABB is computed from mesh/skin local bounds in root space (LOD0 only, inactive children skipped) because prefab assets report empty `Renderer.bounds`; both are shown.

| Prefab | Root scale | Mesh AABB size (root space) | Renderer.bounds size (as-is) | Colliders |
| --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | 1 x 1 x 1 | 10.39 x 12.077 x 14.86 (min y -1.533) | 10.39 x 12.077 x 14.86 | Aspect_SeekerQueen: CapsuleCollider r 1.4 h 7 dir 1 center 0 x 3.5 x 0 layer character scale 1 x 1 x 1; Visual/Armature/Hip/1/2/3/4/5/character collider (3): CapsuleCollider r 1.2 h 5.204 dir 1 center 0 x 0.842 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/1/2/3/character collider (3): CapsuleCollider r 1.5 h 5.204 dir 1 center 0 x 0.842 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/1/character collider (3): CapsuleCollider r 1.7 h 4 dir 1 center 0 x 0 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/8/9/10/11/12/13/14/15/16/17/head/WeakSpot_head: CapsuleCollider r 0.9 h 4 dir 1 center 0 x 0 x 0 layer character_noenv scale 1.201 x 1.201 x 1.201; Visual/Attack collider: CapsuleCollider r 1.7 h 7.129 dir 1 center 0 x 0.31 x 0 layer character_noenv scale 1 x 1 x 1; RunHitDamager: BoxCollider size 2.73 x 2.43 x 1 center 0 x 0 x 0 trigger layer weapon scale 1 x 1 x 2.42 |
| Seeker | 1 x 1 x 1 | 3.205 x 3.21 x 5.524 (min y -0.836) | 3.205 x 3.21 x 5.524 | Seeker: CapsuleCollider r 0.8 h 1 dir 1 center 0 x 0.8 x 0 layer character scale 1 x 1 x 1; character collider: CapsuleCollider r 0.6 h 2.45 dir 1 center 0 x 0 x 0 layer character_noenv scale 1 x 1 x 1 |
| SeekerBrood | 1 x 1 x 1 | 1.202 x 1.204 x 2.072 (min y -0.313) | 1.202 x 1.204 x 2.072 | SeekerBrood: CapsuleCollider r 0.6 h 1 dir 1 center 0 x 0.6 x 0 layer character scale 1 x 1 x 1 |
| SeekerBrute | 1 x 1 x 1 | 4.398 x 2.667 x 4.913 (min y -0.224) | 4.398 x 2.667 x 4.913 | SeekerBrute: CapsuleCollider r 1.2 h 2.2 dir 1 center 0 x 1.2 x 0 layer character scale 1 x 1 x 1; Visual/Armature/root/root2/hip/l.hip/l.backleg1/l.backleg2/l.backleg3/character collider (1): SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.55 x 0.55 x 0.55; Visual/Armature/root/root2/hip/r.hip/r.backleg1/r.backleg2/r.backleg3/character collider (1): SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.55 x 0.55 x 0.55; Visual/Armature/root/root2/hip/spine/l.shoulder/l.frontarm1/l.frontarm2/l.frontarm3/character collider (1): SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.584 x 0.584 x 0.584; Visual/Armature/root/root2/hip/spine/neck/head/character collider (1): SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.703 x 0.703 x 0.703; Visual/Armature/root/root2/hip/spine/r.shoulder/r.frontarm1/r.frontarm2/r.frontarm3/character collider (1): SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.584 x 0.584 x 0.584; Visual/Armature/root/root2/hip/tail/WeakSpot_ass: SphereCollider r 1 center 0 x 0 x 0 layer character_noenv scale 0.825 x 0.825 x 0.825; Visual/Armature/root/root2/character collider: CapsuleCollider r 0.71 h 2.537 dir 1 center 0 x 0.179 x 0.053 layer character_noenv scale 1 x 1.011 x 1 |
| SeekerQueen | 1 x 1 x 1 | 10.39 x 12.077 x 14.86 (min y -1.533) | 10.39 x 12.077 x 14.86 | SeekerQueen: CapsuleCollider r 1.4 h 7 dir 1 center 0 x 3.5 x 0 layer character scale 1 x 1 x 1; Visual/Armature/Hip/1/2/3/4/5/character collider (3): CapsuleCollider r 1.2 h 5.204 dir 1 center 0 x 0.842 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/1/2/3/character collider (3): CapsuleCollider r 1.5 h 5.204 dir 1 center 0 x 0.842 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/1/character collider (3): CapsuleCollider r 1.7 h 4 dir 1 center 0 x 0 x 0 layer character_noenv scale 1 x 1 x 1; Visual/Armature/Hip/8/9/10/11/12/13/14/15/16/17/head/WeakSpot_head: CapsuleCollider r 0.9 h 4 dir 1 center 0 x 0 x 0 layer character_noenv scale 1.201 x 1.201 x 1.201; Visual/Attack collider: CapsuleCollider r 1.7 h 7.129 dir 1 center 0 x 0.31 x 0 layer character_noenv scale 1 x 1 x 1; RunHitDamager: BoxCollider size 2.73 x 2.43 x 1 center 0 x 0 x 0 trigger layer weapon scale 1 x 1 x 2.42 |
| Tick | 1 x 1 x 1 | 1.262 x 0.907 x 1.251 (min y -0.023) | 1.262 x 0.907 x 1.251 | Tick: CapsuleCollider r 0.6 h 0 dir 1 center 0 x 0.6 x 0 layer character scale 1 x 1 x 1 |

### Aspect_SeekerQueen

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop, FootStep, MovementDamage

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | aspect_SeekerQueen_Rush | OneHandedWeapon | slash 100, chop 300, pickaxe 300 | Horizontal | attack_rush | 8 | 1 | 120 | 2.5 | 25 | 25 | 250 | - | vfx_HitSparks, sfx_dragon_melee_hit |
| default | aspect_SeekerQueen_Bite | OneHandedWeapon | pierce 100, chop 300, pickaxe 300, poison 100 | Horizontal | attack_bite | 10 | 1 | 25 | 2 | 10 | 10 | 250 | - |  |
| default | aspect_SeekerQueen_Spit | OneHandedWeapon | blunt 40, chop 50, pickaxe 50, poison 40 | Projectile | attack_spit | 0 | 0 | 10 | 3 | 25 | 20 | 50 | aspect_SeekerQueen_projectile_spit | vfx_HitSparks, sfx_greydwarf_attack_hit |
| default | aspect_SeekerQueen_Slap | OneHandedWeapon | slash 130, chop 300, pickaxe 300 | Horizontal | attack_slash | 10 | 1 | 145 | 2.5 | 9 | 4 | 250 | - | vfx_HitSparks, sfx_dragon_melee_hit (disabled) |
| default | aspect_SeekerQueen_PierceAOE | OneHandedWeapon | pierce 150, chop 300, pickaxe 300 | Area | attack_pierce | 0 | 0.24 | 146.62 | 4.5 | 4 | 4 | 250 | - |  |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| _(none)_ | | | | | | |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | fx_seeker_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | aspect_aoe_explosion (variant 0), fx_aspect_death (variant 0) |
| BaseAI.m_alertedEffects | sfx_HiveQueen_alerted |
| BaseAI.m_idleSound | sfx_HiveQueen_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Cube | SkinnedMeshRenderer | Cube (7308 v) | Aspect_mat | Custom/Distortion |  | _GRADIENTASALPHA_ON | False | 3000 |
| Visual/Plane | SkinnedMeshRenderer | Plane (1032 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512), _BumpMap=SeekerQueen_n (512x512) | _TWOSIDEDNORMALS_ON | True | 2000 |

### Seeker

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | seeker_takeoff | OneHandedWeapon | pierce 30, chop 200, pickaxe 200, frost 200 | None | attack_takeoff | 1 | 0 | 90 | 0 | 100 | 8 | 30 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |
| default | seeker_land | OneHandedWeapon | pierce 30, chop 200, pickaxe 200, frost 200 | None | attack_land | 1 | 0 | 90 | 0 | 5 | 8 | 30 | dragon_ice_projectile | vfx_HitSparks, sfx_greydwarf_attack_hit |
| default | seeker_pincers | OneHandedWeapon | pierce 90 | Horizontal | attack_pincers | 2.2 | 1 | 45 | 1 | 2 | 6 | 40 | - | vfx_HitSparks, fx_seeker_melee_hit |
| default | seeker_claw_left | OneHandedWeapon | pierce 120 | Horizontal | attack_claw_left | 3.5 | 1 | 20 | 0.7 | 3.5 | 10 | 20 | - | vfx_HitSparks, fx_seeker_melee_hit |
| default | seeker_claw_right | OneHandedWeapon | pierce 120 | Horizontal | attack_claw_right | 3.5 | 1 | 20 | 0.7 | 3.5 | 10 | 20 | - | vfx_HitSparks, fx_seeker_melee_hit |
| default | seeker_groundslam | OneHandedWeapon | blunt 100 | Horizontal | attack_slam | 3.2 | 1 | 60 | 0.7 | 3 | 10 | 150 | - | vfx_HitSparks, fx_seeker_melee_hit |
| default | seeker_groundslam_flying | OneHandedWeapon | blunt 100 | Horizontal | attack_flyingslam | 3.2 | 1 | 60 | 0.7 | 5 | 10 | 150 | - | vfx_HitSparks, fx_seeker_melee_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| BugMeat | 1 | 2 | 1 | False | True | False |
| Carapace | 1 | 2 | 1 | False | True | False |
| TrophySeeker | 1 | 1 | 0.05 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | fx_seeker_hurt, sfx_seeker_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | fx_seeker_death (variant 0) |
| BaseAI.m_alertedEffects | sfx_seeker_alerted |
| BaseAI.m_idleSound | sfx_seeker_idle, sfx_seeker_overlay |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Plane.004 | SkinnedMeshRenderer | Plane.004 (4259 v) | seeker | Custom/Creature | _MainTex=seeker_d (256x256), _BumpMap=seeker_n (256x256), _EmissionMap=blood (128x128) | _SNAPNOISETOPIXEL_ON _TWOSIDEDNORMALS_ON | True | 2000 |

### SeekerBrood

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | babyseeker_attack | OneHandedWeapon | pierce 60 | Horizontal | attack_pincers | 1.7 | 0.68 | 45 | 0.5 | 1.5 | 3 | 40 | - | vfx_HitSparks, fx_seeker_melee_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| RoyalJelly | 1 | 1 | 0.5 | False | False | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | fx_babyseeker_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | fx_babyseeker_death (variant 0) |
| BaseAI.m_alertedEffects | sfx_baby_seeker_alerted |
| BaseAI.m_idleSound | sfx_baby_seeker_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Plane.004 | SkinnedMeshRenderer | Plane.004 (4259 v) | babyseeker | Custom/Creature | _MainTex=babyseeker_d (64x64), _BumpMap=babyseeker_n (64x64), _EmissionMap=babyseeker_d (64x64) | _TWOSIDEDNORMALS_ON _USEGLOSSMAP_ON | True | 2000 |

### SeekerBrute

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | SeekerBrute_Taunt | OneHandedWeapon | slash 80, chop 10 | None | taunt | 3.56 | 1.5 | 45 | 1 | 20 | 60 | 50 | - |  |
| default | SeekerBrute_ram | OneHandedWeapon | blunt 100, chop 100, pickaxe 100 | Horizontal | attack_ram | 4 | 1 | 45 | 1 | 4 | 8 | 200 | - | vfx_HitSparks, sfx_seeker_brute_attack_ram_hit |
| default | SeekerBrute_bite | OneHandedWeapon | slash 100 | Horizontal | attack_bite | 4 | 1 | 45 | 1 | 4 | 8 | 70 | - | vfx_HitSparks, sfx_seeker_brute_attack_bite |
| default | SeekerBrute_groundslam | OneHandedWeapon | blunt 120, chop 100, pickaxe 100 | Area | attack_slam | 0.5 | 0.1 | 90 | 4 | 5 | 12 | 100 | - |  |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| BugMeat | 1 | 2 | 1 | False | True | False |
| Carapace | 2 | 4 | 1 | False | True | False |
| TrophySeekerBrute | 1 | 1 | 0.05 | False | True | False |
| Mandible | 1 | 2 | 1 | False | False | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | fx_seeker_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | fx_seekerbrute_death (variant 0) |
| BaseAI.m_alertedEffects | sfx_seeker_brute_alerted |
| BaseAI.m_idleSound | sfx_seeker_brute_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/SeekerBrute | SkinnedMeshRenderer | SeekerBrute (4042 v) | seeker_Brute_mat | Custom/Creature | _MainTex=seekerBrute_d (256x256), _BumpMap=seekerBrute_n (256x256), _EmissionMap=seekerBrute_e (256x256) | _TWOSIDEDNORMALS_ON | True | 2000 |

### SeekerQueen

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop, FootStep, MovementDamage

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | SeekerQueen_Teleport | OneHandedWeapon | slash 80, chop 10 | Projectile | attack_teleport | 3.56 | 1.5 | 45 | 1 | 400 | 60 | 50 | SeekerQueen_projectile_teleport |  |
| default | SeekerQueen_Rush | OneHandedWeapon | slash 100, chop 300, pickaxe 300 | Horizontal | attack_rush | 8 | 1 | 120 | 2.5 | 25 | 25 | 250 | - | vfx_HitSparks, sfx_dragon_melee_hit |
| default | SeekerQueen_Bite | OneHandedWeapon | pierce 140, chop 300, pickaxe 300, poison 100 | Horizontal | attack_bite | 10 | 1 | 25 | 2 | 10 | 10 | 250 | - |  |
| default | SeekerQueen_Call | OneHandedWeapon | slash 80, chop 10 | Projectile | attack_call | 3.56 | 1.5 | 45 | 1 | 25 | 60 | 50 | SeekerQueen_triggerspawn_ability |  |
| default | SeekerQueen_Spit | OneHandedWeapon | blunt 40, chop 50, pickaxe 50, poison 40 | Projectile | attack_spit | 0 | 0 | 10 | 3 | 25 | 20 | 50 | SeekerQueen_projectile_spit | vfx_HitSparks, sfx_greydwarf_attack_hit |
| default | SeekerQueen_Slap | OneHandedWeapon | slash 130, chop 300, pickaxe 300 | Horizontal | attack_slash | 10 | 1 | 145 | 2.5 | 9 | 4 | 250 | - | vfx_HitSparks, sfx_dragon_melee_hit (disabled) |
| default | SeekerQueen_PierceAOE | OneHandedWeapon | pierce 150, chop 300, pickaxe 300 | Area | attack_pierce | 0 | 0.24 | 146.62 | 4.5 | 4 | 4 | 250 | - |  |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophySeekerQueen | 1 | 1 | 1 | False | True | True |
| QueenDrop | 5 | 5 | 1 | False | False | True |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | fx_seeker_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | fx_Queen_Death (variant 0) |
| BaseAI.m_alertedEffects | sfx_HiveQueen_alerted |
| BaseAI.m_idleSound | sfx_HiveQueen_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Cube | SkinnedMeshRenderer | Cube (7308 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512), _BumpMap=SeekerQueen_n (512x512) | _TWOSIDEDNORMALS_ON | True | 2000 |
| Visual/Plane | SkinnedMeshRenderer | Plane (1032 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512), _BumpMap=SeekerQueen_n (512x512) | _TWOSIDEDNORMALS_ON | True | 2000 |

### Tick

Components: CapsuleCollider, Rigidbody, Humanoid, MonsterAI, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | tick_attack_attach | OneHandedWeapon | pierce 50 | Horizontal | attack | 1 | 0.1 | 40 | 0.5 | 4 | 2 | 0 | - | fx_TickBloodHit, sfx_tick_attack_drain |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| GiantBloodSack | 1 | 1 | 1 | False | False | False |
| TrophyTick | 1 | 1 | 0.05 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_boar_hit, sfx_tick_hurt |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | fx_tick_death |
| BaseAI.m_alertedEffects | sfx_tick_alerted |
| BaseAI.m_idleSound | sfx_tick_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/FeastingMesh | SkinnedMeshRenderer | FeastingMesh (1960 v) | Feasting_mat | Custom/Creature | _MainTex=Feasting_d (64x64), _BumpMap=Feasting_n (64x64), _MetallicGlossMap=Feasting_m (64x64) |  | True | 2000 |

### Other prefabs matching Seeker / Tick

| Prefab | Components |
| --- | --- |
| SeekerAspic | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, Piece, WearNTear |
| SeekerBrute_Taunt | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerEgg | LODGroup, ZNetView, Destructible, DropOnDestroyed, EggHatch |
| SeekerEgg_alwayshatch | LODGroup, ZNetView, Destructible, DropOnDestroyed, EggHatch |
| SeekerQueen_Call | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerQueen_Teleport | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerQueen_projectile_spit | Projectile, ZNetView, ZSyncTransform |
| SeekerQueen_projectile_teleport | ZNetView, TeleportAbility |
| SeekerQueen_spithit | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction |
| Spawner_Seeker | ZNetView, CreatureSpawner |
| Spawner_SeekerBrute | ZNetView, CreatureSpawner |
| Spawner_SeekerBrute_respawn_240 | ZNetView, CreatureSpawner |
| Spawner_Seeker_respawn_240 | ZNetView, CreatureSpawner |
| Spawner_Tick | ZNetView, CreatureSpawner |
| Spawner_Tick_stared | ZNetView, CreatureSpawner |
| Spawner_Tick_stared_respawn_240 | ZNetView, CreatureSpawner |
| TriggerSpawner_Seeker | ZNetView, TriggerSpawner, LODGroup |
| TrophySeeker | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophySeekerBrute | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophySeekerQueen | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem, Floating |
| TrophyTick | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| aspect_SeekerQueen_projectile_spit | Projectile, ZNetView, ZSyncTransform |
| fx_TickBloodHit | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction |
| prop_itemstand_TrophySeekerBrute | ZNetView, LODGroup, Destructible, DropOnDestroyed |

### Shader properties (creature materials)

`Custom/Distortion` (9 properties): `_Color` Color, `_MainTex` Texture, `_NormalTex` Texture, `_Glossiness` Range, `_Metallic` Range, `_NormalScale` Float, `_RefractionIntensity` Float, `_WaveVel` Float, `_DepthFade` Float

`Custom/Creature` (38 properties): `_ColorFoldout` Float, `_MainTex` Texture, `_Color` Color, `_MainTex_ST` Vector, `_HSVHeader` Float, `_Hue` Range, `_Saturation` Range, `_Value` Range, `_EmissionSpacer` Float, `_EmissionMap` Texture, `_EmissionColor` Color, `_SmoothnessFoldout` Float, `_MetallicGlossMap` Texture, `_Metallic` Range, `_MetalColor` Color, `_UseGlossmap` Float, `_Glossiness` Range, `_MetalGloss` Range, `_NormalFoldout` Float, `_BumpMap` Texture, `_BumpScale` Range, `_TwoSidedNormals` Float, `_MiscFoldout` Float, `_Cull` Float, `_Cutoff` Range, `_AddRain` Float, `_NoiseGlowEnabled` Float, `_UseStyles` Float, `_SnowCover` Range, `_StylesFoldout` Float, `_StyleTex` Texture, `_Style` Float, `_NoiseGlowFoldout` Float, `_SnapNoiseToPixel` Float, `_NoiseGlowTex` Texture, `_NoiseGlowSize` Float, `_NoiseGlowColor` Color, `_NoiseGlowEdge` Range

## 1b. World-space sizes

Measured on the prefab assets (nothing instantiated). Every point is taken to the prefab root space with `root.worldToLocalMatrix` (root position/rotation dropped) and then multiplied by `root.lossyScale`, so the root scale is applied. Renderers: active `SkinnedMeshRenderer` / `MeshRenderer` only (LOD0 when a LODGroup exists, disabled renderers and inactive children skipped). Skinned: `localBounds` in rootBone space (renderer transform if no rootBone). Alt AABB: all used renderers re-measured with `sharedMesh.bounds` in renderer space, as a cross-check. Collider sizes use the collider transform `lossyScale`.

### Summary

| Prefab | Width X (m) | Height Y (m) | Length Z (m) | Footprint D = sqrt(X*Z) (m) | Main collider (world) | Root scale | Renderers used |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Player | 1.369 | 2.163 | 0.991 | 1.165 | CapsuleCollider r 0.49 h 1.85 dir Y | 1 x 1 x 1 | 1 (+0 skipped) |
| Tick | 1.262 | 0.907 | 1.251 | 1.256 | CapsuleCollider r 0.6 h 0 dir Y | 1 x 1 x 1 | 1 (+0 skipped) |
| Seeker | 3.205 | 3.21 | 5.524 | 4.208 | CapsuleCollider r 0.8 h 1 dir Y | 1 x 1 x 1 | 1 (+0 skipped) |
| SeekerBrood | 1.202 | 1.204 | 2.072 | 1.578 | CapsuleCollider r 0.6 h 1 dir Y | 1 x 1 x 1 | 1 (+0 skipped) |
| SeekerBrute | 4.398 | 2.667 | 4.913 | 4.648 | CapsuleCollider r 1.2 h 2.2 dir Y | 1 x 1 x 1 | 1 (+0 skipped) |

Player sanity check: measured player height = 2.163 m (expected about 1.8 - 2.0 m).

### Player (world size)

Root localScale 1 x 1 x 1, lossyScale 1 x 1 x 1; 1 LODGroup (LOD0 only, 0 LOD1+ renderers excluded); 1 renderers used, 0 skipped. Combined AABB 1.369 x 2.163 x 0.991, y range -0.156 .. 2.007, center 0 x 0.925 x -0.038. Alt AABB (mesh.bounds, renderer space) 1.352 x 1.905 x 0.379.

| Renderer | Kind | Local bounds size | Root-space AABB size | Y range (root space) |
| --- | --- | --- | --- | --- |
| Visual/body | skinned (rootBone Hips) | 0.014 x 0.023 x 0.01 | 1.369 x 2.163 x 0.991 | -0.156 .. 2.007 |

| Collider | Type | Layer | IsTrigger | World dims | Center (root space) | State |
| --- | --- | --- | --- | --- | --- | --- |
| Player | CapsuleCollider | character | False | r 0.49 h 1.85 dir Y | 0 x 0.925 x 0 |  |

### Tick (world size)

Root localScale 1 x 1 x 1, lossyScale 1 x 1 x 1; 1 LODGroup (LOD0 only, 0 LOD1+ renderers excluded); 1 renderers used, 0 skipped. Combined AABB 1.262 x 0.907 x 1.251, y range -0.023 .. 0.884, center 0 x 0.431 x 0.077. Alt AABB (mesh.bounds, renderer space) 1.226 x 0.889 x 1.225.

| Renderer | Kind | Local bounds size | Root-space AABB size | Y range (root space) |
| --- | --- | --- | --- | --- |
| Visual/FeastingMesh | skinned (rootBone Root) | 0.013 x 0.013 x 0.009 | 1.262 x 0.907 x 1.251 | -0.023 .. 0.884 |

| Collider | Type | Layer | IsTrigger | World dims | Center (root space) | State |
| --- | --- | --- | --- | --- | --- | --- |
| Tick | CapsuleCollider | character | False | r 0.6 h 0 dir Y | 0 x 0.6 x 0 |  |

### Seeker (world size)

Root localScale 1 x 1 x 1, lossyScale 1 x 1 x 1; 1 LODGroup (LOD0 only, 0 LOD1+ renderers excluded); 1 renderers used, 0 skipped. Combined AABB 3.205 x 3.21 x 5.524, y range -0.836 .. 2.374, center 0.049 x 0.769 x 0.162. Alt AABB (mesh.bounds, renderer space) 5.797 x 2.547 x 4.111.

| Renderer | Kind | Local bounds size | Root-space AABB size | Y range (root space) |
| --- | --- | --- | --- | --- |
| Visual/Plane.004 | skinned (rootBone root) | 0.04 x 0.04 x 0.069 | 3.205 x 3.21 x 5.524 | -0.836 .. 2.374 |

| Collider | Type | Layer | IsTrigger | World dims | Center (root space) | State |
| --- | --- | --- | --- | --- | --- | --- |
| Seeker | CapsuleCollider | character | False | r 0.8 h 1 dir Y | 0 x 0.8 x 0 |  |
| character collider | CapsuleCollider | character_noenv | False | r 0.6 h 2.45 dir Y | 0 x 0.716 x -0.092 |  |

### SeekerBrood (world size)

Root localScale 1 x 1 x 1, lossyScale 1 x 1 x 1; 1 LODGroup (LOD0 only, 0 LOD1+ renderers excluded); 1 renderers used, 0 skipped. Combined AABB 1.202 x 1.204 x 2.072, y range -0.313 .. 0.89, center 0.018 x 0.289 x 0.061. Alt AABB (mesh.bounds, renderer space) 2.174 x 0.955 x 1.542.

| Renderer | Kind | Local bounds size | Root-space AABB size | Y range (root space) |
| --- | --- | --- | --- | --- |
| Visual/Plane.004 | skinned (rootBone root) | 0.04 x 0.04 x 0.069 | 1.202 x 1.204 x 2.072 | -0.313 .. 0.89 |

| Collider | Type | Layer | IsTrigger | World dims | Center (root space) | State |
| --- | --- | --- | --- | --- | --- | --- |
| SeekerBrood | CapsuleCollider | character | False | r 0.6 h 1 dir Y | 0 x 0.6 x 0 |  |

### SeekerBrute (world size)

Root localScale 1 x 1 x 1, lossyScale 1 x 1 x 1; 1 LODGroup (LOD0 only, 0 LOD1+ renderers excluded); 1 renderers used, 0 skipped. Combined AABB 4.398 x 2.667 x 4.913, y range -0.224 .. 2.443, center 0.019 x 1.11 x -0.166. Alt AABB (mesh.bounds, renderer space) 4.812 x 2.513 x 4.411.

| Renderer | Kind | Local bounds size | Root-space AABB size | Y range (root space) |
| --- | --- | --- | --- | --- |
| Visual/SeekerBrute | skinned (rootBone root) | 0.044 x 0.027 x 0.049 | 4.398 x 2.667 x 4.913 | -0.224 .. 2.443 |

| Collider | Type | Layer | IsTrigger | World dims | Center (root space) | State |
| --- | --- | --- | --- | --- | --- | --- |
| SeekerBrute | CapsuleCollider | character | False | r 1.2 h 2.2 dir Y | 0 x 1.2 x 0 |  |
| Visual/Armature/root/root2/hip/l.hip/l.backleg1/l.backleg2/l.backleg3/character collider (1) | SphereCollider | character_noenv | False | r 0.55 | -1.256 x 0.723 x -1.107 |  |
| Visual/Armature/root/root2/hip/r.hip/r.backleg1/r.backleg2/r.backleg3/character collider (1) | SphereCollider | character_noenv | False | r 0.55 | 1.297 x 0.718 x -1.092 |  |
| Visual/Armature/root/root2/hip/spine/l.shoulder/l.frontarm1/l.frontarm2/l.frontarm3/character collider (1) | SphereCollider | character_noenv | False | r 0.584 | -1.239 x 0.785 x 1.329 |  |
| Visual/Armature/root/root2/hip/spine/neck/head/character collider (1) | SphereCollider | character_noenv | False | r 0.703 | 0 x 1.246 x 1.034 |  |
| Visual/Armature/root/root2/hip/spine/r.shoulder/r.frontarm1/r.frontarm2/r.frontarm3/character collider (1) | SphereCollider | character_noenv | False | r 0.584 | 1.204 x 0.783 x 1.357 |  |
| Visual/Armature/root/root2/hip/tail/WeakSpot_ass | SphereCollider | character_noenv | False | r 0.825 | 0 x 1.292 x -1.516 |  |
| Visual/Armature/root/root2/character collider | CapsuleCollider | character_noenv | False | r 0.71 h 2.565 dir Y | 0 x 1.21 x -0.11 |  |

## 2. Effect prefabs (sfx_ / vfx_ / fx_)

| Prefix | Count |
| --- | --- |
| sfx_ | 791 |
| vfx_ | 487 |
| fx_ | 281 |

Keyword filter: tick, seeker, brood, egg, bug, insect, crawl, squish, death, blood.

| Prefab | List | Details |
| --- | --- | --- |
| fx_Dverger_death | net | ZSFX clips [Enemy_Dverger_VO_Death_01, Enemy_Dverger_VO_Death_02, Enemy_Dverger_VO_Death_03, Enemy_Dverger_VO_Death_04, Enemy_Dverger_VO_Death_05, Enemy_Dverger_VO_Death_06, Enemy_Dverger_VO_Death_07, Enemy_Dverger_VO_Death_08, Enemy_Dverger_VO_Death_09, Enemy_Dverger_VO_Death_10, Enemy_Dverger_VO_Death_11] pitch 1-1.1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_JotunWitch_Death | net | 5 ParticleSystem; TimedDestruction |
| fx_Queen_Death | net | ZSFX clips [HogMonster_DeathRoar5, HogMonster_DeathRoar6] pitch 0.9-1 vol 1-1; 18 ParticleSystem; TimedDestruction |
| fx_TickBloodHit | net | 5 ParticleSystem; TimedDestruction |
| fx_aspect_death | net | ZSFX clips [Enemy_FrozenKing_Spirit_Death_01, Enemy_FrozenKing_Spirit_Death_02, Enemy_FrozenKing_Spirit_Death_03] pitch 0.9-1.1 vol 1.5-1.5; 16 ParticleSystem; TimedDestruction |
| fx_babyseeker_death | net | ZSFX clips [Enemy_BabySeeker_Fx_Death_01, Enemy_BabySeeker_Fx_Death_02, Enemy_BabySeeker_Fx_Death_03, Enemy_BabySeeker_Fx_Death_04] pitch 0.9-1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_babyseeker_hurt | net | 6 ParticleSystem; TimedDestruction |
| fx_bat_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.2-1.3 vol 0.5-0.5; ZSFX clips [SmallCreature3_Hurt1, SmallCreature3_Hurt2] pitch 1-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_bjorn_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_bloodweapon_hit | net | 5 ParticleSystem; TimedDestruction |
| fx_charred_death | net | 7 ParticleSystem; TimedDestruction |
| fx_charred_summoned_death | net | 7 ParticleSystem; TimedDestruction |
| fx_chicken_death | net | ZSFX clips [Wildlife_Chick_Hurt_01, Wildlife_Chick_Hurt_02, Wildlife_Chick_Hurt_03, Wildlife_Chick_Hurt_04, Wildlife_Chick_Hurt_05, Wildlife_Chick_Hurt_06, Wildlife_Chick_Hurt_07, Wildlife_Chick_Hurt_08, Wildlife_Chick_Hurt_09, Wildlife_Chick_Hurt_10, Wildlife_Chick_Hurt_11, Wildlife_Chick_Hurt_12, Wildlife_Chick_Hurt_13] pitch 0.9-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_chicken_lay_egg | net | ZSFX clips [Grass Step 04, Grass Step_03] pitch 1.2-1.3 vol 1-1; 1 ParticleSystem; TimedDestruction |
| fx_deathsquito_hit | net | ZSFX clips [Blood_Splat_Gib02, Blood_Splat_Gib17] pitch 1-1.1 vol 0.8-0.8; 4 ParticleSystem; TimedDestruction |
| fx_deatsquito_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.1-1.2 vol 0.7-0.7; 12 ParticleSystem; TimedDestruction |
| fx_egg_splash | net | ZSFX clips [Enemy_Tick_Death_01, Enemy_Tick_Death_02, Enemy_Tick_Death_03, Enemy_Tick_Death_04, Enemy_Tick_Death_05] pitch 0.8-0.8 vol 1-1; 9 ParticleSystem; TimedDestruction |
| fx_fallenvalkyrie_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.2-1.3 vol 0.5-0.5; 7 ParticleSystem; TimedDestruction |
| fx_gjall_death | net | ZSFX clips [Enemy_Gjall_Death_FX_01, Enemy_Gjall_Death_FX_02, Enemy_Gjall_Death_FX_03] pitch 0.9-1 vol 1-1; 18 ParticleSystem; TimedDestruction |
| fx_gjall_egg_splat | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.9-1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_goblinking_death | net | ZSFX clips [Explo_Large_13] pitch 1-1 vol 1-1; ZSFX clips [Blood_Splat_Gib11] pitch 0.9-0.9 vol 1-1; 10 ParticleSystem; TimedDestruction |
| fx_hare_death | net | 5 ParticleSystem; TimedDestruction |
| fx_hen_death | net | ZSFX clips [seagull1, seagull4, seagull5, seagull6] pitch 0.9-1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_lox_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_loxcalf_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 1.5-2 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_moose_death | net | ZSFX clips [Enemy_Moose_DeathExplode_01, Enemy_Moose_DeathExplode_02, Enemy_Moose_DeathExplode_03, Enemy_Moose_DeathExplode_04, Enemy_Moose_DeathExplode_05] pitch 1-1 vol 0.3-0.45; 5 ParticleSystem; TimedDestruction |
| fx_morgen_death | net | 6 ParticleSystem; TimedDestruction |
| fx_seeker_death | net | ZSFX clips [Enemy_Seeker_FX_Death_01, Enemy_Seeker_FX_Death_02, Enemy_Seeker_FX_Death_03, Enemy_Seeker_FX_Death_04, Enemy_Seeker_FX_Death_05] pitch 0.9-1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_seeker_hurt | net | 6 ParticleSystem; TimedDestruction |
| fx_seeker_melee_hit | net | ZSFX clips [Enemy_Seeker_Attack_VO_01, Enemy_Seeker_Attack_VO_02, Enemy_Seeker_Attack_VO_03, Enemy_Seeker_Attack_VO_04, Enemy_Seeker_Attack_VO_05, Enemy_Seeker_Attack_VO_06, Enemy_Seeker_Attack_VO_07] pitch 0.7-0.8 vol 0.6-0.8; TimedDestruction |
| fx_seeker_spawn | net | ZSFX clips [Hit_SlimeSplat3, Hit_SlimeSplat4, Hit_WaterSplash_JumpOnBody] pitch 0.9-0.6 vol 1-1; 4 ParticleSystem; TimedDestruction |
| fx_seekerbrute_death | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.8-0.9 vol 1-1; 12 ParticleSystem; TimedDestruction |
| fx_tendril_death | net | ZSFX clips [Enemy_FrozenKing_Tendril_Death_01, Enemy_FrozenKing_Tendril_Death_02, Enemy_FrozenKing_Tendril_Death_03, Enemy_FrozenKing_Tendril_Death_04, Enemy_FrozenKing_Tendril_Death_05, Enemy_FrozenKing_Tendril_Death_06] pitch 0.85-1.2 vol 0.55-0.85; 8 ParticleSystem; TimedDestruction |
| fx_tentaroot_death | net | ZSFX clips [Blood_Splat_Gib11, Blood_Splat_Gib12, Blood_Splat_Gib13] pitch 1.1-1.2 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_tick_death | net | ZSFX clips [Enemy_Tick_Death_01, Enemy_Tick_Death_02, Enemy_Tick_Death_03, Enemy_Tick_Death_04, Enemy_Tick_Death_05] pitch 0.9-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_unbjorn_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| sfx_Bonemass_death | net | ZSFX clips [RockMonster_DeathRoar2] pitch 1-1 vol 1-1; ZSFX clips [TreeMonster_HurtRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_GoblinShaman_death | net | ZSFX clips [Male_LS_D_Laugh06, Male_LS_D_Laugh07, Male_LS_D_MadScream01, Male_LS_D_MadScream02, Male_LS_D_MadScream03, Male_LS_D_MadScream04] pitch 1-1.2 vol 1-1; TimedDestruction |
| sfx_asksvin_death | net | ZSFX clips [Enemy_Asksvin_Death_01, Enemy_Asksvin_Death_02, Enemy_Asksvin_Death_03, Enemy_Asksvin_Death_04, Enemy_Asksvin_Death_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_bear_death | net | ZSFX clips [Enemy_Bear_Death_01, Enemy_Bear_Death_02, Enemy_Bear_Death_03, Enemy_Bear_Death_04, Enemy_Bear_Death_05, Enemy_Bear_Death_06, Enemy_Bear_Death_07] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_blob_death | net | ZSFX clips [FishMonster_DeathRoar1, FishMonster_DeathRoar2, FishMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_bloblava_death | net | ZSFX clips [Enemy_LavaBlob_DeathBurst_01, Enemy_LavaBlob_DeathBurst_02, Enemy_LavaBlob_DeathBurst_03, Enemy_LavaBlob_DeathBurst_04, Enemy_LavaBlob_DeathBurst_05, Enemy_LavaBlob_DeathBurst_06, Enemy_LavaBlob_DeathBurst_07, Enemy_LavaBlob_DeathBurst_08] pitch 0.9-1.1 vol 2.2-2.4; TimedDestruction |
| sfx_boar_death | net | ZSFX clips [HogMonster_DeathRoar5, HogMonster_DeathRoar6] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_bonemaw_serpent_death | net | ZSFX clips [Enemy_Bonemaw_Death_01, Enemy_Bonemaw_Death_02, Enemy_Bonemaw_Death_03, Enemy_Bonemaw_Death_04] pitch 0.7-1.3 vol 1.6-1.7; TimedDestruction |
| sfx_charred_death | net | ZSFX clips [Enemy_CharredMelee_Death_01, Enemy_CharredMelee_Death_02, Enemy_CharredMelee_Death_03, Enemy_CharredMelee_Death_04, Enemy_CharredMelee_Death_05, Enemy_CharredMelee_Death_06, Enemy_CharredMelee_Death_07, Enemy_CharredMelee_Death_08, Enemy_CharredMelee_Death_09, Enemy_CharredMelee_Death_10] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_charred_twitcher_death | net | ZSFX clips [Enemy_Twitcher_Death_01, Enemy_Twitcher_Death_02, Enemy_Twitcher_Death_03, Enemy_Twitcher_Death_04, Enemy_Twitcher_Death_05] pitch 1.2-0.8 vol 0.8-1; TimedDestruction |
| sfx_chicken_death | net | ZSFX clips [Wildlife_Chicken_Death_01, Wildlife_Chicken_Death_02, Wildlife_Chicken_Death_03, Wildlife_Chicken_Death_04, Wildlife_Chicken_Death_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_crow_death | net | ZSFX clips [crow_splatt] pitch 1.1-0.9 vol 0.8-0.8; TimedDestruction |
| sfx_deathsquito_attack | net | ZSFX clips [sScorpionTailAttack01, sScorpionTailAttack02] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_deer_death | net | ZSFX clips [Rhinosaur_HurtGrowl1, Rhinosaur_HurtGrowl2, Rhinosaur_HurtGrowl3] pitch 1.6-1.8 vol 1-1; TimedDestruction |
| sfx_dragon_death | net | ZSFX clips [Dragon_DeathRoar4] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_dragonegg_destroy | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib10] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_draugr_death | net | ZSFX clips [Zombie09_Death1, Zombie09_Death2, Zombie09_Death3, Zombie09_Death4, Zombie09_Death5] pitch 1-1.1 vol 0.7-0.7; TimedDestruction |
| sfx_dverger_vo_death | net | ZSFX clips [Enemy_Dverger_VO_Death_01, Enemy_Dverger_VO_Death_02, Enemy_Dverger_VO_Death_03, Enemy_Dverger_VO_Death_04, Enemy_Dverger_VO_Death_05, Enemy_Dverger_VO_Death_06, Enemy_Dverger_VO_Death_07, Enemy_Dverger_VO_Death_08, Enemy_Dverger_VO_Death_09, Enemy_Dverger_VO_Death_10, Enemy_Dverger_VO_Death_11] pitch 1-1.1 vol 0.9-1; TimedDestruction |
| sfx_eikthyr_death | net | ZSFX clips [Rhinosaur_DeathGrowl2, Rhinosaur_DeathGrowl1] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_elaking_death | net | ZSFX clips [Enemy_Elaking_Death_01, Enemy_Elaking_Death_02, Enemy_Elaking_Death_03, Enemy_Elaking_Death_04] pitch 1-1 vol 0.25-0.35; TimedDestruction |
| sfx_elaking_death_old | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 2-2.6 vol 1-1; TimedDestruction |
| sfx_fader_death_explosion | net | ZSFX clips [Enemy_Father_Death_Explosion] pitch 1-1 vol 6-2; TimedDestruction |
| sfx_fader_death_start | net | ZSFX clips [Enemy_Father_Death_Start] pitch 1-1 vol 1.4-1.6; TimedDestruction |
| sfx_fallenvalkyrie_death | net | ZSFX clips [Enemy_FallenValkyrie_Death_01, Enemy_FallenValkyrie_Death_02, Enemy_FallenValkyrie_Death_03, Enemy_FallenValkyrie_Death_04, Enemy_FallenValkyrie_Death_05, Enemy_FallenValkyrie_Death_06, Enemy_FallenValkyrie_Death_07, Enemy_FallenValkyrie_Death_08] pitch 1-1 vol 0.8-1; TimedDestruction |
| sfx_fallenwarrior_death | net | ZSFX clips [Enemy_FallenWarrior_Death_01, Enemy_FallenWarrior_Death_02, Enemy_FallenWarrior_Death_03, Enemy_FallenWarrior_Death_04, Enemy_FallenWarrior_Death_05] pitch 0.9-1.05 vol 0.3-0.45; TimedDestruction |
| sfx_fenring_death | net | ZSFX clips [Zombie09_Death4, Zombie09_Death5] pitch 1-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_frozenking_death_crystal_break | net | ZSFX clips [Enemy_FrozenKing_Death_Crystal_Break_01] pitch 1-1 vol 0.85-0.85; TimedDestruction |
| sfx_frozenking_death_crystal_sequence | net | ZSFX clips [Enemy_FrozenKing_Death_Crystal_Sequence_01] pitch 1-1 vol 0.85-0.85; TimedDestruction |
| sfx_frozenking_death_p3_sequence | net | ZSFX clips [Enemy_frozenKing_p3_Death] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_frozenking_spirit_death | net | ZSFX clips [Enemy_FrozenKing_Spirit_Death_01, Enemy_FrozenKing_Spirit_Death_02, Enemy_FrozenKing_Spirit_Death_03] pitch 0.9-1.1 vol 1.5-1.5; TimedDestruction |
| sfx_frozenking_tendril_death | net | ZSFX clips [Enemy_FrozenKing_Tendril_Death_01, Enemy_FrozenKing_Tendril_Death_02, Enemy_FrozenKing_Tendril_Death_03, Enemy_FrozenKing_Tendril_Death_04, Enemy_FrozenKing_Tendril_Death_05, Enemy_FrozenKing_Tendril_Death_06] pitch 0.85-1.2 vol 0.55-0.85; TimedDestruction |
| sfx_gameltroll_death | net | ZSFX clips [Enemy_Gameltroll_Death_01, Enemy_Gameltroll_Death_02, Enemy_Gameltroll_Death_03, Enemy_Gameltroll_Death_04, Enemy_Gameltroll_Death_05] pitch 0.95-1.05 vol 0.35-0.5; TimedDestruction |
| sfx_gameltroll_stoneturn_death | net | ZSFX clips [Enemy_Gameltroll_Death_StoneTurn_01, Enemy_Gameltroll_Death_StoneTurn_02, Enemy_Gameltroll_Death_StoneTurn_03, Enemy_Gameltroll_Death_StoneTurn_04, Enemy_Gameltroll_Death_StoneTurn_05] pitch 0.92-1.05 vol 0.45-0.55; TimedDestruction |
| sfx_gdking_death | net | ZSFX clips [TreeMonster_DeathRoar2] pitch 0.9-1.1 vol 1-1; ZSFX clips [TreeMonster_HurtRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_ghost_death | net | ZSFX clips [EvilGhost_SpecterDeath2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_ghosts_death | net | ZSFX clips [Enemy_Ghost_Death_01, Enemy_Ghost_Death_02, Enemy_Ghost_Death_03, Enemy_Ghost_Death_04, Enemy_Ghost_Death_05, Enemy_Ghost_Death_06] pitch 1-1 vol 0.4-0.5; TimedDestruction |
| sfx_gjall_attack_tick_drop | net | ZSFX clips [Enemy_Gjall_Attack_TickDrop_01, Enemy_Gjall_Attack_TickDrop_02, Enemy_Gjall_Attack_TickDrop_03] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_gjall_death | net | ZSFX clips [Enemy_Gjall_Death_FX_01, Enemy_Gjall_Death_FX_02, Enemy_Gjall_Death_FX_03] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_gjall_tick_drop | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_goblin_death | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_goblinbrute_death | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 0.5-0.6 vol 1-1; TimedDestruction |
| sfx_greydwarf_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_greydwarf_deepnorth_death | net | ZSFX clips [Enemy_Greydwarf_DeepNorth_Death_01, Enemy_Greydwarf_DeepNorth_Death_02, Enemy_Greydwarf_DeepNorth_Death_03] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_greydwarf_elite_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_greyling_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 1.8-2 vol 1-1; TimedDestruction |
| sfx_hare_death_vocal | net | ZSFX clips [Hare_Death_Vocal_01, Hare_Death_Vocal_02, Hare_Death_Vocal_03, Hare_Death_Vocal_04, Hare_Death_Vocal_05] pitch 0.95-1 vol 0.8-1; TimedDestruction |
| sfx_hatchling_death | net | ZSFX clips [Dragon_DeathRoar1] pitch 0.9-1 vol 1-1; ZSFX clips [Dragon_FlameBurst] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_imp_death | net | ZSFX clips [GiantAnt_Death1, GiantAnt_Death2, GiantAnt_Death3] pitch 0.9-1.1 vol 1-1; ZSFX clips [Dragon_BreathFire1, Dragon_BreathFire2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_jotunwarrior_death | net | ZSFX clips [Enemy_JotunWarrior_Death_01, Enemy_JotunWarrior_Death_02, Enemy_JotunWarrior_Death_03, Enemy_JotunWarrior_Death_04, Enemy_JotunWarrior_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_jotunwitch_death | net | ZSFX clips [Enemy_JotunWitch_Death_01, Enemy_JotunWitch_Death_02, Enemy_JotunWitch_Death_03, Enemy_JotunWitch_Death_04, Enemy_JotunWitch_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_kvastur_death | net | ZSFX clips [EvilGhost_SpecterDeath2] pitch 2-3 vol 1-1; ZSFX clips [Dragon_BreathFire1, Dragon_BreathFire2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_leech_death | net | ZSFX clips [Blood_Splat_Gib05, Blood_Splat_Gib06, Blood_Splat_Gib07] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_moleman_death | net | ZSFX clips [Enemy_Moleman_Death_01, Enemy_Moleman_Death_02, Enemy_Moleman_Death_03, Enemy_Moleman_Death_04, Enemy_Moleman_Death_05] pitch 1-1 vol 0.5-0.65; TimedDestruction |
| sfx_moose_death | net | ZSFX clips [Enemy_Moose_Death_01, Enemy_Moose_Death_02, Enemy_Moose_Death_03, Enemy_Moose_Death_04, Enemy_Moose_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_moose_death_explode | net | ZSFX clips [Enemy_Moose_DeathExplode_01, Enemy_Moose_DeathExplode_02, Enemy_Moose_DeathExplode_03, Enemy_Moose_DeathExplode_04, Enemy_Moose_DeathExplode_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_morgen_death | net | ZSFX clips [Enemy_Morgen_Death_01, Enemy_Morgen_Death_02, Enemy_Morgen_Death_03, Enemy_Morgen_Death_04, Enemy_Morgen_Death_05] pitch 1-1 vol 1.4-1.6; TimedDestruction |
| sfx_neck_death | net | ZSFX clips [Hit_LargeInsectSplat1, Hit_LargeInsectSplat2] pitch 0.7-0.8 vol 0.7-0.7; TimedDestruction |
| sfx_seal_death | net | ZSFX clips [Enemy_Seal_Death_01, Enemy_Seal_Death_02, Enemy_Seal_Death_03, Enemy_Seal_Death_04, Enemy_Seal_Death_05, Enemy_Seal_Death_07, Enemy_Seal_Death_06] pitch 1-1 vol 0.45-0.55; TimedDestruction |
| sfx_serpent_death | net | ZSFX clips [Blood_Splat_Gib05, Blood_Splat_Gib06, Blood_Splat_Gib07] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_skeleton_basic_death | net | ZSFX clips [Enemy_Skeleton_Basic_Death_01, Enemy_Skeleton_Basic_Death_02, Enemy_Skeleton_Basic_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_big_death | net | ZSFX clips [Skeleton_Death_BoneShatter1, Skeleton_Hit_Shatter2, Skeleton_Hit_Shatter1] pitch 0.9-1.1 vol 1-1; ZSFX clips [Zombie08_Death1] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_skeleton_death | net | ZSFX clips [Skeleton_Death_BoneShatter1, Skeleton_Hit_Shatter2, Skeleton_Hit_Shatter1] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_skeleton_frozen_death | net | ZSFX clips [Enemy_Skeleton_DeepNorth_Death_01, Enemy_Skeleton_DeepNorth_Death_02, Enemy_Skeleton_DeepNorth_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_hildir_death | net | ZSFX clips [Enemy_Skeleton_Hildir_Death_01, Enemy_Skeleton_Hildir_Death_02, Enemy_Skeleton_Hildir_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_poison_death | net | ZSFX clips [Enemy_Skeleton_Poison_Death_01, Enemy_Skeleton_Poison_Death_02, Enemy_Skeleton_Poison_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_swamp_death | net | ZSFX clips [Enemy_Skeleton_Swamp_Death_01, Enemy_Skeleton_Swamp_Death_02, Enemy_Skeleton_Swamp_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_staffthunderblood_cast | net | ZSFX clips [Weapons_StaffThunderBlood_Cast_01, Weapons_StaffThunderBlood_Cast_02, Weapons_StaffThunderBlood_Cast_03, Weapons_StaffThunderBlood_Cast_04] pitch 0.85-1.15 vol 1.2-1.45; TimedDestruction |
| sfx_staffthunderblood_thunder | net | ZSFX clips [Weapons_StaffThunderBlood_Thunder_01, Weapons_StaffThunderBlood_Thunder_02, Weapons_StaffThunderBlood_Thunder_03, Weapons_StaffThunderBlood_Thunder_04] pitch 0.85-1.15 vol 1.1-1.3; TimedDestruction |
| sfx_stonegolem_death | net | ZSFX clips [Smash_HugeRockImpact3] pitch 0.9-1.1 vol 1-1; ZSFX clips [EvilMonster_Breath] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_tick_alerted | net | ZSFX clips [Enemy_Tick_Alerted_01, Enemy_Tick_Alerted_02, Enemy_Tick_Alerted_03, Enemy_Tick_Alerted_04, Enemy_Tick_Alerted_05, Enemy_Tick_Alerted_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_attack_drain | net | ZSFX clips [Enemy_Tick_Drain_01, Enemy_Tick_Drain_02, Enemy_Tick_Drain_03, Enemy_Tick_Drain_04, Enemy_Tick_Drain_05, Enemy_Tick_Drain_06, Enemy_Tick_Drain_07, Enemy_Tick_Drain_08] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_attack_jump | net | ZSFX clips [Enemy_Tick_DrainAttack_01, Enemy_Tick_DrainAttack_02, Enemy_Tick_DrainAttack_03, Enemy_Tick_DrainAttack_04, Enemy_Tick_DrainAttack_05, Enemy_Tick_DrainAttack_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_hurt | net | ZSFX clips [Enemy_Tick_Hurt_01, Enemy_Tick_Hurt_02, Enemy_Tick_Hurt_03, Enemy_Tick_Hurt_04, Enemy_Tick_Hurt_05, Enemy_Tick_Hurt_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_idle | net | ZSFX clips [Enemy_Tick_Idle_01, Enemy_Tick_Idle_02, Enemy_Tick_Idle_03, Enemy_Tick_Idle_04, Enemy_Tick_Idle_05, Enemy_Tick_Idle_06, Enemy_Tick_Idle_07, Enemy_Tick_Idle_08, Enemy_Tick_Idle_09, Enemy_Tick_Idle_10] pitch 0.9-1.1 vol 0.5-0.8; TimedDestruction |
| sfx_troll_death | net | ZSFX clips [RockMonster_DeathRoar1] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_trollfire_death | net | ZSFX clips [Enemy_Firetroll_Death_01, Enemy_Firetroll_Death_02, Enemy_Firetroll_Death_03] pitch 0.9-1.1 vol 1.6-1.6; TimedDestruction |
| sfx_ulv_death | net | ZSFX clips [Zombie09_Death4, Zombie09_Death5] pitch 0.9-1 vol 0.6-0.6; TimedDestruction |
| sfx_unbjorn_death | net | ZSFX clips [Enemy_Unbjorn_Death_01, Enemy_Unbjorn_Death_02, Enemy_Unbjorn_Death_03, Enemy_Unbjorn_Death_04, Enemy_Unbjorn_Death_05, Enemy_Unbjorn_Death_06] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_vulture_death | net | ZSFX clips [Enemy_Vulture_Death_01, Enemy_Vulture_Death_02, Enemy_Vulture_Death_03, Enemy_Vulture_Death_04] pitch 0.9-1.1 vol 0.9-1; TimedDestruction |
| sfx_wolf_death | net | ZSFX clips [ZombieDog_Death1, ZombieDog_Death2] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_wraith_death | net | ZSFX clips [EvilGhost_Death3] pitch 1-1.1 vol 1-1; ZSFX clips [FlameThrower_ShotBurst2, FlameThrower_ShotBurst3, FlameThrower_ShotBurst4, FlameThrower_ShotBurst5] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_writhan_death_charge | net | ZSFX clips [Enemy_Writhan_Death_Charge_01, Enemy_Writhan_Death_Charge_02, Enemy_Writhan_Death_Charge_03] pitch 1-1 vol 0.6-0.8; TimedDestruction |
| sfx_writhan_death_explosion | net | ZSFX clips [Enemy_Writhan_Death_Explosion_01, Enemy_Writhan_Death_Explosion_02, Enemy_Writhan_Death_Explosion_03] pitch 0.95-1.05 vol 0.75-1; TimedDestruction |
| sfx_writhan_verse_death | net | ZSFX clips [Enemy_Writhan_Verse_Death_01, Enemy_Writhan_Verse_Death_02, Enemy_Writhan_Verse_Death_03, Enemy_Writhan_Verse_Death_04] pitch 1-1 vol 0.6-0.8; TimedDestruction |
| vfx_BloodDeath | net | 5 ParticleSystem; TimedDestruction |
| vfx_BloodHit | net | 5 ParticleSystem; TimedDestruction |
| vfx_BonemassDeath | net | 8 ParticleSystem; TimedDestruction |
| vfx_BugRepellent | net | 4 ParticleSystem |
| vfx_FallenWarrior_death | net | 2 ParticleSystem; TimedDestruction |
| vfx_ImpDeath | net | 3 ParticleSystem; TimedDestruction |
| vfx_ShadowPerson_death | net | 2 ParticleSystem; TimedDestruction |
| vfx_TrollFrost_Death | net | 2 ParticleSystem; TimedDestruction |
| vfx_ashlandsbow_blood_fire | net | 2 ParticleSystem; TimedDestruction |
| vfx_blob_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_blob_frost_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_blobmork_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_blobtar_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_boar_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_bonemawserpent_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_crossbow_blood_fire | net | 3 ParticleSystem; TimedDestruction |
| vfx_crow_death | net | 3 ParticleSystem; TimedDestruction |
| vfx_deer_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_dragon_death | net | ZSFX clips [FlameThrower_ShotBurst2, FlameThrower_ShotBurst3, FlameThrower_ShotBurst4, FlameThrower_ShotBurst5] pitch 0.9-1 vol 0.8-0.8; 11 ParticleSystem; TimedDestruction |
| vfx_dragonegg_destroy | net | 5 ParticleSystem; TimedDestruction |
| vfx_draugr_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_eikthyr_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_fenring_cultist_death | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| vfx_fenring_cultist_hildir_death | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| vfx_fenring_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_firetreecut_snow_FrostTrollDeath | net | 3 ParticleSystem; TimedDestruction |
| vfx_frozenking_death | net | ZSFX clips [Wind_RushingStormLoop] pitch 1-1 vol 1-1; 13 ParticleSystem; TimedDestruction |
| vfx_frozenking_final_death | net | ZSFX clips [Wind_RushingStormLoop] pitch 1-1 vol 1-1; 51 ParticleSystem; TimedDestruction |
| vfx_frozenking_final_death_ground | net | 8 ParticleSystem; TimedDestruction |
| vfx_ghost_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_goblin_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblin_dn_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblinbrute_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblinbrute_hildir_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_greydwarf_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_greydwarf_elite_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_hatchling_death | net | 13 ParticleSystem; TimedDestruction |
| vfx_leech_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_neck_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_player_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_seagull_death | net | 3 ParticleSystem; TimedDestruction |
| vfx_seekerbrute_groundslam | net | 6 ParticleSystem; TimedDestruction |
| vfx_serpent_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_skeleton_big_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_skeleton_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_skeleton_fire_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_stonegolem_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_troll_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_troll_summoned_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_ulv_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_wolf_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_wraith_death | net | 10 ParticleSystem; TimedDestruction |

<details><summary>All sfx_* (791)</summary>

sfx_Abomination_Attack2_slam_whoosh, sfx_Abomination_attack, sfx_Abomination_swing, sfx_Bonemass_Hit, sfx_Bonemass_alert, sfx_Bonemass_death, sfx_Bonemass_idle, sfx_DraugrSpawn, sfx_FireAddFuel, sfx_Frost_Start, sfx_GoblinShaman_alerted, sfx_GoblinShaman_death, sfx_GoblinShaman_fireball_launch, sfx_GoblinShaman_hurt, sfx_GoblinShaman_idle, sfx_GuckSackDestroyed, sfx_GuckSackHit, sfx_HiveQueen_acitspit, sfx_HiveQueen_alerted, sfx_HiveQueen_backslam, sfx_HiveQueen_bite, sfx_HiveQueen_burrow, sfx_HiveQueen_callout, sfx_HiveQueen_doubleslash, sfx_HiveQueen_idle, sfx_HiveQueen_move, sfx_HiveQueen_pierce, sfx_HiveQueen_rush, sfx_HiveQueen_slash, sfx_HiveQueen_slashcombo, sfx_HiveQueen_spitimpact, sfx_HiveQueen_turn, sfx_MeadBurp, sfx_MudDestroyed, sfx_MudHit, sfx_OpenPortal, sfx_Poison_Start, sfx_Potion_eitr_minor, sfx_Potion_fireresist_Start, sfx_Potion_frostresist_Start, sfx_Potion_health_Start, sfx_Potion_health_large, sfx_Potion_health_medium, sfx_Potion_health_minor, sfx_Potion_stamina_Start, sfx_Potion_stamina_Start_lingering, sfx_Potion_stamina_Start_medium, sfx_ProjectileHit, sfx_Puke_female, sfx_Puke_male, sfx_StaffLightning_charge, sfx_StaffLightning_fire, sfx_UndeadBurn_Start, sfx_WishbonePing_closer, sfx_WishbonePing_far, sfx_WishbonePing_further, sfx_WishbonePing_med, sfx_WishbonePing_near, sfx_abomination_arise_end, sfx_arbalest_fire, sfx_archery_target_hit, sfx_arrow_hit, sfx_asksvin_alert, sfx_asksvin_bite, sfx_asksvin_death, sfx_asksvin_footstep, sfx_asksvin_idle, sfx_asksvin_pounce, sfx_atgeir_attack, sfx_atgeir_attack_secondary, sfx_axe_flint_hit, sfx_axe_hit, sfx_axe_swing, sfx_babyseal_idle, sfx_barley_hit, sfx_barnacle_destroyed, sfx_bat_alerted, sfx_bat_attack, sfx_bat_idle, sfx_battering_ram_anticipation, sfx_battleaxe_hit, sfx_battleaxe_swing_start, sfx_battleaxe_swing_wosh, sfx_bear_bite_attack, sfx_bear_bite_attack_impact, sfx_bear_claw_attack, sfx_bear_claw_attack slash, sfx_bear_death, sfx_bear_footstep, sfx_bear_hurt, sfx_bear_idle, sfx_beehive_destroyed, sfx_beehive_hit, sfx_blobLava_explosion, sfx_blob_alerted, sfx_blob_attack, sfx_blob_death, sfx_blob_hit, sfx_blob_idle, sfx_blob_jump, sfx_bloblava_alert, sfx_bloblava_death, sfx_bloblava_grow, sfx_bloblava_idle, sfx_bloblava_jump, sfx_blobtar_attack_spit, sfx_blobtar_idle, sfx_boar_alerted, sfx_boar_attack, sfx_boar_birth, sfx_boar_death, sfx_boar_hit, sfx_boar_idle, sfx_boar_love, sfx_bogwitch_creak, sfx_bogwitch_greet, sfx_bogwitch_random, sfx_bogwitch_trade, sfx_bomb_throw, sfx_bombdynamite_drop, sfx_bombdynamite_explosion, sfx_bomblava_crumble, sfx_bomblava_fail, sfx_bomblava_rocks, sfx_bonemaw_serpent_alert, sfx_bonemaw_serpent_bite, sfx_bonemaw_serpent_death, sfx_bonemaw_serpent_spit, sfx_bonemaw_serpent_spit_hit, sfx_bonepile_destroyed, sfx_bones_pick, sfx_bow_draw, sfx_bow_fire, sfx_bow_fire_silent, sfx_bowl_AddItem, sfx_branch_break, sfx_branch_hit, sfx_build_cultivator, sfx_build_hammer_crystal, sfx_build_hammer_default, sfx_build_hammer_ice, sfx_build_hammer_metal, sfx_build_hammer_stone, sfx_build_hammer_wood, sfx_build_hoe, sfx_bush_hit, sfx_carrion_destroyed, sfx_cart_hit, sfx_catapult_ammo_load, sfx_catapult_charge, sfx_catapult_legs_down, sfx_catapult_legs_up, sfx_catapult_place_item, sfx_catapult_shoot, sfx_charred_alert, sfx_charred_archer_bow_draw, sfx_charred_archer_bow_release, sfx_charred_archer_volley_draw, sfx_charred_archer_volley_release, sfx_charred_death, sfx_charred_hurt, sfx_charred_idle, sfx_charred_mage_alert, sfx_charred_mage_attack_charge, sfx_charred_mage_attack_shoot, sfx_charred_mage_spawn_staff, sfx_charred_melee_attack_lift, sfx_charred_melee_attack_stab, sfx_charred_melee_attack_swing, sfx_charred_spawner_destroy, sfx_charred_twitcher_alert, sfx_charred_twitcher_attack, sfx_charred_twitcher_death, sfx_charred_twitcher_hurt, sfx_charred_twitcher_idle, sfx_charred_twitcher_summoned_alert, sfx_charred_twitcher_throw, sfx_charred_twitcher_throw_impact, sfx_chest_close, sfx_chest_open, sfx_chick_hurt, sfx_chicken_death, sfx_chicken_eat, sfx_chicken_footstep, sfx_chicken_hurt, sfx_chicken_idle_vocal, sfx_chicken_idle_wingflap, sfx_cinder_rain_hit, sfx_claw_swing, sfx_clay_pot_break, sfx_club_hit, sfx_club_swing, sfx_coins_destroyed, sfx_coins_pile_destroyed, sfx_coins_pile_placed, sfx_coins_placed, sfx_cooking_station_burnt, sfx_cooking_station_done, sfx_cooking_station_take, sfx_creature_consume, sfx_crow_death, sfx_deathsquito_attack, sfx_deepnorth_gate_activate, sfx_deepnorth_gate_close_loop, sfx_deepnorth_gate_open_loop, sfx_deer_alerted, sfx_deer_death, sfx_deer_idle, sfx_demister_start, sfx_dodge, sfx_dragon_alerted, sfx_dragon_coldball_explode, sfx_dragon_coldball_launch, sfx_dragon_coldball_start, sfx_dragon_coldbreath_start, sfx_dragon_coldbreath_trailon, sfx_dragon_death, sfx_dragon_hurt, sfx_dragon_idle, sfx_dragon_melee_hit, sfx_dragon_melee_start, sfx_dragon_scream, sfx_dragonegg_destroy, sfx_draugr_alerted, sfx_draugr_death, sfx_draugr_hit, sfx_draugr_idle, sfx_draugrpile_destroyed, sfx_draugrpile_hit, sfx_drink, sfx_dverger_ball_start, sfx_dverger_fireball_rain_shot, sfx_dverger_fireball_rain_start, sfx_dverger_footsteps, sfx_dverger_heal_finish, sfx_dverger_heal_start, sfx_dverger_heavyattack_launch, sfx_dverger_ice_aoe_start, sfx_dverger_ice_projectile_start, sfx_dverger_staff_baseattack_foley, sfx_dverger_staff_kolvattack_foley, sfx_dverger_staff_poke_foley, sfx_dverger_vo_alerted, sfx_dverger_vo_attack, sfx_dverger_vo_death, sfx_dverger_vo_hurt, sfx_dverger_vo_idle, sfx_eat, sfx_eikthyr_alert, sfx_eikthyr_death, sfx_eikthyr_hit, sfx_eikthyr_idle, sfx_elaking_alerted, sfx_elaking_alerted_old, sfx_elaking_attack_claw, sfx_elaking_attack_claw_impact, sfx_elaking_attack_jump, sfx_elaking_attack_jump_impact, sfx_elaking_death, sfx_elaking_death_old, sfx_elaking_hit, sfx_elaking_hit_old, sfx_elaking_idle, sfx_elaking_idle_old, sfx_equip, sfx_equip_start, sfx_equip_vibration_only, sfx_fader_bell, sfx_fader_bite_pre, sfx_fader_bite_snarl, sfx_fader_claw_pre, sfx_fader_claw_swipe, sfx_fader_death_explosion, sfx_fader_death_start, sfx_fader_firebreath_in, sfx_fader_firebreath_out, sfx_fader_firewall_fireburst, sfx_fader_firewall_start, sfx_fader_fissure, sfx_fader_fissure_footimpact, sfx_fader_fissure_footslide, sfx_fader_idle, sfx_fader_spawn_meteor_ambience, sfx_fader_spawn_meteor_arrival, sfx_fader_spawn_meteor_impact, sfx_fader_spin, sfx_fader_taunt, sfx_fallenvalkyrie_alert, sfx_fallenvalkyrie_attack, sfx_fallenvalkyrie_attack_claw, sfx_fallenvalkyrie_attack_spin_charge, sfx_fallenvalkyrie_attack_spin_release, sfx_fallenvalkyrie_attack_spit_charge, sfx_fallenvalkyrie_attack_spit_projectile, sfx_fallenvalkyrie_attack_spit_projectile_impact, sfx_fallenvalkyrie_attack_spit_vocal, sfx_fallenvalkyrie_death, sfx_fallenvalkyrie_idle, sfx_fallenvalkyrie_screech, sfx_fallenvalkyrie_taunt, sfx_fallenvalkyrie_wingflap, sfx_fallenwarrior_attack, sfx_fallenwarrior_attack_impact, sfx_fallenwarrior_death, sfx_fallenwarrior_hurt, sfx_fallenwarrior_idle, sfx_feast_destroyed, sfx_fenring_alerted, sfx_fenring_claw_hit, sfx_fenring_claw_start, sfx_fenring_claw_trailstart, sfx_fenring_death, sfx_fenring_fireclaw, sfx_fenring_howl, sfx_fenring_idle, sfx_fenring_jump_start, sfx_fenring_jump_trailstart, sfx_fenring_jump_trigger, sfx_fermenter_add, sfx_fermenter_tap, sfx_firestaff_launch, sfx_firework_explode, sfx_fishingrod_linebreak, sfx_fishingrod_swing, sfx_fist_metal_blocked, sfx_frostfoundry_activate, sfx_frostfoundry_deactivate, sfx_frozenking_attackmelee_double_chain, sfx_frozenking_attackmelee_double_impact, sfx_frozenking_attackmelee_double_whoosh_follow, sfx_frozenking_attackmelee_double_whoosh_lead, sfx_frozenking_attackmelee_single_chainback, sfx_frozenking_attackmelee_single_charge, sfx_frozenking_attackmelee_single_impact, sfx_frozenking_attackmelee_single_whsh, sfx_frozenking_chainflurry_chain, sfx_frozenking_chainflurry_coldvortex, sfx_frozenking_chainflurry_fly, sfx_frozenking_chainflurry_start, sfx_frozenking_charge_chainback, sfx_frozenking_charge_doublehookup, sfx_frozenking_charge_start, sfx_frozenking_charge_whoosh, sfx_frozenking_death_crystal_break, sfx_frozenking_death_crystal_sequence, sfx_frozenking_death_p3_sequence, sfx_frozenking_doubleslam_explosion, sfx_frozenking_frozenspark_whip, sfx_frozenking_frozentwirl_charge, sfx_frozenking_frozentwirl_coldvortex, sfx_frozenking_frozentwirl_tornado, sfx_frozenking_frozentwirl_wind, sfx_frozenking_idle_breath, sfx_frozenking_idle_chained, sfx_frozenking_intro_chainpressure, sfx_frozenking_intro_icepillar_left_break, sfx_frozenking_intro_icepillar_right_break, sfx_frozenking_intro_rise, sfx_frozenking_punchaoe_chain_end, sfx_frozenking_punchaoe_chain_start, sfx_frozenking_punchaoe_debris, sfx_frozenking_punchaoe_punch_first, sfx_frozenking_punchaoe_punch_second, sfx_frozenking_punchaoe_punch_third, sfx_frozenking_punchaoe_whoosh_punch, sfx_frozenking_punchaoe_whoosh_start, sfx_frozenking_spikerain_chain, sfx_frozenking_spikerain_explosion, sfx_frozenking_spikerain_flyby, sfx_frozenking_spikerain_iceceiling, sfx_frozenking_spikerain_shards, sfx_frozenking_spirit_death, sfx_frozenking_spirit_summon, sfx_frozenking_tendril_attack, sfx_frozenking_tendril_death, sfx_frozenking_tendril_grow, sfx_frozenking_tendril_summon, sfx_frozenking_turn, sfx_frozenking_voice_attack, sfx_frozenking_voice_scream, sfx_gameltroll_death, sfx_gameltroll_hurt, sfx_gameltroll_idle, sfx_gameltroll_melee_attack, sfx_gameltroll_melee_attack_Impact, sfx_gameltroll_stomp_attack, sfx_gameltroll_stomp_attack_impact, sfx_gameltroll_stoneturn_death, sfx_gameltroll_throw_attack, sfx_gameltroll_throw_attack_impact, sfx_gdking_alert, sfx_gdking_death, sfx_gdking_idle, sfx_gdking_punch, sfx_gdking_rock_destroyed, sfx_gdking_scream, sfx_gdking_shoot_start, sfx_gdking_shoot_trigger, sfx_gdking_spawn, sfx_gdking_stomp, sfx_ghost_alert, sfx_ghost_attack, sfx_ghost_attack_hit, sfx_ghost_death, sfx_ghost_hurt, sfx_ghost_idle, sfx_ghosts_death, sfx_ghosts_footstep, sfx_ghosts_hurt, sfx_ghosts_idle, sfx_ghosts_melee_attack, sfx_ghosts_melee_attack_impact, sfx_gjall_alerted, sfx_gjall_attack_shake, sfx_gjall_attack_tick_drop, sfx_gjall_death, sfx_gjall_idle_blowout, sfx_gjall_idle_shiver, sfx_gjall_idle_vocals, sfx_gjall_spit, sfx_gjall_spit_fx, sfx_gjall_spit_impact, sfx_gjall_tick_drop, sfx_glowworm_pickup, sfx_goblin_alerted, sfx_goblin_death, sfx_goblin_hit, sfx_goblin_idle, sfx_goblinbrute_alerted, sfx_goblinbrute_clubhit, sfx_goblinbrute_clubswing, sfx_goblinbrute_death, sfx_goblinbrute_hit, sfx_goblinking_beam, sfx_goblinking_taunt, sfx_grapplinghook_detach, sfx_grapplinghook_fire, sfx_grapplinghook_hit, sfx_grapplinghook_reload, sfx_grapplinghook_repel, sfx_greydwarf_alerted, sfx_greydwarf_attack, sfx_greydwarf_attack_hit, sfx_greydwarf_death, sfx_greydwarf_deepnorth_death, sfx_greydwarf_deepnorth_verse_attack, sfx_greydwarf_elite_alerted, sfx_greydwarf_elite_attack, sfx_greydwarf_elite_death, sfx_greydwarf_elite_idle, sfx_greydwarf_hit, sfx_greydwarf_idle, sfx_greydwarf_shaman_attack, sfx_greydwarf_shaman_heal, sfx_greydwarf_stone_hit, sfx_greydwarf_throw, sfx_greydwarfnest_destroyed, sfx_greydwarfnest_hit, sfx_greyling_alerted, sfx_greyling_attack, sfx_greyling_death, sfx_greyling_hit, sfx_greyling_idle, sfx_gui_craftitem, sfx_gui_craftitem_cauldron, sfx_gui_craftitem_cauldron_end, sfx_gui_craftitem_end, sfx_gui_craftitem_forge, sfx_gui_craftitem_forge_end, sfx_gui_craftitem_workbench, sfx_gui_craftitem_workbench_end, sfx_gui_repairitem_forge, sfx_gui_repairitem_workbench, sfx_haldor_greet, sfx_haldor_laugh, sfx_haldor_yea, sfx_hare_alerted, sfx_hare_death_vocal, sfx_hare_idle, sfx_hare_idle_eating, sfx_hatchling_alerted, sfx_hatchling_coldball_explode, sfx_hatchling_coldball_launch, sfx_hatchling_coldball_start, sfx_hatchling_death, sfx_hatchling_idle, sfx_hatcling_hurt, sfx_hit, sfx_hit_vibration_only, sfx_ice_destroyed, sfx_ice_hit, sfx_icestaff_start, sfx_imp_alerted, sfx_imp_death, sfx_imp_fireball_explode, sfx_imp_fireball_launch, sfx_imp_hit, sfx_jotunwarrior_Idle, sfx_jotunwarrior_attack1, sfx_jotunwarrior_attack2, sfx_jotunwarrior_death, sfx_jotunwarrior_hurt, sfx_jotunwarrior_impact_flesh, sfx_jotunwarrior_impact_ground, sfx_jotunwitch_Idle, sfx_jotunwitch_attackball, sfx_jotunwitch_attackball_impact, sfx_jotunwitch_attackblast, sfx_jotunwitch_attackbuildup, sfx_jotunwitch_death, sfx_jotunwitch_dodge, sfx_jotunwitch_flight, sfx_jotunwitch_hurt, sfx_jump, sfx_jump_vibration_only, sfx_kiln_addore, sfx_kiln_produce, sfx_knife_swing, sfx_kromsword_swing, sfx_kvastur_death, sfx_land, sfx_land_water, sfx_leech_alerted, sfx_leech_attack, sfx_leech_attack_hit, sfx_leech_death, sfx_leech_hit, sfx_leech_idle, sfx_lootspawn, sfx_lox_alerted, sfx_lox_attack_bite, sfx_lox_attack_stomp, sfx_lox_shout, sfx_loxcalf_alerted, sfx_malicious_ice_break, sfx_metal_blocked, sfx_metal_blocked_overlay, sfx_metal_shield_blocked, sfx_metal_shield_blocked_overlay, sfx_metalbars_break, sfx_metalbars_hit, sfx_mill_add, sfx_mill_produce, sfx_moleman_alert, sfx_moleman_attack, sfx_moleman_death, sfx_moleman_footsteps, sfx_moleman_hurt, sfx_moleman_idle, sfx_moleman_spawn, sfx_moleman_stonedust, sfx_moose_alert, sfx_moose_attack_melee, sfx_moose_attack_melee_hit, sfx_moose_attack_melee_movement, sfx_moose_attack_melee_whoosh, sfx_moose_calf_verse, sfx_moose_death, sfx_moose_death_explode, sfx_moose_hurt, sfx_moose_idle, sfx_morgen_alert, sfx_morgen_attack, sfx_morgen_death, sfx_morgen_idle, sfx_morgen_roll_jump, sfx_morgen_roll_land, sfx_morgen_slap, sfx_morgen_slap_delayed, sfx_morgen_spawn, sfx_morkhalla_gate_open_end, sfx_morkhalla_gate_open_start, sfx_morkhalla_gate_slide, sfx_neck_alerted, sfx_neck_attack, sfx_neck_attack_hit, sfx_neck_death, sfx_neck_hit, sfx_neck_idle, sfx_obliterator_close, sfx_obliterator_open, sfx_offering, sfx_oozebomb_explode, sfx_oven_burnt, sfx_oven_done, sfx_oven_take, sfx_perfect_dodge, sfx_perfectblock, sfx_pickable_pick, sfx_pickaxe_hit, sfx_pickaxe_hit_vibration_only, sfx_pickaxe_swing, sfx_prespawn, sfx_prespawnLastBossGate, sfx_prespawn_fader, sfx_prespawn_fader_vibration_only, sfx_prespawn_vibration_only, sfx_reload_done, sfx_reload_dverger_done, sfx_reload_dverger_start, sfx_reload_start, sfx_rock_destroyed, sfx_rock_hit, sfx_rock_wall_crumble, sfx_rooster_idle, sfx_seal_alert, sfx_seal_death, sfx_seal_idle, sfx_secretfound, sfx_serpent_alerted, sfx_serpent_attack, sfx_serpent_attack_hit, sfx_serpent_attack_trigger, sfx_serpent_death, sfx_serpent_hurt, sfx_serpent_idle, sfx_serpent_taunt, sfx_shieldgenerator_hit, sfx_shieldgenerator_refuel, sfx_shieldgenerator_shutdown, sfx_shieldgenerator_startup, sfx_ship_destroyed, sfx_ship_impact, sfx_ship_sailposition_change_vibration_only, sfx_silvermace_hit, sfx_skeleton_alerted, sfx_skeleton_attack, sfx_skeleton_basic_attack_melee, sfx_skeleton_basic_death, sfx_skeleton_basic_verse_attack, sfx_skeleton_basic_verse_idle, sfx_skeleton_big_alerted, sfx_skeleton_big_death, sfx_skeleton_death, sfx_skeleton_frozen_attack_melee, sfx_skeleton_frozen_death, sfx_skeleton_frozen_verse_attack, sfx_skeleton_frozen_verse_idle, sfx_skeleton_hildir_attack_melee, sfx_skeleton_hildir_attack_skill, sfx_skeleton_hildir_death, sfx_skeleton_hildir_verse_attack, sfx_skeleton_hildir_verse_idle, sfx_skeleton_hit, sfx_skeleton_idle, sfx_skeleton_mace_hit, sfx_skeleton_poison_attack_melee, sfx_skeleton_poison_death, sfx_skeleton_poison_verse_attack, sfx_skeleton_poison_verse_idle, sfx_skeleton_rise, sfx_skeleton_swamp_attack_melee, sfx_skeleton_swamp_death, sfx_skeleton_swamp_verse_attack, sfx_skeleton_swamp_verse_idle, sfx_skull_summon_skeleton, sfx_sledge_hit, sfx_sledge_iron_hit, sfx_sledge_swing, sfx_smelter_add, sfx_smelter_produce, sfx_snow_hit_debris, sfx_snow_hit_transient, sfx_spawn, sfx_spawn_vibration_only, sfx_spear_flint_hit, sfx_spear_hit, sfx_spear_poke, sfx_spear_throw, sfx_staff_elder_burst, sfx_staff_elder_cast, sfx_staff_elder_grow, sfx_staff_elder_root_hit, sfx_staff_elder_root_swing, sfx_staff_grenade_cast, sfx_staff_lightning_charge, sfx_staff_lightning_fire, sfx_staff_trollstav_troll_impact, sfx_stafffrostorbs_cast, sfx_stafffrostorbs_shield_spawn, sfx_stafforbofahri_cast, sfx_stafforbofahri_explosion, sfx_staffspiritcaller_cast, sfx_staffspiritcaller_summon, sfx_staffthunderblood_cast, sfx_staffthunderblood_thunder, sfx_stonegolem_alerted, sfx_stonegolem_attack_hit, sfx_stonegolem_attack_wosh, sfx_stonegolem_death, sfx_stonegolem_hurt, sfx_stonegolem_idle, sfx_stonegolem_primary_start, sfx_stonegolem_second_start, sfx_stonegolem_spikeattack_trailon, sfx_stonegolem_wakeup, sfx_sword_hit, sfx_sword_swing, sfx_tentaroot_attack, sfx_tick_alerted, sfx_tick_attack_drain, sfx_tick_attack_jump, sfx_tick_hurt, sfx_tick_idle, sfx_torch_swing, sfx_trainingdummy_alert, sfx_trainingdummy_heavy_attack, sfx_trainingdummy_heavy_attack impact, sfx_trainingdummy_idle, sfx_trainingdummy_light_attack, sfx_trainingdummy_light_attack impact, sfx_trainingdummy_stagger, sfx_trainingdummy_throw, sfx_trainingdummy_throw_impact, sfx_treasurechest_destroyed, sfx_tree_fall, sfx_tree_fall_abomination, sfx_tree_fall_hit, sfx_tree_hit, sfx_tree_hit_abomination, sfx_troll_alerted, sfx_troll_attack_hit, sfx_troll_attacking, sfx_troll_death, sfx_troll_hit, sfx_troll_idle, sfx_troll_rock_destroyed, sfx_trollfire_attack_club_impact, sfx_trollfire_attack_club_swing_down, sfx_trollfire_attack_club_swing_up, sfx_trollfire_attack_slam_impact, sfx_trollfire_attack_slam_start, sfx_trollfire_attack_slap_impact, sfx_trollfire_attack_slap_start, sfx_trollfire_attack_slap_swing, sfx_trollfire_attack_throw, sfx_trollfire_death, sfx_trollfire_idle, sfx_trollfire_roar, sfx_ulv_death, sfx_unarmed_blocked, sfx_unarmed_hit, sfx_unarmed_swing, sfx_unbjorn_bite_attack, sfx_unbjorn_bite_attack_impact, sfx_unbjorn_claw_attack, sfx_unbjorn_claw_attack_slash, sfx_unbjorn_death, sfx_unbjorn_footstep, sfx_unbjorn_hurt, sfx_unbjorn_idle, sfx_unstablerock_explosion, sfx_vulture_alert, sfx_vulture_attack_claw, sfx_vulture_death, sfx_vulture_eat_bite, sfx_vulture_eat_break, sfx_vulture_eat_swallow, sfx_vulture_footstep, sfx_vulture_idle, sfx_vulture_wing_flap, sfx_weapons_nature_root, sfx_wolf_alerted, sfx_wolf_attack, sfx_wolf_attack_hit, sfx_wolf_birth, sfx_wolf_death, sfx_wolf_hit, sfx_wolf_love, sfx_wood_blocked, sfx_wood_blocked_overlay, sfx_wood_break, sfx_wood_destroyed, sfx_wood_hit, sfx_woosh_scythe, sfx_wraith_alerted, sfx_wraith_attack, sfx_wraith_attack_hit, sfx_wraith_death, sfx_wraith_hit, sfx_wraith_idle, sfx_writhan_bite, sfx_writhan_bite_attack, sfx_writhan_bite_attack_impact, sfx_writhan_death_charge, sfx_writhan_death_explosion, sfx_writhan_fizz, sfx_writhan_idle, sfx_writhan_step, sfx_writhan_verse_attack, sfx_writhan_verse_death

</details>

<details><summary>All vfx_* (487)</summary>

vfx_ carrion_destroyed, vfx_ArcheryTarget_hit, vfx_AshPieceDestroyed, vfx_Ash_Arch2_Broken1_destroyed, vfx_Ash_Arch2_Broken2_destroyed, vfx_Ashpiece_roof_destroyed, vfx_Ashruin_steepstair_destroyed, vfx_Ashruin_wall3_destroyed, vfx_Ashruin_wall4_destroyed, vfx_Ashruin_wall5_destroyed, vfx_Ashruin_window2_destroyed, vfx_Ashruin_window3_destroyed, vfx_Ashruin_window4_destroyed, vfx_Ashruin_window5_destroyed, vfx_Ashruin_window6_destroyed, vfx_BigBlob_destroyed, vfx_BloodDeath, vfx_BloodHit, vfx_BombBlob_explode_frost, vfx_BombBlob_explode_lava, vfx_BombBlob_explode_morkhalla, vfx_BombBlob_explode_poison, vfx_BombBlob_explode_poisonelite, vfx_BombBlob_explode_tar, vfx_BonemassDeath, vfx_BonemassHit, vfx_BugRepellent, vfx_Burning, vfx_Burning_blue, vfx_Burning_green, vfx_CharredBanner1_destroyed, vfx_CharredBanner2_destroyed, vfx_CharredBanner3_destroyed, vfx_CoalDestroyed, vfx_CoalHit, vfx_Cold, vfx_ColdBall_Hit, vfx_ColdBall_launch, vfx_Damaged_Karve, vfx_Damaged_Raft, vfx_Damaged_VikingShip, vfx_Destroyed_AshlandsShip, vfx_Destroyed_Karve, vfx_Destroyed_Raft, vfx_Destroyed_VikingShip, vfx_Destroyed_VikingShip_frozen, vfx_DraugrSpawn, vfx_FallenWarrior_death, vfx_FallenWarrior_hit, vfx_FernAshlands_destroyed, vfx_FernAshlands_puff, vfx_FireAddFuel, vfx_FireWork_BlackCore, vfx_FireWork_SurtlingCore, vfx_FireWork_ThunderStone, vfx_FireballHit, vfx_Firework_Rocket, vfx_Firework_Rocket_Blue, vfx_Firework_Rocket_Cyan, vfx_Firework_Rocket_Green, vfx_Firework_Rocket_Purple, vfx_Firework_Rocket_Red, vfx_Firework_Rocket_Yellow, vfx_FlameBall_launch, vfx_ForgeAddFuel, vfx_Freezing, vfx_Frost, vfx_FrostOrbs, vfx_GiantMetal_destroyed, vfx_GoblinShield, vfx_GodExplosion, vfx_GraustenDestroyed, vfx_GraustenDestroyed_large, vfx_GuckSackDestroyed, vfx_GuckSackHit, vfx_GuckSackSmall_Destroyed, vfx_Harpooned, vfx_HealthUpgrade, vfx_HearthAddFuel, vfx_HitSparks, vfx_HoleSpawner_destruction, vfx_HoleSpawner_double_destruction, vfx_ImpDeath, vfx_LastBossGate_destroyed, vfx_LightFoot, vfx_MarbleDestroyed, vfx_MarbleHit, vfx_MeadBzerker, vfx_MeadHasty, vfx_MeadSplash, vfx_MeadStrength, vfx_MeadSwimmer, vfx_MeadTamer, vfx_MorkhallaStatueDestroyed, vfx_MudDestroyed, vfx_MudHit, vfx_Place_HildirClothesRack, vfx_Place_HildirFabricRoll, vfx_Place_HildirTableClothes, vfx_Place_Karve, vfx_Place_Raft, vfx_Place_VikingShip, vfx_Place_bed, vfx_Place_beehive, vfx_Place_brazierceiling01, vfx_Place_cart, vfx_Place_cauldron, vfx_Place_charcoalkiln, vfx_Place_chest, vfx_Place_cookingstation_iron, vfx_Place_darkwood_gate, vfx_Place_digg, vfx_Place_forge, vfx_Place_mud_road, vfx_Place_oven, vfx_Place_portal, vfx_Place_raise, vfx_Place_refinery, vfx_Place_replant, vfx_Place_smallitem, vfx_Place_smelter, vfx_Place_spinningwheel, vfx_Place_stone_floor, vfx_Place_stone_floor_2x2, vfx_Place_stone_wall_2x1, vfx_Place_stone_wall_4x2, vfx_Place_throne02, vfx_Place_turret, vfx_Place_windmill, vfx_Place_wood_beam, vfx_Place_wood_floor, vfx_Place_wood_pole, vfx_Place_wood_roof, vfx_Place_wood_stair, vfx_Place_wood_wall, vfx_Place_wood_wall_half, vfx_Place_wood_wall_roof, vfx_Place_workbench, vfx_Poison, vfx_Potion_eitr_minor, vfx_Potion_health_medium, vfx_Potion_stamina_medium, vfx_ProjectileHit, vfx_RockDestroyed, vfx_RockDestroyed_Obsidian, vfx_RockDestroyed_large, vfx_RockDestroyed_marble, vfx_RockHit, vfx_RockHit_Marble, vfx_RockHit_Obsidian, vfx_SawDust, vfx_SawDust_Ashlands, vfx_SawDust_abomination, vfx_ShadowPerson_death, vfx_ShadowPerson_hit, vfx_Slimed, vfx_Smoked, vfx_StaffShield, vfx_StaminaUpgrade, vfx_Tared, vfx_TrainingDummy_destruction, vfx_TrainingDummy_hit, vfx_TrollFrost_Death, vfx_TrollPheromones, vfx_UndeadBurn, vfx_WaterImpact_Karve, vfx_WaterImpact_Raft, vfx_WaterImpact_VikingShip, vfx_Wet, vfx_WishbonePing, vfx_arbalest_fire, vfx_archerytarget_bullseye, vfx_archerytarget_bullseye_double, vfx_archerytarget_bullseye_quint, vfx_arrowhit, vfx_ashland_gate_destruction, vfx_ashland_ruin_wall_windows_broken_4x6_destruction, vfx_ashland_wall_2x2_corner_destruction, vfx_ashland_wall_2x2_destruction, vfx_ashlandsbow_blood_fire, vfx_ashlandsbow_lightning_fire, vfx_ashlandsbow_nature_fire, vfx_ashlandslogdestroyed, vfx_ashlandslogdestroyed_half, vfx_ashlandstreecut, vfx_aspect_summoned_prespawn, vfx_auto_pickup, vfx_bar_ancientmetal_stack_destroyed, vfx_bar_blackmetal_stack_destroyed, vfx_bar_bronze_stack_destroyed, vfx_bar_copper_stack_destroyed, vfx_bar_flametal_stack_destroyed, vfx_bar_gold_stack_destroyed, vfx_bar_iron_stack_destroyed, vfx_bar_silver_stack_destroyed, vfx_bar_tin_stack_destroyed, vfx_barley_destroyed, vfx_barnacle_destroyed, vfx_barnacle_hit, vfx_barrle_destroyed, vfx_beech_cut, vfx_beech_small1_destroy, vfx_beech_small2_destroy, vfx_beechlog_destroyed, vfx_beechlog_half_destroyed, vfx_beehive_destroyed, vfx_beehive_hit, vfx_birch1_aut_cut, vfx_birch1_cut, vfx_birch2_aut_cut, vfx_birch2_cut, vfx_bjorn_groundslam, vfx_blackice_destroyed, vfx_blastfurance_addfuel, vfx_blastfurnace_addore, vfx_blastfurnace_produce, vfx_blob_attack, vfx_blob_death, vfx_blob_frost_attack, vfx_blob_frost_death, vfx_blob_frost_hit, vfx_blob_hit, vfx_blobelite_attack, vfx_blobmork_attack, vfx_blobmork_death, vfx_blobmork_hit, vfx_blobtar_death, vfx_blobtar_hit, vfx_blocked, vfx_boar_birth, vfx_boar_death, vfx_boar_hit, vfx_boar_love, vfx_bone_stack_destroyed, vfx_bonemawserpent_death, vfx_bonepile_destroyed, vfx_bones_pick, vfx_bonfire_AddFuel, vfx_bonfire_destroyed, vfx_bow_fire, vfx_bowl_AddItem, vfx_bush2_e_hit, vfx_bush2_en_destroyed, vfx_bush_destroyed, vfx_bush_destroyed_heath, vfx_bush_leaf_puff, vfx_bush_leaf_puff_heath, vfx_cartograpertable_write, vfx_catapult_legdown, vfx_catapult_load, vfx_catapult_release, vfx_charredbanner_destroyed, vfx_charredcross_spawner_destroyed, vfx_cloth_hanging_destroyed, vfx_clubhit, vfx_coin_pile_destroyed, vfx_coin_stack_destroyed, vfx_cooking_station_transform, vfx_corpse_destruction_large, vfx_corpse_destruction_medium, vfx_corpse_destruction_small, vfx_crate_destroyed, vfx_creature_soothed, vfx_creep_hangingdetroyed, vfx_crossbow_blood_fire, vfx_crossbow_lightning_fire, vfx_crossbow_nature_fire, vfx_crow_death, vfx_crypt_skeleton_chest_destroyed, vfx_damaged_cart, vfx_darkland_groundfog, vfx_deer_death, vfx_deer_hit, vfx_dragon_coldbreath, vfx_dragon_death, vfx_dragon_hurt, vfx_dragon_ice_hit, vfx_dragonegg_destroy, vfx_draugr_death, vfx_draugr_hit, vfx_draugrpile_destroyed, vfx_draugrpile_hit, vfx_dvergpost_destroyed, vfx_dvergr_beam_destroyed, vfx_dvergr_curtain_banner_destroyed, vfx_dvergr_curtain_banner_destroyed_horisontal, vfx_dvergr_pole_destroyed, vfx_dvergr_stake_destroyed, vfx_dvergr_stakewall_destroyed, vfx_dvergr_wood_wall02_destroyed, vfx_dvergr_wood_wall03_destroyed, vfx_dvergr_wood_wall04_destroyed, vfx_dvergrchair_destroyed, vfx_dvergrcreep_beam_destroyed, vfx_dvergrcreep_pole_destroyed, vfx_dvergrcreep_support_destroyed, vfx_dvergrcreep_wood_wall02_destroyed, vfx_dvergrcreep_wood_wall03_destroyed, vfx_dvergrstool_destroyed, vfx_dvergrtable_destroyed, vfx_edge_clouds, vfx_eikthyr_death, vfx_fenring_cultist_death, vfx_fenring_cultist_hildir_death, vfx_fenring_death, vfx_fenring_hurt, vfx_fenrirhide_hanging_destroyed, vfx_fermenter_add, vfx_fermenter_tap, vfx_fir_oldlog, vfx_firetree_regrow, vfx_firetreecut, vfx_firetreecut_dead, vfx_firetreecut_dead_abomination, vfx_firetreecut_dead_snow, vfx_firetreecut_snow, vfx_firetreecut_snow_FrostTrollDeath, vfx_firetreecut_snow_FrostTrollThrow, vfx_firetreecut_snow_small, vfx_firlogdestroyed, vfx_firlogdestroyed_half, vfx_flintpile_destroyed, vfx_foresttroll_hit, vfx_frostarrow_hit, vfx_frostcore_pick, vfx_frostfoundry_transform, vfx_frostkiln_addore, vfx_frostkiln_produce, vfx_frosttroll_hit, vfx_frozengd_destroyed, vfx_frozenking_blackice_destroyed, vfx_frozenking_death, vfx_frozenking_final_death, vfx_frozenking_final_death_ground, vfx_gdking_stomp, vfx_ghost_death, vfx_ghost_hit, vfx_ghost_spawn, vfx_gjall_spit, vfx_goblin_death, vfx_goblin_dn_death, vfx_goblin_dn_hit, vfx_goblin_hit, vfx_goblin_woodwall_destroyed, vfx_goblinbrute_death, vfx_goblinbrute_hildir_death, vfx_goblinbrute_hit, vfx_goblinking_beam_OLD, vfx_grausten_stair_destroyed, vfx_greydwarf_death, vfx_greydwarf_elite_death, vfx_greydwarf_hit, vfx_greydwarf_root_destroyed, vfx_greydwarf_shaman_pray, vfx_greydwarfnest_destroyed, vfx_greydwarfnest_hit, vfx_groundtorch_addFuel, vfx_hatchling_death, vfx_hatchling_hurt, vfx_hjall_spit_hit, vfx_ice_destroyed, vfx_ice_destroyed_shelf, vfx_ice_hit, vfx_iceblocker_destroyed, vfx_icecube_destroyed, vfx_kiln_addore, vfx_kiln_produce, vfx_leech_death, vfx_leech_hit, vfx_lever, vfx_lightningstaff_fire, vfx_lootspawn, vfx_lox_groundslam, vfx_lox_love, vfx_lox_soothed, vfx_mill_add, vfx_mill_produce, vfx_mistlands_mist, vfx_morgenhole_pile_destroyed, vfx_morkhalla_bench_destroyed, vfx_morkhalla_firepit_destroyed, vfx_morkhalla_gatedoor02_destroyed, vfx_morkhalla_gatedoor03_destroyed, vfx_morkhalla_gatedoor_destroyed, vfx_morkhalla_stool_destroyed, vfx_morkhalla_table_destroyed, vfx_morkhalla_trainingdummy1_destroyed, vfx_morkhalla_trainingdummy2_destroyed, vfx_morkhalla_weaponstand_destroyed, vfx_morkhalla_web_destroyed, vfx_mountainkit_chair_destroyed, vfx_mountainkit_table_destroyed, vfx_neck_death, vfx_neck_hit, vfx_oak_cut, vfx_oaklogdestroyed, vfx_oaklogdestroyed_half, vfx_obsidian_destroyed, vfx_ocean_clouds, vfx_odin_despawn, vfx_offering, vfx_perfectblock, vfx_pick_wisp, vfx_pickable_pick, vfx_pinelogdestroyed, vfx_pinelogdestroyed_half, vfx_pinetree_regrow, vfx_pinetreecut, vfx_pinetreecut_dead, vfx_pinetreecut_snow, vfx_player_death, vfx_player_hit, vfx_poisonarrow_hit, vfx_prespawn, vfx_prespawn_fader, vfx_seagull_death, vfx_seal_hit, vfx_seekerbrute_groundslam, vfx_serpent_attack_trigger, vfx_serpent_death, vfx_serpent_hurt, vfx_shieldgenerator_refuel, vfx_shieldgenerator_startup, vfx_shrub_2_destroyed, vfx_shrub_2_heath_destroyed, vfx_shrub_2_hit, vfx_shrub_heath_hit, vfx_silvermace_hit, vfx_skeleton_big_death, vfx_skeleton_death, vfx_skeleton_fire_death, vfx_skeleton_hit, vfx_skeleton_mace_hit, vfx_skullpile_destroyed, vfx_sledge_hit, vfx_sledge_iron_hit, vfx_smelter_addfuel, vfx_smelter_addore, vfx_smelter_produce, vfx_spawn, vfx_spawn_large, vfx_spawn_small, vfx_spikey_beam_destruction, vfx_standing_brazier_destroyed, vfx_stone_floor_2x2_destroyed, vfx_stone_floor_destroyed, vfx_stone_stair_destroyed, vfx_stone_wall_4x2_destroyed, vfx_stone_wall_ruin_2_destroyed, vfx_stone_wall_ruin_destroyed, vfx_stonegolem_attack_hit, vfx_stonegolem_death, vfx_stonegolem_hurt, vfx_stonegolem_wakeup, vfx_stubbe, vfx_swamp_mist, vfx_swamptree_cut, vfx_torch_hit, vfx_tree_fall_hit, vfx_troll_attack_hit, vfx_troll_death, vfx_troll_groundslam, vfx_troll_log_hitground, vfx_troll_rock_destroyed, vfx_troll_summoned_death, vfx_troll_summoned_prespawn, vfx_trollsnow_attack_hit, vfx_trollsnow_groundslam, vfx_trollsnow_log_destroyed, vfx_turnip_grow, vfx_ulv_death, vfx_vines_destroyed, vfx_wagon_destroyed, vfx_walltorch_addFuel, vfx_wolf_death, vfx_wolf_hit, vfx_wood_black_stack_destroyed, vfx_wood_core_stack_destroyed, vfx_wood_fine_stack_destroyed, vfx_wood_frost_stack_destroyed, vfx_wood_stack_destroyed, vfx_wood_yggdrasil_stack_destroyed, vfx_wooden_path_destroyed, vfx_wraith_death, vfx_wraith_hit, vfx_yggashoot_cut, vfx_yggashoot_small1_destroy

</details>

<details><summary>All fx_* (281)</summary>

fx_Abomination_arise, fx_Abomination_arise_end, fx_Abomination_attack1, fx_Abomination_attack1_start, fx_Abomination_attack1_trailon, fx_Abomination_attack2, fx_Abomination_attack2_lift, fx_Abomination_attack2_start, fx_Abomination_attack3, fx_Abomination_attack3_start, fx_Abomination_attack_hit, fx_Adrenaline1, fx_ArmorStand_pick, fx_Bonemass_aoe_start, fx_BonemawSerpent_Spit_Hit, fx_BonemawSerpent_poisonbreath, fx_BonusYield, fx_CharredStone_Destruction, fx_CharredStone_Gibs, fx_CinderFire_Burn, fx_Cinder_hit, fx_Cinder_storm_hit, fx_DvergerMage_Fire_hit, fx_DvergerMage_Ice_hit, fx_DvergerMage_MistileSpawn, fx_DvergerMage_Mistile_attack, fx_DvergerMage_Mistile_die, fx_DvergerMage_Nova_ring, fx_DvergerMage_Support_hit, fx_DvergerMage_Support_start, fx_Dverger_death, fx_Dverger_hit, fx_Eat, fx_Eat_Black, fx_Eat_Blue, fx_Eat_Green, fx_Eat_Orange, fx_Eat_Red, fx_Eat_Yellow, fx_ElakingMole_Sandcloud, fx_ElakingMole_Wakeup, fx_Fader_CorpseExplosion, fx_Fader_Fissure_Prespawn, fx_Fader_Ragdoll, fx_Fader_Roar, fx_Fader_Roar_Projectile_Hit, fx_Fader_Spin, fx_Fading_Bellfragment_Beam, fx_FallenValkyrie_projectile_explosion, fx_FeastAshlandsEat, fx_FeastBlackForestEat, fx_FeastMeadowsEat, fx_FeastMistlandsEat, fx_FeastMountainsEat, fx_FeastOceansEat, fx_FeastPlainsEat, fx_FeastSmall, fx_FeastSwampsEat, fx_FeastTemplate, fx_GP_Activation, fx_GP_Player, fx_GP_Stone, fx_GoblinShieldBreak, fx_GoblinShieldHit, fx_HildirChest_Unlock, fx_IceShelf_Damage, fx_Immobilize, fx_JotunWitch_Death, fx_JotunWitch_Dodge, fx_JotunWitch_Flying, fx_JotunWitch_LightningBolt_Explosion, fx_JotunWitch_LightningBolt_Trigger, fx_JotunWitch_MagicBlast, fx_Lightning, fx_Lightning_red, fx_Potion_fireresist, fx_Potion_frostresist, fx_Puke, fx_QueenPierceGround, fx_Queen_Death, fx_RootAshlands, fx_ShieldCharge_1, fx_ShieldCharge_2, fx_ShieldCharge_3, fx_ShieldCharge_4, fx_ShieldCharge_5, fx_StaffShield_Break, fx_StaffShield_Hit, fx_TickBloodHit, fx_TrollUndead_Gibs, fx_UpgradeStation_Fail, fx_UpgradeStation_Success, fx_WaterImpact_Big, fx_altar_charred_destruction, fx_altar_crystal_destruction, fx_ashvine_destruction, fx_ashvine_leaf_puff, fx_asksvin_pet, fx_asksvin_soothed, fx_asksvin_tamed, fx_aspect_death, fx_babyseeker_death, fx_babyseeker_hurt, fx_backstab, fx_bat_death, fx_bat_hit, fx_batteringram_fire, fx_bjorn_death, fx_bjorn_hit, fx_blobLava_explosion, fx_blobtar_tarball_hit, fx_bloodweapon_hit, fx_boar_pet, fx_candle_addfuel, fx_candle_off, fx_candle_on, fx_chainlightning_hit, fx_chainlightning_spread, fx_chainlightning_spread_red, fx_charred_death, fx_charred_firestaff_chargeup, fx_charred_hit, fx_charred_summoned_death, fx_chicken_birth, fx_chicken_death, fx_chicken_lay_egg, fx_chicken_pet, fx_clusterbombstaff_hit, fx_clusterbombstaff_splinter_hit, fx_creature_tamed, fx_crit, fx_crystal_destruction, fx_deathsquito_hit, fx_deatsquito_death, fx_dragon_land, fx_drown, fx_dynamite_explosion, fx_egg_splash, fx_eikthyr_forwardshockwave, fx_eikthyr_stomp, fx_fader_meteor_hit, fx_fader_meteorsmash, fx_fallenfalkyrie_spin, fx_fallenvalkyrie_death, fx_fallenvalkyrie_poisonbreath, fx_fenring_burning_hand, fx_fenring_burning_hand_long, fx_fenring_flames, fx_fenring_frost, fx_fenring_frost_hand, fx_fenring_frost_hand_aoestart, fx_fenring_frost_hand_long, fx_fenring_icenova, fx_fimbulvinter_meteor_hit, fx_fireball_staff_explosion, fx_fireskeleton_nova, fx_flametalnode_destruction, fx_float_hitwater, fx_float_nibble, fx_frostfoundry_addfuel, fx_frostfoundry_additem, fx_frozenking_aspectspawn, fx_frozenking_chain_fury, fx_frozenking_chain_fury_ascending, fx_frozenking_chain_ground_impact_1, fx_frozenking_chain_ground_impact_2, fx_frozenking_chain_rush, fx_frozenking_chain_rush_impact, fx_frozenking_punch_aoe, fx_frozenking_spikerain_hit, fx_frozenking_spikerain_summoning, fx_frozenking_spikesmash, fx_frozenking_spin, fx_frozenking_tendrils_summoning, fx_frozenking_tendrilspawn, fx_gdking_rootspawn, fx_gjall_death, fx_gjall_egg_splat, fx_gjall_taunt, fx_goblinbrute_groundslam, fx_goblinking_beam_hit, fx_goblinking_death, fx_goblinking_hit, fx_goblinking_meteor_hit, fx_goblinking_nova, fx_greenroots_projectile_hit, fx_guardstone_activate, fx_guardstone_deactivate, fx_guardstone_permitted_add, fx_guardstone_permitted_removed, fx_hare_death, fx_hen_death, fx_hen_love, fx_himminafl_aoe, fx_himminafl_hit, fx_hottub_addwood, fx_icefloor_destruction, fx_iceshard_hit, fx_iceshard_launch, fx_icicle_destruction, fx_jelly_pickup, fx_jotunbane_hit, fx_jotunbane_swing, fx_lavabomb_explosion, fx_leviathanLava_leave, fx_leviathanLava_reaction, fx_leviathan_leave, fx_leviathan_reaction, fx_lightningstaffprojectile_hit, fx_lightningweapon_hit, fx_lox_death, fx_lox_hit, fx_lox_pet, fx_lox_tamed, fx_loxcalf_death, fx_moose_birth, fx_moose_death, fx_moose_hit, fx_morgen_death, fx_morgen_hit, fx_morgen_roll, fx_morgen_swipe, fx_natureweapon_hit, fx_oven_add_food, fx_oven_add_wood, fx_oven_produce, fx_perfectdodge, fx_pheromonebomb_explode, fx_radiation_hit, fx_redlightning_burst, fx_redlightning_launch, fx_refinery_addfuel, fx_refinery_addtissue, fx_refinery_destroyed, fx_refinery_produce, fx_seeker_death, fx_seeker_hurt, fx_seeker_melee_hit, fx_seeker_spawn, fx_seekerbrute_death, fx_shaman_fireball_expl, fx_shaman_protect, fx_shield_start, fx_shield_start_frost, fx_shieldgenerator_attack, fx_shieldgenerator_domehit, fx_siegebomb_explosion, fx_skeleton_pet, fx_sledge_demolisher_hit, fx_stone_L_destroyed, fx_stone_R_destroyed, fx_summon_skeleton, fx_summon_skeleton_spawn, fx_summon_spirit_spawn, fx_summon_start, fx_summon_troll, fx_summon_twitcher_spawn, fx_sw_addflax, fx_sw_produce, fx_tar_bubbles, fx_tendril_death, fx_tentaroot_death, fx_tick_death, fx_tombstone_destroyed, fx_totem_destroyed, fx_trap_arm, fx_trap_trigger, fx_troll_love, fx_turret_addammo, fx_turret_fire, fx_turret_newtarget, fx_turret_notarget, fx_turret_reload, fx_turret_warmup, fx_unbjorn_death, fx_unbjorn_hit, fx_unstablelavarock_explosion, fx_vinegreen_destruction, fx_vines_hit, fx_wolf_pet, fx_writhan_explosion

</details>

## 3. Destructible candidates (eggs, sacs, nests)

| Prefab | Health | Type | MinToolTier | Modifiers (non-Normal) | m_destroyedEffect | m_hitEffect | SpawnWhenDestroyed | DropOnDestroyed | Mesh AABB |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CreepProp_egg_hanging01 | 1 | Default | 0 | all Normal | fx_egg_splash (variant 0) | - | CreepProp_egg_hanging02 | 1-1 @ 0.5: SeekerBrood 1-1 w1 | 2.163 x 2.802 x 2.525 (min y -0.136) |
| dvergrprops_barrel | 800 | Default | 0 | pierce Resistant, chop Weak, poison Immune, spirit Immune | vfx_barrle_destroyed | vfx_watersplash_bathtub, sfx_wood_destroyed | - | 3-6 @ 1: FineWood 1-1 w0.5, CopperScrap 1-1 w0.5, MeadTasty 1-1 w0.5 | 1.512 x 1.593 x 1.781 (min y 0.004) |
| dvergrprops_pickaxe | 1 | Default | 0 | all Normal | vfx_HitSparks, sfx_hit | - | - | 1-1 @ 0.5: IronScrap 1-1 w1 | 0.896 x 0.836 x 0.163 (min y -0.132) |
| dvergrtown_arch | 200 | Default | 0 | blunt Resistant, slash Resistant, pierce Resistant, chop Resistant, pickaxe Resistant, fire Immune, frost Immune, lightning Immune, poison Immune, spirit Immune | sfx_rock_destroyed, vfx_MarbleDestroyed | vfx_MarbleHit, sfx_rock_hit | - | - | 2.193 x 2.015 x 1.041 (min y -1.049) |
| dvergrtown_creep_door | 10 | Default | 0 | blunt Resistant, fire Weak, frost Resistant, lightning Immune, poison Immune, spirit Immune | vfx_creep_hangingdetroyed, vfx_creep_hangingdetroyed, vfx_creep_hangingdetroyed, vfx_creep_hangingdetroyed | fx_babyseeker_hurt | - | - | 4.954 x 4.48 x 0.606 (min y 0.922) |
| dvergrtown_wood_beam | 300 | Default | 0 | pierce Resistant, chop Weak, lightning Immune, poison Immune, spirit Immune | vfx_SawDust, sfx_wood_destroyed | vfx_SawDust | vfx_dvergrcreep_beam_destroyed | 1-1 @ 1: Wood 1-1 w1 | 6.107 x 0.921 x 0.919 (min y -0.458) |
| dvergrtown_wood_pole | 800 | Default | 0 | pierce Resistant, chop Weak, lightning Immune, poison Immune, spirit Immune | vfx_SawDust, sfx_wood_destroyed | vfx_SawDust | vfx_dvergrcreep_pole_destroyed | 1-2 @ 1: Wood 1-1 w1, CopperScrap 1-1 w0.5 | 1.364 x 4 x 1.34 (min y -2) |
| dvergrtown_wood_support | 500 | Default | 0 | pierce Resistant, chop Weak, lightning Immune, poison Immune, spirit Immune | vfx_SawDust, sfx_wood_destroyed | vfx_SawDust | vfx_dvergrcreep_support_destroyed | 1-2 @ 1: Wood 1-1 w1 | 6.107 x 4.712 x 1.103 (min y 0) |
| dvergrtown_wood_wall01 | 200 | Default | 0 | pierce Resistant, chop Weak, poison Immune, spirit Immune | sfx_wood_destroyed, vfx_SawDust | vfx_SawDust | vfx_dvergrcreep_wood_wall03_destroyed | 1-3 @ 1: Wood 1-1 w1, CopperScrap 1-1 w0.5 | 6.71 x 5.421 x 0.919 (min y -2.708) |
| dvergrtown_wood_wall02 | 200 | Default | 0 | pierce Resistant, chop Weak, poison Immune, spirit Immune | sfx_wood_destroyed, vfx_SawDust | vfx_SawDust | vfx_dvergrcreep_wood_wall02_destroyed | 1-2 @ 1: Wood 1-1 w1 | 6.107 x 4.921 x 0.921 (min y -0.458) |
| dvergrtown_wood_wall03 | 100 | Default | 0 | pierce Resistant, chop Weak, poison Immune, spirit Immune | sfx_wood_destroyed, vfx_SawDust | vfx_SawDust | vfx_dvergr_wood_wall04_destroyed | 1-2 @ 1: Wood 1-1 w1 | 3.914 x 4.019 x 0.791 (min y -0.019) |
| GraveStone_Broken_CharredTwitcherNest | 20 | Default | 0 | blunt Resistant, slash Resistant, pierce Resistant, fire Immune, frost Resistant, lightning Resistant, poison Ignore, spirit Ignore | vfx_RockDestroyed | - | - | 1-1 @ 1: Grausten 1-1 w1 | 0.724 x 1.477 x 0.26 (min y -0.371) |
| GraveStone_CharredTwitcherNest | 20 | Default | 0 | blunt Resistant, slash Resistant, pierce Resistant, fire Immune, frost Immune, lightning Immune, poison Ignore, spirit Ignore | vfx_RockDestroyed | - | - | 1-1 @ 1: Grausten 1-1 w1 | 0.747 x 1.904 x 0.26 (min y -0.371) |
| GraveStone_Elite_Broken_CharredTwitcherNest | 20 | Default | 0 | blunt Resistant, slash Resistant, pierce Resistant, fire Immune, frost Resistant, lightning Resistant, poison Ignore, spirit Ignore | vfx_RockDestroyed | - | - | 1-1 @ 1: Grausten 1-1 w1 | 0.724 x 1.477 x 0.26 (min y -0.371) |
| GraveStone_Elite_CharredTwitcherNest | 20 | Default | 0 | blunt Resistant, slash Resistant, pierce Resistant, fire Immune, frost Immune, lightning Immune, poison Ignore, spirit Ignore | vfx_RockDestroyed | - | - | 1-1 @ 1: Grausten 1-1 w1 | 0.747 x 1.904 x 0.26 (min y -0.371) |
| GuckSack | 30 | Default | 0 | blunt Immune, slash Immune, pierce Immune, fire Immune, frost Immune, lightning Immune, poison Immune, spirit Immune | vfx_GuckSackDestroyed, sfx_GuckSackDestroyed | vfx_GuckSackHit, sfx_GuckSackHit | - | 4-7 @ 1: Guck 1-1 w1 | 2.413 x 3.921 x 2.795 (min y -1.859) |
| GuckSack_small | 30 | Default | 0 | blunt Immune, slash Immune, pierce Immune, fire Immune, frost Immune, lightning Immune, poison Immune, spirit Immune | vfx_GuckSackSmall_Destroyed, sfx_GuckSackDestroyed | vfx_GuckSackHit, sfx_GuckSackHit | - | 1-2 @ 1: Guck 1-1 w1 | 1.207 x 1.961 x 1.397 (min y -0.929) |
| prop_itemstand_TrophySeekerBrute | 10 | Default | 0 | all Normal | - | - | - | 1-1 @ 1: FineWood 1-1 w0.9, TrophySeekerBrute 1-1 w0.05 | 1.31 x 2.519 x 0.826 (min y -1.349) |
| SeekerEgg | 1 | Default | 0 | spirit Ignore | fx_egg_splash (variant 0) | - | - | 1-1 @ 0.5: SeekerBrood 1-1 w1 | 0.528 x 1.164 x 0.528 (min y 0.002) |
| SeekerEgg_alwayshatch | 1 | Default | 0 | spirit Ignore | fx_egg_splash (variant 0) | - | - | 1-1 @ 1: SeekerBrood 1-1 w1 | 0.528 x 1.164 x 0.528 (min y 0.002) |
| Spawner_GreydwarfNest | 100 | Default | 0 | blunt Resistant, pierce Resistant, pickaxe Ignore, fire Weak, frost Immune, poison Immune, spirit Ignore | sfx_greydwarfnest_destroyed, vfx_greydwarfnest_destroyed | vfx_greydwarfnest_hit, sfx_greydwarfnest_hit | - | 1-1 @ 1: AncientSeed 1-1 w0 | 9.441 x 5.73 x 8.32 (min y -1.142) |

## 4. Items

ObjectDB: 1519 items, 481 recipes, 356 with durability.

### Wisplight (Demister and tokens containing demister / wisp)

| Prefab | m_name | Type | UseDurability | MaxDurability | DurabilityPerLevel | MaxQuality | Recipe |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Demister | $item_demister (Lumière de feu-follet) | Utility | False | 100 | 50 | 1 | piece_workbench ($piece_workbench) lvl 1: Silver x1 (+1/lvl), Wisp x1 (+1/lvl) |
| Wisp | $item_wisp (Feu-follet) | Material | False | 100 | 50 | 1 | no recipe |

### Entrails and Bloodbag

| Prefab | Found | m_name | Type | Weight | MaxStack | Value | Teleportable |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Entrails | True | $item_entrails (Entrailles) | Material | 0.5 | 50 | 0 | True |
| Bloodbag | True | $item_bloodbag (Poche de sang) | Material | 0.5 | 50 | 0 | True |

### Fishing baits

| Prefab | m_name | MaxStack | Recipe output | Station | Resources | Trophy ingredients |
| --- | --- | --- | --- | --- | --- | --- |
| FishingBait | $item_fishingbait (Appât de pêche) | 100 | no recipe | - | - | - |
| FishingBaitAshlands | $item_fishingbait_ashlands (Appât de pêche chaud) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyCharredMelee x1 (+1/lvl) [trophy] | 1 |
| FishingBaitCave | $item_fishingbait_cave (Appât de pêche froid) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyFenring x1 (+1/lvl) [trophy] | 1 |
| FishingBaitDeepNorth | $item_fishingbait_deepnorth (Appât de pêche gelé) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyHatchling x1 (+1/lvl) [trophy] | 1 |
| FishingBaitForest | $item_fishingbait_forest (Appât de pêche moussu) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyFrostTroll x1 (+1/lvl) [trophy] | 1 |
| FishingBaitMistlands | $item_fishingbait_mistlands (Appât de pêche brumeux) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyLox x1 (+1/lvl) [trophy] | 1 |
| FishingBaitOcean | $item_fishingbait_ocean (Appât de pêche lourd) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophySerpent x1 (+1/lvl) [trophy] | 1 |
| FishingBaitPlains | $item_fishingbait_plains (Appât de pêche piquant) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyGoblin x1 (+1/lvl) [trophy] | 1 |
| FishingBaitSwamp | $item_fishingbait_swamp (Appât de pêche odorant) | 100 | 20 | piece_preptable ($piece_preptable) lvl 1 | FishingBait x20 (+1/lvl), TrophyAbomination x1 (+1/lvl) [trophy] | 1 |

### Padded armor

| Prefab | m_name | Type | UseDurability | Max durability per quality | DurabilityPerLevel | Armor (+/lvl) | Damages | Station | Resources (amount, +perLevel) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ArmorPaddedCuirass | $item_chest_pcuirass (Cuirasse rembourrée) | Chest | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| ArmorPaddedGreaves | $item_legs_pgreaves (Jambières rembourrées) | Legs | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| FW_ArmorPaddedCuirass | $item_chest_pcuirass (Cuirasse rembourrée) | Chest | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| FW_ArmorPaddedGreaves | $item_legs_pgreaves (Jambières rembourrées) | Legs | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| HelmetPadded | $item_helmet_padded (Casque rembourré) | Helmet | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | blunt 10 | forge ($piece_forge) lvl 1 | Iron x10 (+5/lvl), LinenThread x15 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| SP_ArmorPaddedCuirass | $item_chest_pcuirass (Cuirasse rembourrée) | Chest | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |
| SP_ArmorPaddedGreaves | $item_legs_pgreaves (Jambières rembourrées) | Legs | True | 1000 / 1200 / 1400 / 1600 | 200 | 26 (+2) | none | forge ($piece_forge) lvl 2 | Iron x10 (+3/lvl), LinenThread x20 (+10/lvl), Upgrader4Armor x1 (+0/lvl) |

### Bronze / iron weapons and shields

| Prefab | m_name | Type | UseDurability | Max durability per quality | DurabilityPerLevel | Armor (+/lvl) | Damages | Station | Resources (amount, +perLevel) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| KnifeCopper | $item_knife_copper (Couteau en cuivre) | OneHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 20 (+1) | slash 12, pierce 12 | forge ($piece_forge) lvl 1 | Wood x2 (+0/lvl), Copper x8 (+4/lvl), GreydwarfEye x0 (+8/lvl), Upgrader1Weapon x1 (+0/lvl) |
| SwordBronze | $item_sword_bronze (Épée en bronze) | OneHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 20 (+1) | slash 35 | forge ($piece_forge) lvl 1 | Wood x2 (+1/lvl), Bronze x8 (+4/lvl), LeatherScraps x2 (+1/lvl), Upgrader1Weapon x1 (+0/lvl) |
| AxeBronze | $item_axe_bronze (Hache en bronze) | OneHandedWeapon | True | 125 / 175 / 225 / 275 | 50 | 20 (+1) | slash 40, chop 40 | forge ($piece_forge) lvl 1 | Wood x4 (+0/lvl), Bronze x8 (+4/lvl), LeatherScraps x2 (+1/lvl), Upgrader2Weapon x1 (+0/lvl) |
| MaceBronze | $item_mace_bronze (Massue de bronze) | OneHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 20 (+1) | blunt 35 | forge ($piece_forge) lvl 1 | Wood x4 (+0/lvl), Bronze x8 (+4/lvl), LeatherScraps x3 (+0/lvl), Upgrader1Weapon x1 (+0/lvl) |
| SpearBronze | $item_spear_bronze (Lance en bronze) | OneHandedWeapon | True | 100 / 150 / 200 / 250 | 50 | 15 (+1) | pierce 35 | forge ($piece_forge) lvl 1 | Wood x5 (+3/lvl), Bronze x6 (+4/lvl), DeerHide x2 (+1/lvl), Upgrader1Weapon x1 (+0/lvl) |
| AtgeirBronze | $item_atgeir_bronze (Atgeir en bronze) | TwoHandedWeapon | True | 125 / 175 / 225 / 275 | 50 | 15 (+1) | pierce 45 | forge ($piece_forge) lvl 1 | Wood x10 (+0/lvl), Bronze x8 (+4/lvl), LeatherScraps x2 (+0/lvl), Upgrader1Weapon x1 (+0/lvl) |
| PickaxeBronze | $item_pickaxe_bronze (Pioche en bronze) | TwoHandedWeapon | True | 120 / 170 / 220 / 270 | 50 | 20 (+1) | pierce 25, pickaxe 25 | forge ($piece_forge) lvl 1 | RoundLog x3 (+1/lvl), Bronze x10 (+5/lvl), Upgrader1Weapon x1 (+0/lvl) |
| ShieldBronzeBuckler | $item_shield_bronzebuckler (Bouclier en bronze) | Shield | True | 200 / 250 / 300 | 50 | 100 (+1) | none | forge ($piece_forge) lvl 1 | Bronze x10 (+5/lvl), Wood x4 (+1/lvl), Upgrader1Armor x1 (+0/lvl) |
| SwordIron | $item_sword_iron (Épée en fer) | OneHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 20 (+1) | slash 55 | forge ($piece_forge) lvl 2 | Wood x2 (+1/lvl), Iron x20 (+10/lvl), LeatherScraps x3 (+2/lvl), Upgrader2Weapon x1 (+0/lvl) |
| AxeIron | $item_axe_iron (Hache en fer) | OneHandedWeapon | True | 175 / 225 / 275 / 325 | 50 | 20 (+1) | slash 60, chop 50 | forge ($piece_forge) lvl 2 | Wood x4 (+0/lvl), Iron x20 (+10/lvl), LeatherScraps x2 (+1/lvl), Upgrader3Weapon x1 (+0/lvl) |
| MaceIron | $item_mace_iron (Massue de fer) | OneHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 20 (+1) | blunt 55 | forge ($piece_forge) lvl 2 | Wood x4 (+0/lvl), Iron x20 (+10/lvl), LeatherScraps x3 (+0/lvl), Upgrader2Weapon x1 (+0/lvl) |
| SledgeIron | $item_sledge_iron (Masse de fer) | TwoHandedWeapon | True | 100 / 150 / 200 / 250 | 50 | 20 (+1) | blunt 55 | forge ($piece_forge) lvl 2 | ElderBark x10 (+2/lvl), Iron x30 (+15/lvl), YmirRemains x4 (+2/lvl), TrophyDraugrElite x1 (+0/lvl) [trophy], Upgrader2Weapon x1 (+0/lvl) |
| AtgeirIron | $item_atgeir_iron (Atgeir en fer) | TwoHandedWeapon | True | 175 / 225 / 275 / 325 | 50 | 15 (+1) | pierce 65 | forge ($piece_forge) lvl 2 | Wood x10 (+0/lvl), Iron x30 (+15/lvl), LeatherScraps x2 (+1/lvl), Upgrader2Weapon x1 (+0/lvl) |
| Battleaxe | $item_battleaxe (Hache de combat) | TwoHandedWeapon | True | 200 / 250 / 300 / 350 | 50 | 15 (+1) | slash 70, chop 40 | forge ($piece_forge) lvl 2 | ElderBark x30 (+5/lvl), Iron x35 (+15/lvl), LeatherScraps x4 (+0/lvl), Upgrader2Weapon x1 (+0/lvl) |
| PickaxeIron | $item_pickaxe_iron (Pioche en fer) | TwoHandedWeapon | True | 150 / 200 / 250 / 300 | 50 | 20 (+1) | pierce 33, pickaxe 33 | forge ($piece_forge) lvl 2 | RoundLog x3 (+1/lvl), Iron x20 (+10/lvl), Upgrader2Weapon x1 (+0/lvl) |
| ShieldBanded | $item_shield_banded (Bouclier bandé) | Shield | True | 200 / 250 / 300 | 50 | 200 (+1) | blunt 10 | forge ($piece_forge) lvl 2 | FineWood x10 (+10/lvl), Iron x8 (+4/lvl), Upgrader2Armor x1 (+0/lvl) |
| BowFineWood | $item_bow_finewood (Arc de bois noble) | Bow | True | 100 / 150 / 200 / 250 | 50 | 15 (+1) | pierce 32 | piece_workbench ($piece_workbench) lvl 1 | FineWood x10 (+5/lvl), RoundLog x10 (+5/lvl), DeerHide x2 (+2/lvl), Upgrader1Weapon x1 (+0/lvl) |

## 5. Room themes and Dvergr / Mistlands locations

### Room.Theme

| Name | Value | Hex | Rooms in DungeonDB |
| --- | --- | --- | --- |
| None | 0 | 0x0 | 0 |
| Crypt | 1 | 0x1 | 0 |
| SunkenCrypt | 2 | 0x2 | 16 |
| Cave | 4 | 0x4 | 12 |
| ForestCrypt | 8 | 0x8 | 6 |
| GoblinCamp | 16 | 0x10 | 17 |
| MeadowsVillage | 32 | 0x20 | 10 |
| MeadowsFarm | 64 | 0x40 | 13 |
| DvergerTown | 128 | 0x80 | 48 |
| DvergerBoss | 256 | 0x100 | 18 |
| ForestCryptHildir | 512 | 0x200 | 3 |
| CaveHildir | 1024 | 0x400 | 1 |
| PlainsFortHildir | 2048 | 0x800 | 33 |
| AshlandRuins | 4096 | 0x1000 | 6 |
| FortressRuins | 8192 | 0x2000 | 3 |
| Hole | 16384 | 0x4000 | 32 |
| NorthVillage | 65536 | 0x10000 | 9 |
| MorkHalla | 131072 | 0x20000 | 16 |

<details><summary>DvergerTown / DvergerBoss rooms (66)</summary>

dvergr_bossroom_middle_endcap01 (DvergerBoss), dvergr_bossroom_middle_endcap02 (DvergerBoss), dvergr_bossroom_middle_endcap03 (DvergerBoss), dvergr_bossroom_roof_endcap01 (DvergerBoss), dvergr_bossroom_roof_endcap02 (DvergerBoss), dvergr_bossroom_roof_endcap03 (DvergerBoss), dvergr_bossroom_top_endcap01 (DvergerBoss), dvergr_bossroom_top_endcap02 (DvergerBoss), dvergr_bossroom_top_endcap03 (DvergerBoss), dvergr_corridor01 (DvergerTown), dvergr_corridor02 (DvergerTown), dvergr_divider01 (DvergerTown), dvergr_divider02 (DvergerTown), dvergr_divider03 (DvergerTown), dvergr_entrance01 (DvergerTown), dvergr_entrance02 (DvergerTown), dvergr_new_bossroom_ENTRANCE02 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair01 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair02 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair03 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair04 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair05 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair06 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair07 (DvergerBoss), dvergr_new_bossroom_bottom_endcap_stair08 (DvergerBoss), dvergr_room01 (DvergerTown), dvergr_room02 (DvergerTown), dvergr_room03 (DvergerTown), dvergr_room04 (DvergerTown), dvergr_room_TREASURE (DvergerTown), dvergr_room_TREASURE2 (DvergerTown), dvergr_room_endcap01 (DvergerTown), dvergr_room_endcap02 (DvergerTown), dvergr_room_endcap03 (DvergerTown), dvergr_room_fence01 (DvergerTown), dvergr_room_open01 (DvergerTown), dvergr_room_open02 (DvergerTown), dvergr_room_open03 (DvergerTown), dvergr_room_open04 (DvergerTown), dvergr_roombig_corner01 (DvergerTown), dvergr_roombig_corridor01 (DvergerTown), dvergr_roombig_corridor02 (DvergerTown), dvergr_roombig_endcap02 (DvergerTown), dvergr_roombig_open01 (DvergerTown), dvergr_roombig_open02 (DvergerTown), dvergr_roombig_side01 (DvergerTown), dvergr_roombig_side02 (DvergerTown), dvergr_roombig_toStone01 (DvergerTown), dvergr_roombig_toStone02 (DvergerTown), dvergr_roombig_windows01 (DvergerTown), dvergr_stair01 (DvergerTown), dvergr_stair02 (DvergerTown), dvergr_stair03 (DvergerTown), dvergr_stone_corridor01 (DvergerTown), dvergr_stone_corridor02 (DvergerTown), dvergr_stone_corridor03 (DvergerTown), dvergr_stone_endcap01 (DvergerTown), dvergr_stone_endcap02 (DvergerTown), dvergr_stone_endcap03 (DvergerTown), dvergr_stone_open01 (DvergerTown), dvergr_stone_room01 (DvergerTown), dvergr_stone_room02 (DvergerTown), dvergr_stone_room03 (DvergerTown), dvergr_stone_room04 (DvergerTown), dvergr_stone_stair01 (DvergerTown), dvergr_stone_turn01 (DvergerTown)

</details>

### Locations

ZoneSystem.m_locations: 232 entries.

| m_name | m_prefabName | Enabled | Biome | Quantity | Unique | InteriorRadius | ExteriorRadius | DungeonGenerator |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Mistlands_GuardTower1_new | Mistlands_GuardTower1_new | True | Mistlands | 75 | False | 0 | 24 | none |
| Mistlands_GuardTower1_ruined_new | Mistlands_GuardTower1_ruined_new | True | Mistlands | 80 | False | 0 | 20 | none |
| Mistlands_GuardTower1_ruined_new2 | Mistlands_GuardTower1_ruined_new2 | True | Mistlands | 20 | False | 0 | 20 | none |
| Mistlands_GuardTower2_new | Mistlands_GuardTower2_new | True | Mistlands | 75 | False | 0 | 24 | none |
| Mistlands_GuardTower3_new | Mistlands_GuardTower3_new | True | Mistlands | 50 | False | 0 | 24 | none |
| Mistlands_GuardTower3_ruined_new | Mistlands_GuardTower3_ruined_new | True | Mistlands | 50 | False | 0 | 20 | none |
| Mistlands_Lighthouse1_new | Mistlands_Lighthouse1_new | True | Mistlands | 100 | False | 0 | 20 | none |
| Mistlands_Excavation1 | Mistlands_Excavation1 | True | Mistlands | 40 | False | 0 | 20 | none |
| Mistlands_Excavation2 | Mistlands_Excavation2 | True | Mistlands | 40 | False | 0 | 20 | none |
| Mistlands_Excavation3 | Mistlands_Excavation3 | True | Mistlands | 40 | False | 0 | 17 | none |
| Mistlands_Harbour1 | Mistlands_Harbour1 | True | Mistlands | 100 | False | 0 | 20 | none |
| Mistlands_Viaduct1 | Mistlands_Viaduct1 | True | Mistlands | 100 | False | 0 | 24 | none |
| Mistlands_Viaduct2 | Mistlands_Viaduct2 | True | Mistlands | 150 | False | 0 | 8 | none |
| Mistlands_RockSpire1 | Mistlands_RockSpire1 | True | Mistlands | 200 | False | 0 | 4 | none |
| Mistlands_Giant1 | Mistlands_Giant1 | True | Mistlands | 250 | False | 0 | 11 | none |
| Mistlands_Giant2 | Mistlands_Giant2 | True | Mistlands | 85 | False | 0 | 10 | none |
| Mistlands_DvergrTownEntrance1 | Mistlands_DvergrTownEntrance1 | True | Mistlands | 120 | False | 32 | 20 | Interior/DG_DvergrTown: themes=DvergerTown, algorithm=Dungeon, rooms 16-96, zoneSize=64 x 256 x 64, tile=8, alternative=True |
| Mistlands_DvergrTownEntrance2 | Mistlands_DvergrTownEntrance2 | True | Mistlands | 120 | False | 32 | 32 | Interior/DG_DvergrTown: themes=DvergerTown, algorithm=Dungeon, rooms 16-96, zoneSize=64 x 256 x 64, tile=8, alternative=True |
| Mistlands_DvergrBossEntrance1 | Mistlands_DvergrBossEntrance1 | True | Mistlands | 5 | False | 32 | 32 | Interior/DG_DvergrBoss: themes=DvergerBoss, algorithm=Dungeon, rooms 512-512, zoneSize=64 x 256 x 64, tile=8, alternative=True |
| Mistlands_RoadPost1 | Mistlands_RoadPost1 | True | Mistlands | 500 | False | 0 | 6 | none |
| Mistlands_StatueGroup1 | Mistlands_StatueGroup1 | True | Mistlands | 200 | False | 0 | 16 | none |
| Mistlands_Statue1 | Mistlands_Statue1 | True | Mistlands | 200 | False | 0 | 6 | none |
| Mistlands_Statue2 | Mistlands_Statue2 | True | Mistlands | 200 | False | 0 | 6 | none |
| Mistlands_Swords1 | Mistlands_Swords1 | True | Mistlands | 33 | False | 0 | 12 | none |
| Mistlands_Swords2 | Mistlands_Swords2 | True | Mistlands | 33 | False | 0 | 12 | none |
| Mistlands_Swords3 | Mistlands_Swords3 | True | Mistlands | 33 | False | 0 | 8 | none |
| Runestone_Mistlands | Runestone_Mistlands | True | Mistlands | 50 | False | 0 | 12 | none |
| CharredTowerRuins1_dvergr | CharredTowerRuins1_dvergr | True | AshLands | 30 | False | 0 | 22 | none |

## 6. Shaders and instancing

`SystemInfo.supportsInstancing` = True. Candidates: `Performance.ShaderCandidates` (`Custom/Creature,Custom/Piece,Standard,Legacy Shaders/Diffuse`) + Custom/Creature, Custom/Piece, Custom/Vegetation, Custom/Player.

Immediate test: `new Material(shader) { enableInstancing = true }`, read back, then one `Graphics.RenderMeshInstanced` of a quad 2 m in front of the camera; warnings/errors logged during the call are captured. A deferred check over the next frames is appended at the end of this file.

| Shader | Shader.Find | Used by prefabs | isSupported | Queue | Passes | INSTANCING* keyword | enableInstancing readback | RenderMeshInstanced |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Custom/Creature | null | yes (from prefab material) | True | 2000 | 4 | True | True | no exception |
| Custom/Piece | null | yes (from prefab material) | True | 2000 | 4 | True | True | no exception |
| Standard | null | yes (from prefab material) | True | 2000 | 4 | True | True | no exception |
| Legacy Shaders/Diffuse | found | no | True | 2000 | 3 | True | True | no exception |
| Custom/Vegetation | null | yes (from prefab material) | True | 2000 | 4 | True | True | no exception |
| Custom/Player | null | yes (from prefab material) | True | 2000 | 4 | True | True | no exception |

### Candidate shader properties

`Custom/Piece` (33 properties): `_TriplanarFoldout` Float, `_TriplanarMap` Float, `_TriplanarLocalPos` Float, `_TriplanarScale` Float, `_ColorFoldout` Float, `_MainTex` Texture, `_Color` Color, `_MainTex_ST` Vector, `_Cutoff` Range, `_EmissionSpacer` Float, `_EmissionMap` Texture, `_EmissionColor` Color, `_SmoothnessFoldout` Float, `_MetallicTex` Texture, `_Metallic` Range, `_MetalColor` Color, `_Glossiness` Range, `_MetallicAlphaGloss` Range, `_NormalFoldout` Float, `_BumpMap` Texture, `_BumpScale` Float, `_TwoSidedNormals` Float, `_NoiseFoldout` Float, `_NoiseTex` Texture, `_ValueNoise` Float, `_RippleDistance` Float, `_RippleFreq` Float, `_ValueNoiseVertex` Float, `_MiscFoldout` Float, `_Cull` Float, `_AddRain` Float, `_AddSnow` Int, `_MoveableObject` Float

`Standard` (27 properties): `_Color` Color, `_MainTex` Texture, `_Cutoff` Range, `_Glossiness` Range, `_GlossMapScale` Range, `_SmoothnessTextureChannel` Float, `_Metallic` Range, `_MetallicGlossMap` Texture, `_SpecularHighlights` Float, `_GlossyReflections` Float, `_BumpScale` Float, `_BumpMap` Texture, `_Parallax` Range, `_ParallaxMap` Texture, `_OcclusionStrength` Range, `_OcclusionMap` Texture, `_EmissionColor` Color, `_EmissionMap` Texture, `_DetailMask` Texture, `_DetailAlbedoMap` Texture, `_DetailNormalMapScale` Float, `_DetailNormalMap` Texture, `_UVSec` Float, `_Mode` Float, `_SrcBlend` Float, `_DstBlend` Float, `_ZWrite` Float

`Legacy Shaders/Diffuse` (2 properties): `_Color` Color, `_MainTex` Texture

`Custom/Vegetation` (35 properties): `_MainTex` Texture, `_Color` Color, `_Glossiness` Range, `_Metallic` Range, `_Cutoff` Range, `_USEMETALMAP` Float, `_MetalTex` Texture, `_MetalGloss` Range, `_BumpMap` Texture, `_BumpScale` Float, `_TwoSidedNormals` Float, `_SphereNormals` Range, `_SphereOffset` Float, `_EmissiveTex` Texture, `_EmissionColor` Color, `_MossTex` Texture, `_MossAlpha` Range, `_MossBlend` Range, `_MossNormal` Range, `_MossTransition` Float, `_AddSnow` Float, `_SnowSparkleLum` Float, `_AddRain` Float, `_UV2Height` Float, `_Height` Float, `_SwaySpeed` Float, `_SwayDistance` Float, `_RippleSpeed` Float, `_RippleDistance` Float, `_RippleDeadzoneMin` Range, `_RippleDeadzoneMax` Range, `_PushDistance` Float, `_PushClothMode` Float, `_CamCull` Float, `_Cull` Float

`Custom/Player` (14 properties): `_Cutoff` Range, `_MainTex` Texture, `_SkinBumpMap` Texture, `_SkinColor` Color, `_ChestTex` Texture, `_ChestBumpMap` Texture, `_ChestMetal` Texture, `_LegsTex` Texture, `_LegsBumpMap` Texture, `_LegsMetal` Texture, `_BumpScale` Float, `_Glossiness` Range, `_MetalGlossiness` Range, `_SnowCover` Range

### Shader usage across all prefabs

| Shader | Material slots | Prefabs | isSupported | Sample prefabs |
| --- | --- | --- | --- | --- |
| Custom/Piece | 7947 | 785 | True | Acorn, ArmorStand, ArmorStand_Female, ArmorStand_Male, Ashland_Steepstair |
| Custom/StaticRock | 6980 | 418 | True | ArrowFrost, ArrowObsidian, ArrowPoison, Ashlands_rock1, Ashlands_rock2 |
| Custom/Trilinearmap | 3709 | 34 | True | HeathRockPillar_frac, RockFingerBroken_frac, RockFinger_frac, RockThumb_frac, Rock_3_deepnorth_frac |
| Standard | 3538 | 1181 | True | Amber, AmberPearl, AncientCoin, AncientGemstoneBlack, AncientGemstoneGreen |
| Custom/Creature | 1580 | 761 | True | Abomination, Abomination_ragdoll, ArmorAshlandsMediumChest, ArmorAshlandsMediumlegs, ArmorBerserkerChest |
| Legacy Shaders/Particles/Alpha Blended | 1324 | 1199 | True | Acorn, Amber, AmberPearl, AncientCoin, AncientGemstoneBlack |
| Lux Lit Particles/ Bumped | 1294 | 803 | True | Abomination, ArrowPoison, Aspect_Bonemass, Aspect_Elder, Aspect_Yagluth |
| Particles/Standard Unlit2 | 1104 | 573 | True | ArrowFrost, ArrowNeedle, Aspect_Eikthyr, Aspect_Fader, AtgeirBlackmetal |
| Particles/Standard Surface2 | 634 | 434 | True | Abomination, Aspect_Bonemass, Aspect_Eikthyr, Aspect_Elder, Aspect_SeekerQueen |
| Custom/Vegetation | 623 | 188 | True | Acorn, AshlandsBranch1, AshlandsBranch2, AshlandsBranch3, AshlandsBush1 |
| Custom/Gradient Mapped Particle (Unlit) | 444 | 235 | True | Aspect_Fader, AtgeirGold_FrostFire, AxeBerzerkrNature, AxeGold_FrostFire, BatteringRam |
| Valheim/Snow Mesh | 361 | 298 | True | Piece_flametal_beam, Piece_flametal_pillar, Piece_grausten_floor_1x1, Piece_grausten_floor_2x2, Piece_grausten_floor_4x4 |
| Legacy Shaders/Particles/Additive | 272 | 191 | True | BatteringRam, BogWitch_Fire_Pit, BombBlob_Frost_projectile, BombBlob_Lava_projectile, BombBlob_Morkhalla_projectile |
| Standard (Specular setup) | 217 | 34 | True | Coal, Fader_Fissure_AOE, FeastAshlands, FeastBlackforest, FeastDeepNorth |
| Custom/Tar | 148 | 17 | True | IceWall, TarLiquid, WaterLiquid, fx_aspect_death, fx_frozenking_chain_fury |
| Custom/Particle (Unlit) | 139 | 90 | True | BogWitch_Fire_Pit, BonePileSpawner, BonePileSpawner_swamp, CinderSky, CinderStorm |
| Custom/Blob | 122 | 27 | True | Blob, BlobAspect, BlobElite, BlobMork, BlobMorkBig |
| Custom/LitParticles | 108 | 86 | True | AxeBerzerkrBlood, AxeJotunBane, BlobMorkBig, BonePileSpawner, BonePileSpawner_swamp |
| Custom/ParticleDecal | 108 | 93 | True | fx_Dverger_death, fx_Dverger_hit, fx_Puke, fx_TickBloodHit, fx_bat_death |
| Standard TwoSided | 102 | 59 | True | ArmorBronzeLegs, ArmorWolfChest, ArmorWolfLegs, ArrowBronze, ArrowFire |
| Custom/ShadowBlob | 89 | 18 | True | FishingRodFloat, Karve, Raft, Trailership, VikingShip |
| Custom/Fallen Warrior | 73 | 47 | True | Bjorn_spiritcaller, Boar_spiritcaller, FW_ArmorBronzeChest, FW_ArmorBronzeLegs, FW_ArmorFenringChest |
| Unlit/Lighting | 65 | 14 | True | AtgeirHimminAfl, fx_UpgradeStation_Fail, fx_UpgradeStation_Success, fx_chainlightning_hit, fx_eikthyr_forwardshockwave |
| Custom/Bonemass | 56 | 19 | True | Bonemass, CreepProp_egg_hanging01, CreepProp_egg_hanging02, CreepProp_entrance1, CreepProp_entrance2 |
| Custom/Grass | 48 | 10 | True | Barley, Pickable_Barley, Pickable_Barley_Wild, Pickable_Flax, Pickable_Flax_Wild |
| Custom/FlowOpaque | 42 | 17 | True | AxeJotunBane, BlobLava, BombBlob_Lava, BombBlob_Lava_projectile, FimbulvinterOrb |
| Unlit/WeaponGlow | 40 | 30 | True | AxeBerzerkrBlood, AxeBerzerkrLightning, AxeBerzerkrNature, BowAshlandsBlood, BowAshlandsRoot |
| Custom/AlphaParticle | 37 | 26 | True | Aspect_Elder, Aspect_SeekerQueen, SeekerQueen, SeekerQueen_projectile_spit, SeekerQueen_projectile_teleport |
| Custom/Rug | 30 | 13 | True | jute_carpet, jute_carpet_blue, rug_Bjorn, rug_asksvin, rug_bogwitch_deer |
| Custom/Distortion | 26 | 15 | True | Aspect_Bonemass, Aspect_Eikthyr, Aspect_Elder, Aspect_Fader, Aspect_Moder |
| Legacy Shaders/Particles/Alpha Blended Premultiply | 26 | 21 | True | Aspect_Yagluth, Catapult_Ammo_BloodGold_Projectile, Catapult_Ammo_Projectile, Catapult_projectile, FireFlies |
| Custom/Decal | 23 | 16 | True | MountainKit_wood_gate, SeekerQueen_projectile_spit, SeekerQueen_projectile_teleport, SeekerQueen_spithit, aspect_SeekerQueen_projectile_spit |
| Custom/Flow | 17 | 5 | True | FrozenKing, FrozenKing_p2, FrozenKing_p3, dvergrprops_crate, dvergrprops_crate_ashlands |
| Autodesk Interactive | 16 | 5 | True | MeadPoisonResist, MeadStaminaLingering, MeadStaminaMedium, MeadStaminaMinor, SwordIronFire |
| Custom/Player | 14 | 8 | True | ArmorAshlandsMediumlegs, ArmorLeatherChest, ArmorRagsChest, ArmorStand, ArmorTrollLeatherChest |
| Particles/Standard Unlit | 12 | 10 | True | FistGold_BloodLightning, FistGold_FrostFire, FrostWisp, bombdynamite_projectile, fx_JotunWitch_Dodge |
| Custom/Mesh Flipbook Particle | 8 | 8 | True | JotunWitch_projectile_lightningbolt, fx_JotunWitch_LightningBolt_Explosion, fx_JotunWitch_LightningBolt_Trigger, fx_chainlightning_spread, fx_chainlightning_spread_red |
| Custom/WaterMask | 6 | 4 | True | Karve, Trailership, VikingShip, VikingShip_Ashlands |
| Custom/Water | 3 | 3 | True | blackforge_ext1, piece_barber, piece_bathtub |
| Unlit/BeaconBeam | 2 | 2 | True | Charred_altar_bellfragment, fx_Fading_Bellfragment_Beam |
| Lux Lit Particles/ Tess Bumped | 2 | 2 | True | mistvolume, vfx_mistlands_mist |
| Custom/Yggdrasil_root | 2 | 1 | True | YggdrasilRoot |
| Unlit/Invis | 1 | 1 | True | piece_shieldgenerator |

## 7. Layers and hit masks

| Index | Name |
| --- | --- |
| 0 | Default |
| 1 | TransparentFX |
| 2 | Ignore Raycast |
| 3 | - |
| 4 | Water |
| 5 | UI |
| 6 | - |
| 7 | - |
| 8 | effect |
| 9 | character |
| 10 | piece |
| 11 | terrain |
| 12 | item |
| 13 | ghost |
| 14 | character_trigger |
| 15 | static_solid |
| 16 | piece_nonsolid |
| 17 | character_ghost |
| 18 | hitbox |
| 19 | skybox |
| 20 | Default_small |
| 21 | WaterVolume |
| 22 | weapon |
| 23 | blocker |
| 24 | pathblocker |
| 25 | viewblock |
| 26 | character_net |
| 27 | character_noenv |
| 28 | vehicle |
| 29 | InstanceRenderer |
| 30 | - |
| 31 | smoke |

`Physics.queriesHitTriggers` = True. Per decompiled Attack.cs: `DoAreaAttack` uses OverlapSphere with `QueryTriggerInteraction.UseGlobal`, `DoMeleeAttack` uses SphereCast/Raycast with `QueryTriggerInteraction.Ignore` (melee sweeps never hit triggers). Aoe uses OverlapBox/OverlapSphere (UseGlobal) plus OnTriggerEnter/Stay when `m_useTriggers`. Projectile uses SphereCastAll/RaycastAll (UseGlobal).

### Masks

Static masks are lazily initialised by vanilla (0 until the first attack/projectile); the reconstructed value repeats the layer list from the decompiled source.

| Mask | Current value | Reconstructed | Layers |
| --- | --- | --- | --- |
| Attack.m_attackMask | 0 | 471303681 | 0:Default, 9:character, 10:piece, 15:static_solid, 16:piece_nonsolid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| Attack.m_attackMaskTerrain | 0 | 471305729 | 0:Default, 9:character, 10:piece, 11:terrain, 15:static_solid, 16:piece_nonsolid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| Attack.m_attackMaskCharacters | 0 | 470155776 | 9:character, 17:character_ghost, 18:hitbox, 26:character_net, 27:character_noenv, 28:vehicle |
| Projectile.s_rayMaskSolids | 0 | 471305729 | 0:Default, 9:character, 10:piece, 11:terrain, 15:static_solid, 16:piece_nonsolid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| Aoe.m_rayMask (hitCharacters part) | per instance | 67240448 | 9:character, 17:character_ghost, 26:character_net |
| Aoe.m_rayMask (hitProps part) | per instance | 403997697 | 0:Default, 10:piece, 15:static_solid, 18:hitbox, 20:Default_small, 27:character_noenv, 28:vehicle |
| Aoe.m_rayMask (hitTerrain part) | per instance | 2048 | 11:terrain |

### Attack setup of a few vanilla items

| Item | Attack | Type | HitTerrain | Range | RayWidth | Projectile | Projectile setup |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SwordBronze | primary | Horizontal | True | 2.4 | 0.5 | - | - |
| SwordBronze | secondary | Horizontal | True | 2.7 | 0.7 | - | - |
| AtgeirBronze | primary | Horizontal | True | 3.2 | 0.3 | flintspear_projectile | rayRadius 0, aoe 0, hitFriendly False, ttl 10, layer Default, spawnOnHit - |
| AtgeirBronze | secondary | Horizontal | True | 3 | 0.3 | - | - |
| SpearBronze | primary | Horizontal | True | 1.9 | 0.5 | - | - |
| SpearBronze | secondary | Projectile | True | 1 | 0 | bronzespear_projectile | rayRadius 0, aoe 0, hitFriendly False, ttl 10, layer Default, spawnOnHit - |
| SledgeIron | primary | Area | True | 2 | 4 | - | - |
| SledgeIron | secondary | Horizontal | True | 1.5 | 0 | - | - |
| BowFineWood | primary | Projectile | True | 1 | 0 | - | - |
| BowFineWood | secondary | Horizontal | True | 1.5 | 0 | - | - |
| ArrowWood | primary | Horizontal | True | 2.4 | 0 | bow_projectile | rayRadius 0, aoe 0, hitFriendly False, ttl 20, layer Default, spawnOnHit - |
| ArrowWood | secondary | Horizontal | True | 1.5 | 0 | - | - |
| ArrowFire | primary | Horizontal | True | 2.4 | 0 | bow_projectile_fire | rayRadius 0, aoe 0, hitFriendly False, ttl 20, layer Default, spawnOnHit - |
| ArrowFire | secondary | Horizontal | True | 1.5 | 0 | - | - |
| StaffFireball | primary | Projectile | True | 2 | 0 | staff_fireball_projectile | rayRadius 0.1, aoe 3, hitFriendly False, ttl 20, layer Default, spawnOnHit - |
| StaffFireball | secondary | Horizontal | True | 1.5 | 0 | - | - |

### Aoe prefabs (first 15)

| Prefab | UseTriggers | HitCharacters | HitProps | HitTerrain | Radius | Collider | Mask layers |
| --- | --- | --- | --- | --- | --- | --- | --- |
| aoe_nova | False | True | False | False | 10 | - | 9:character, 17:character_ghost, 26:character_net |
| Ashlands_Fortress_Gate_Door | True | True | False | False | 4 | BoxCollider trigger | 9:character, 17:character_ghost, 26:character_net |
| Ashlands_Fortress_Wall_Spikes | True | True | False | False | 4 | BoxCollider trigger | 9:character, 17:character_ghost, 26:character_net |
| Asksvin | True | True | True | False | 4 | SphereCollider trigger | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| Asksvin_hatchling | True | True | True | False | 4 | SphereCollider trigger | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| aspect_aoe_explosion | False | True | False | False | 130 | - | 9:character, 17:character_ghost, 26:character_net |
| aspect_aoe_nova | False | True | False | False | 10 | - | 9:character, 17:character_ghost, 26:character_net |
| aspect_bonemass_aoe | False | True | False | False | 9 | SphereCollider trigger | 9:character, 17:character_ghost, 26:character_net |
| Aspect_Fader | True | False | True | False | 4 | BoxCollider trigger | 0:Default, 10:piece, 15:static_solid, 18:hitbox, 20:Default_small, 27:character_noenv, 28:vehicle |
| aspect_Fader_Fissure_AOE | False | True | True | False | 11 | - | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| aspect_Fader_Flamebreath_AOE | True | True | True | False | 4 | BoxCollider trigger | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| aspect_Fader_WallOfFire_AOE | True | True | True | False | 4 | SphereCollider trigger | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| Aspect_SeekerQueen | True | False | True | False | 4 | BoxCollider trigger | 0:Default, 10:piece, 15:static_solid, 18:hitbox, 20:Default_small, 27:character_noenv, 28:vehicle |
| BatteringRam | False | True | True | False | 1.1 | - | 0:Default, 9:character, 10:piece, 15:static_solid, 17:character_ghost, 18:hitbox, 20:Default_small, 26:character_net, 27:character_noenv, 28:vehicle |
| bee_aoe | False | True | False | False | 4 | - | 9:character, 17:character_ghost, 26:character_net |

## 8. Engine and game versions

| Key | Value |
| --- | --- |
| Application.unityVersion | 6000.0.75f1 |
| Application.version | 1.0 |
| Version.CurrentVersion | 1.0.20 |
| Version.GetVersionString(true) | 1.0.20 e3ee4a778ed0 |
| Jotunn | 2.30.2 |
| BepInEx | 5.4.23.5 |
| SystemInfo.supportsInstancing | True |
| SystemInfo.graphicsDeviceType | Direct3D11 |
| SystemInfo.graphicsDeviceName | NVIDIA GeForce RTX 3060 |
| SystemInfo.graphicsDeviceVersion | Direct3D 11.0 [level 11.1] |
| SystemInfo.operatingSystem | Windows 11  (10.0.26300) |
| Render pipeline | built-in |
| Graphics.RenderMeshInstanced available | True |
| Dedicated server | False |


## 6b. Deferred render check

Each probe material is drawn alone for 6 frames; warnings and errors logged from any thread during its window are attributed to it.

| Shader | Result |
| --- | --- |
| Custom/Creature | ok (no warnings/errors) |
| Custom/Piece | ok (no warnings/errors) |
| Standard | ok (no warnings/errors) |
| Legacy Shaders/Diffuse | ok (no warnings/errors) |
| Custom/Vegetation | ok (no warnings/errors) |
| Custom/Player | ok (no warnings/errors) |
