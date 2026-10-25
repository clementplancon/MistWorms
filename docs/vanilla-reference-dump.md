# MistWorms vanilla dump

Generated 2026-10-25 06:14:57 by `VanillaDump` (MistWorms 0.1.0). Valheim `1.0.20`, Unity `6000.0.75f1`. Raw output, to be curated into `docs/vanilla-reference.md`.

## 1. Reference creatures

### Stats

| Prefab | m_name | Health | Speed | Walk | Run | Turn | RunTurn | Faction | Group | Flying | Boss | TolerateFire |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | $enemy_aspect_seekerqueen (Aspect de la Matriarche rampante) | 1700 | 4 | 2 | 8 | 50 | 120 | Boss |  | False | False | False |
| Blob | $enemy_blob (Blob) | 50 | 1 | 5 | 2 | 100 | 100 | Undead |  | False | False | False |
| BlobAspect | $enemy_blob (Blob) | 50 | 1 | 5 | 2 | 100 | 100 | Undead |  | False | False | False |
| BlobElite | $enemy_blobelite (Oozer) | 150 | 1 | 5 | 2 | 100 | 100 | Undead |  | False | False | False |
| BlobFrost | $enemy_blobfrost (Blob de givre) | 50 | 1 | 5 | 2 | 100 | 100 | MountainMonsters |  | False | False | False |
| BlobLava | $enemy_bloblava (Blob de lave) | 300 | 1 | 5 | 2 | 100 | 100 | Demon |  | False | False | True |
| BlobMork | $enemy_blobmork (Pâte sans forme) | 150 | 1 | 5 | 2 | 100 | 100 | DeepNorth |  | False | False | False |
| BlobMorkMini | $enemy_blobmorkmini (Petite pâte) | 50 | 2 | 10 | 4 | 100 | 100 | DeepNorth |  | False | False | False |
| BlobTar | $enemy_blobtar (Excroissance) | 100 | 1 | 5 | 3 | 100 | 100 | Undead |  | False | False | False |
| Leech | $enemy_leech (Sangsue) | 60 | 2 | 5 | 2 | 50 | 50 | Undead |  | False | False | False |
| Leech_cave | $enemy_leech (Sangsue) | 60 | 2 | 5 | 2 | 50 | 50 | Undead |  | False | False | False |
| Seeker | $enemy_seeker (Fouineur) | 200 | 2 | 5 | 6 | 150 | 250 | MistlandsMonsters |  | False | False | False |
| SeekerBrood | $enemy_babyseeker (Jeune fouineur) | 20 | 1 | 1 | 3 | 150 | 250 | MistlandsMonsters |  | False | False | False |
| SeekerBrute | $enemy_seekerbrute (Soldat fouineur) | 1500 | 2 | 2 | 6 | 80 | 150 | MistlandsMonsters |  | False | False | False |
| SeekerQueen | $enemy_seekerqueen (La Reine) | 12500 | 4 | 2 | 8 | 50 | 120 | Boss |  | False | True | False |
| Tick | $enemy_tick (Tique) | 50 | 1 | 5 | 8 | 300 | 400 | MistlandsMonsters |  | False | False | False |

### Perception and AI

| Prefab | AI | ViewRange | ViewAngle | HearRange | AlertRange | AfraidOfFire | AvoidFire | PathAgentType | AttackPlayerObjects | IdleSound (interval, chance) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | MonsterAI | 30 | 90 | 9999 | 30 | False | False | SeekerQueen | True | sfx_HiveQueen_idle (5 s, 0.5) |
| Blob | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobAspect | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobElite | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobFrost | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobLava | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_bloblava_idle (5 s, 0.5) |
| BlobMork | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobMorkMini | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | HumanoidNoSwim | False | sfx_blob_idle (10 s, 0.25) |
| BlobTar | MonsterAI | 30 | 90 | 9999 | 9999 | False | False | Humanoid | True | sfx_blobtar_idle (10 s, 0.25) |
| Leech | MonsterAI | 30 | 360 | 9999 | 20 | False | False | Fish | False | sfx_leech_idle (10 s, 0.5) |
| Leech_cave | MonsterAI | 30 | 360 | 9999 | 20 | False | False | Fish | False | sfx_leech_idle (10 s, 0.5) |
| Seeker | MonsterAI | 30 | 120 | 9999 | 30 | False | False | Humanoid | True | sfx_seeker_idle, sfx_seeker_overlay (5 s, 0.5) |
| SeekerBrood | MonsterAI | 30 | 120 | 9999 | 30 | False | False | Humanoid | True | sfx_baby_seeker_idle (5 s, 0.5) |
| SeekerBrute | MonsterAI | 30 | 90 | 9999 | 30 | False | False | HumanoidBig | True | sfx_seeker_brute_idle (10 s, 0.5) |
| SeekerQueen | MonsterAI | 30 | 90 | 9999 | 30 | False | False | SeekerQueen | True | sfx_HiveQueen_idle (5 s, 0.5) |
| Tick | MonsterAI | 30 | 90 | 9999 | 20 | False | False | HumanoidNoSwim | True | sfx_tick_idle (6 s, 0.5) |

### Damage modifiers

| Prefab | blunt | slash | pierce | chop | pickaxe | fire | frost | lightning | poison | spirit | nonPlayer |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Aspect_SeekerQueen | Normal | Normal | Resistant | Ignore | Ignore | Normal | Normal | Normal | Normal | Immune | Immune |
| Blob | Weak | Resistant | Resistant | Ignore | Ignore | Resistant | Weak | Weak | Immune | Normal | Normal |
| BlobAspect | Weak | Resistant | Resistant | Ignore | Ignore | Resistant | Weak | Weak | Immune | Normal | Normal |
| BlobElite | Weak | Resistant | Resistant | Ignore | Ignore | Resistant | Weak | Weak | Immune | Normal | Normal |
| BlobFrost | Weak | Resistant | Resistant | Ignore | Ignore | Weak | VeryResistant | Weak | Immune | Normal | Normal |
| BlobLava | Weak | Normal | Resistant | Ignore | Ignore | Immune | Weak | Weak | Resistant | Normal | Normal |
| BlobMork | Weak | Resistant | Resistant | Ignore | Ignore | Resistant | Weak | Weak | Immune | Normal | Normal |
| BlobMorkMini | Weak | Resistant | Resistant | Ignore | Ignore | Resistant | Weak | Weak | Immune | Normal | Normal |
| BlobTar | Weak | Resistant | Resistant | Ignore | Ignore | Weak | Resistant | Normal | Immune | Normal | Normal |
| Leech | Normal | Normal | Normal | Ignore | Ignore | Immune | Normal | Normal | Resistant | Immune | Normal |
| Leech_cave | Normal | Normal | Normal | Ignore | Ignore | Immune | Normal | Normal | Resistant | Immune | Normal |
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
| Blob | 1 x 1 x 1 | 2.079 x 1.257 x 1.885 (min y -0.242) | 2.079 x 1.257 x 1.885 | Blob: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobAspect | 1 x 1 x 1 | 2.079 x 1.257 x 1.885 (min y -0.242) | 2.079 x 1.257 x 1.885 | BlobAspect: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobElite | 1 x 1 x 1 | 3.048 x 1.843 x 2.765 (min y -0.539) | 3.048 x 1.843 x 2.765 | BlobElite: CapsuleCollider r 1.2 h 1 dir 1 center 0 x 1 x -0.048 layer character scale 1 x 1 x 1 |
| BlobFrost | 1 x 1 x 1 | 2.079 x 1.257 x 1.885 (min y -0.242) | 2.079 x 1.257 x 1.885 | BlobFrost: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobLava | 1 x 1 x 1 | 2.978 x 2.915 x 3.394 (min y -0.412) | 2.978 x 2.915 x 3.394 | BlobLava: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobMork | 1 x 1 x 1 | 2.079 x 1.257 x 1.885 (min y -0.242) | 2.079 x 1.257 x 1.885 | BlobMork: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobMorkMini | 1 x 1 x 1 | 1.039 x 0.628 x 0.943 (min y -0.121) | 1.039 x 0.628 x 0.943 | BlobMorkMini: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| BlobTar | 1 x 1 x 1 | 2.079 x 1.257 x 1.885 (min y -0.242) | 2.079 x 1.257 x 1.885 | BlobTar: CapsuleCollider r 0.78 h 1.5 dir 1 center 0 x 0.77 x -0.048 layer character scale 1 x 1 x 1 |
| Leech | 1 x 1 x 1 | 0.58 x 0.679 x 4.58 (min y -0.116) | 0.58 x 0.679 x 4.58 | Leech: CapsuleCollider r 0.3 h 1.5 dir 2 center 0 x 0.3 x 0 layer character scale 1 x 1 x 1; attack_hitbox: SphereCollider r 0.8 center 0 x 0 x 0 layer hitbox scale 1 x 1 x 1 |
| Leech_cave | 1 x 1 x 1 | 0.58 x 0.679 x 4.58 (min y -0.116) | 0.58 x 0.679 x 4.58 | Leech_cave: CapsuleCollider r 0.3 h 1.5 dir 2 center 0 x 0.3 x 0 layer character scale 1 x 1 x 1; attack_hitbox: SphereCollider r 0.8 center 0 x 0 x 0 layer hitbox scale 1 x 1 x 1 |
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
| Visual/Plane | SkinnedMeshRenderer | Plane (1032 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_SRGB), _BumpMap=SeekerQueen_n (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_UNorm) | _TWOSIDEDNORMALS_ON | True | 2000 |

Material values `Aspect_mat` (renderer Visual/Cube, shader `Custom/Distortion`). Active keywords: [_GRADIENTASALPHA_ON].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(0.277, 0.525, 0.66, 0.992) |
| _MainTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NormalTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.429 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _NormalScale | Float | 1 |
| _RefractionIntensity | Float | 2 |
| _WaveVel | Float | 1 |
| _DepthFade | Float | 1 |

Material values `seeker_queen` (renderer Visual/Plane, shader `Custom/Creature`). Active keywords: [_TWOSIDEDNORMALS_ON].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | SeekerQueen_d (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | none; scale (0.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 0.099, 0.099, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.364 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | SeekerQueen_n (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 1 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 0 |
| _Cutoff | Range | 0.3 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

### Blob

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blob_attack_aoe | OneHandedWeapon | poison 90 | Area | attack | 0 | 0.5 | 90 | 4 | 2 | 2 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyBlob | 1 | 1 | 0.1 | False | True | False |
| Ooze | 1 | 2 | 1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blob_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blob_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blob | Custom/Blob | _MainTex=blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB), _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blob` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex | Texture | blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (3.00, 3.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.753 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -8 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -10 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(1, 0.89, 0, 1) |
| _EmissiveColor | Color | RGBA(0.302, 0.395, 0.093, 1) |
| _SSS | Color | RGBA(0.188, 0.588, 0.03, 1) |

### BlobAspect

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blob_attack_aoe | OneHandedWeapon | poison 90 | Area | attack | 0 | 0.5 | 90 | 4 | 2 | 2 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyBlob | 1 | 1 | 0.1 | False | True | False |
| Ooze | 1 | 2 | 1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blob_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blob_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blobaspect | Custom/Blob | _MainTex=blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB), _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blobaspect` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex | Texture | blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (3.00, 3.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.753 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -8 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -10 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(0, 0.679, 1, 1) |
| _EmissiveColor | Color | RGBA(0.246, 0.448, 0.736, 0.475) |
| _SSS | Color | RGBA(0.035, 0.48, 0.588, 0.412) |

### BlobElite

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blobelite_attack_aoe | OneHandedWeapon | poison 115 | Area | attack | 0 | 0.5 | 90 | 8 | 3 | 4 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| Ooze | 2 | 3 | 1 | False | True | False |
| IronScrap | 1 | 1 | 0.33 | False | True | False |
| TrophyBlob | 1 | 1 | 0.1 | False | True | False |
| Blob | 2 | 2 | 1 | False | True | True |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blob_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blob_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blob_elite | Custom/Blob | _MainTex=blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB), _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blob_elite` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex | Texture | blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (4.00, 4.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.723 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -6 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -8 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(1, 0, 0, 1) |
| _EmissiveColor | Color | RGBA(0.257, 0.069, 0, 1) |
| _SSS | Color | RGBA(0.485, 0, 0.151, 1) |

### BlobFrost

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blob_frost_attack_aoe | OneHandedWeapon | frost 100 | Area | attack | 0 | 0.5 | 90 | 4 | 2 | 2 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| Crystal | 1 | 2 | 1 | False | True | False |
| TrophyBlob_Frost | 1 | 1 | 0.1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blob_frost_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blob_frost_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | crystal_window | Standard | _BumpMap=crystal_window_n (32x32, filter Point, wrap Repeat, aniso 1, mips 6, format RGBA_DXT5_UNorm) | _ALPHABLEND_ON _NORMALMAP _ADDRAIN_ON | True | 3000 |

Material values `crystal_window` (renderer Visual/blob/Cube, shader `Standard`). Active keywords: [_ALPHABLEND_ON _NORMALMAP _ADDRAIN_ON].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 0.376) |
| _MainTex | Texture | none; scale (6.00, 6.00), offset (0.00, 0.00) |
| _Cutoff | Range | 1 (range 0..1) |
| _Glossiness | Range | 0.9 (range 0..1) |
| _GlossMapScale | Range | 0.5 (range 0..1) |
| _SmoothnessTextureChannel | Float | 0 |
| _Metallic | Range | 1 (range 0..1) |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _SpecularHighlights | Float | 1 |
| _GlossyReflections | Float | 1 |
| _BumpScale | Float | 1.2 |
| _BumpMap | Texture | crystal_window_n (32x32, filter Point, wrap Repeat, aniso 1, mips 6, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Parallax | Range | 0.02 (range 0.005..0.08) |
| _ParallaxMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _OcclusionStrength | Range | 1 (range 0..1) |
| _OcclusionMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0.669, 0.796, 1, 0.639) |
| _EmissionMap | Texture | none; scale (6.00, 6.00), offset (0.00, 0.00) |
| _DetailMask | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _DetailAlbedoMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _DetailNormalMapScale | Float | 1 |
| _DetailNormalMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _UVSec | Float | 0 |
| _Mode | Float | 2 |
| _SrcBlend | Float | 5 |
| _DstBlend | Float | 10 |
| _ZWrite | Float | 0 |

### BlobLava

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blobLava_attack_aoe | OneHandedWeapon | none | Area | AttackBlowUp | 0 | 0.5 | 90 | 4 | 6 | 10 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| ProustitePowder | 1 | 2 | 1 | False | True | False |
| SulfurStone | 1 | 2 | 1 | False | True | False |
| TrophyBlob_Lava | 1 | 1 | 0.1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | sfx_bloblava_death, BlobLava_explosion, fx_blobLava_explosion |
| BaseAI.m_alertedEffects | sfx_bloblava_alert |
| BaseAI.m_idleSound | sfx_bloblava_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube.004 | SkinnedMeshRenderer | Cube.004 (354 v) | LavaBlob_mat | Custom/FlowOpaque | _MainTex=Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _BumpMap=Unstable_n 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm), _FoamTex=Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB) |  | True | 2000 |
| Visual/blob/Cube.004 | SkinnedMeshRenderer | Cube.004 (354 v) | BlobCrust_Mat | Custom/FlowOpaque | _MainTex=Crust_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT1_SRGB), _BumpMap=Crust_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_UNorm), _FoamTex=Crust_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT1_SRGB), _EmissionMap=Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB) |  | True | 2000 |

Material values `LavaBlob_mat` (renderer Visual/blob/Cube.004, shader `Custom/FlowOpaque`). Active keywords: [].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 0.408, 0, 1) |
| _MainTex | Texture | Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (0.50, 0.50), offset (0.00, 0.00) |
| _FoamTex | Texture | Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (0.50, 0.50), offset (0.00, 0.00) |
| _FoamColor | Color | RGBA(1, 0.36, 0, 1) |
| _Glossiness | Range | 0.694 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _SpeedX | Float | 0 |
| _SpeedY | Float | 0.13 |
| _FoamSpeed | Float | 1.09 |
| _BumpMap | Texture | Unstable_n 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Float | 1 |
| _EmissionMap | Texture | none; scale (0.50, 0.50), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(1.498, 0.071, 0, 1) |
| _Cull | Float | 2 |

Material values `BlobCrust_Mat` (renderer Visual/blob/Cube.004, shader `Custom/FlowOpaque`). Active keywords: [].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 0.408, 0, 1) |
| _MainTex | Texture | Crust_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT1_SRGB); scale (0.50, 0.50), offset (0.00, 0.00) |
| _FoamTex | Texture | Crust_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT1_SRGB); scale (0.50, 0.50), offset (0.00, 0.00) |
| _FoamColor | Color | RGBA(1, 0.361, 0, 1) |
| _Glossiness | Range | 0.48 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _SpeedX | Float | 0 |
| _SpeedY | Float | 0.56 |
| _FoamSpeed | Float | 1.09 |
| _BumpMap | Texture | Crust_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_UNorm); scale (2.00, 2.00), offset (0.00, 0.00) |
| _BumpScale | Float | 1 |
| _EmissionMap | Texture | Unstable_d 1 (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (0.50, 0.50), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(2.039, 0.166, 0, 1) |
| _Cull | Float | 2 |

### BlobMork

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blobmork_attack_aoe | OneHandedWeapon | blunt 20, poison 110 | Area | attack | 0 | 0.5 | 90 | 4 | 2 | 2 | 0 | - | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyBlob_Morkhalla | 1 | 1 | 0.1 | False | False | False |
| BlobMorkMini | 1 | 2 | 1 | False | True | False |
| OozeMork | 1 | 1 | 0.5 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blobmork_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blobmork_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blob_morkhalla | Custom/Blob | _MainTex=blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB), _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blob_morkhalla` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex | Texture | blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (3.00, 3.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.753 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -8 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -10 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(0, 1, 0.741, 1) |
| _EmissiveColor | Color | RGBA(0.094, 0.396, 0.39, 1) |
| _SSS | Color | RGBA(0.031, 0.588, 0.355, 1) |

### BlobMorkMini

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| _(none)_ | | | | | | | | | | | | | | |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| OozeMork | 1 | 1 | 0.25 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blobmork_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blobmork_hit, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blob_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blob_morkhalla | Custom/Blob | _MainTex=blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB), _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blob_morkhalla` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex | Texture | blob_d (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (3.00, 3.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.753 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -8 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -10 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(0, 1, 0.741, 1) |
| _EmissiveColor | Color | RGBA(0.094, 0.396, 0.39, 1) |
| _SSS | Color | RGBA(0.031, 0.588, 0.355, 1) |

### BlobTar

Components: CapsuleCollider, Rigidbody, Humanoid, CharacterDrop, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, FootStep

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | blobtar_attack | OneHandedWeapon | blunt 45, poison 50 | Projectile | attack_shoot | 1.6 | 0.8 | 90 | 0 | 15 | 8 | 80 | blobtar_projectile_tarball | vfx_HitSparks, sfx_greydwarf_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyGrowth | 1 | 1 | 0.1 | False | True | False |
| Tar | 1 | 1 | 1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | vfx_blobtar_hit, sfx_blob_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_blobtar_death, sfx_blob_death |
| BaseAI.m_alertedEffects | sfx_blob_alerted |
| BaseAI.m_idleSound | sfx_blobtar_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/blob/Cube | SkinnedMeshRenderer | Cube (120 v) | blob_tar | Custom/Blob | _BumpMap=blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm), _FlowTexture=blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB), _FlowMaskTex=blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB) | _NORMALMAP | True | 2000 |

Material values `blob_tar` (renderer Visual/blob/Cube, shader `Custom/Blob`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _Color | Color | RGBA(0, 0, 0, 1) |
| _MainTex | Texture | none; scale (3.00, 3.00), offset (0.00, 0.00) |
| _Glossiness | Range | 0.925 (range 0..1) |
| _Metallic | Range | 0 (range 0..1) |
| _BumpScale | Float | 0.84 |
| _BumpMap | Texture | blob_n (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedAll | Float | -4 |
| _FlowMaskTex | Texture | blob_flowmask (128x128, filter Point, wrap Repeat, aniso 1, mips 1, format RGBA_BC7_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowSpeedY | Float | -7 |
| _FlowTexture | Texture | blob_flow_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _FlowColor | Color | RGBA(1, 0.89, 0, 1) |
| _EmissiveColor | Color | RGBA(0, 0, 0, 1) |
| _SSS | Color | RGBA(0, 0, 0, 1) |

### Leech

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | Leech_BiteAttack | OneHandedWeapon | pierce 20, poison 70 | Horizontal | attack | 1.5 | 0.51 | 48.5 | 0.45 | 1.5 | 2 | 30 | - | vfx_HitSparks, sfx_leech_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyLeech | 1 | 1 | 0.1 | False | True | False |
| Bloodbag | 1 | 1 | 1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | sfx_leech_hit, vfx_leech_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_leech_death, sfx_leech_death |
| BaseAI.m_alertedEffects | sfx_leech_alerted |
| BaseAI.m_idleSound | sfx_leech_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Cube | SkinnedMeshRenderer | Cube (218 v) | swampfish | Custom/Creature | _MainTex=swampfish_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB), _BumpMap=swampfish_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_BC7_UNorm) | _NORMALMAP | False | 2000 |

Material values `swampfish` (renderer Visual/Cube, shader `Custom/Creature`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | swampfish_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 1, 1, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.54 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | swampfish_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 0 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 2 |
| _Cutoff | Range | 0.5 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

### Leech_cave

Components: CapsuleCollider, Rigidbody, Humanoid, ZNetView, ZSyncTransform, ZSyncAnimation, MonsterAI, CharacterDrop

Attacks (Humanoid items):

| Source | Item | Type | Damages | AttackType | Anim | Range | Height | Angle | RayWidth | AI range | AI interval | Force | Projectile | Hit effects |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default | Leech_BiteAttack | OneHandedWeapon | pierce 20, poison 70 | Horizontal | attack | 1.5 | 0.51 | 48.5 | 0.45 | 1.5 | 2 | 30 | - | vfx_HitSparks, sfx_leech_attack_hit |

Drops (CharacterDrop):

| Item | Min | Max | Chance | OnePerPlayer | LevelMultiplier | DontScale |
| --- | --- | --- | --- | --- | --- | --- |
| TrophyLeech | 1 | 1 | 0.1 | False | False | False |
| Bloodbag | 1 | 1 | 1 | False | True | False |

Effects:

| List | Prefabs |
| --- | --- |
| m_hitEffects | sfx_leech_hit, vfx_leech_hit |
| m_critHitEffects | fx_crit |
| m_backstabHitEffects | fx_backstab |
| m_deathEffects | vfx_leech_death, sfx_leech_death |
| BaseAI.m_alertedEffects | sfx_leech_alerted |
| BaseAI.m_idleSound | sfx_leech_idle |
| MonsterAI.m_wakeupEffects | - |
| MonsterAI.m_sleepEffects | - |

Renderers and materials:

| Renderer | Type | Mesh | Material | Shader | Textures | Keywords | Instancing | Queue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Visual/Cube | SkinnedMeshRenderer | Cube (218 v) | swampfish_cave | Custom/Creature | _MainTex=swampfish_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB), _BumpMap=swampfish_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_BC7_UNorm) | _NORMALMAP | False | 2000 |

Material values `swampfish_cave` (renderer Visual/Cube, shader `Custom/Creature`). Active keywords: [_NORMALMAP].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | swampfish_d (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | -0.75 (range -1..1) |
| _Value | Range | 0.5 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 1, 1, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.54 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | swampfish_n (128x128, filter Point, wrap Repeat, aniso 1, mips 8, format RGBA_BC7_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 0 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 2 |
| _Cutoff | Range | 0.5 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

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
| Visual/Plane.004 | SkinnedMeshRenderer | Plane.004 (4259 v) | seeker | Custom/Creature | _MainTex=seeker_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_SRGB), _BumpMap=seeker_n (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm), _EmissionMap=blood (128x128, filter Bilinear, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB) | _SNAPNOISETOPIXEL_ON _TWOSIDEDNORMALS_ON | True | 2000 |

Material values `seeker` (renderer Visual/Plane.004, shader `Custom/Creature`). Active keywords: [_SNAPNOISETOPIXEL_ON _TWOSIDEDNORMALS_ON].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | seeker_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | blood (128x128, filter Bilinear, wrap Repeat, aniso 1, mips 8, format RGBA_DXT5_SRGB); scale (0.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 0.222, 0.222, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.55 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | seeker_n (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 1 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 0 |
| _Cutoff | Range | 0.779 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

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
| Visual/Plane.004 | SkinnedMeshRenderer | Plane.004 (4259 v) | babyseeker | Custom/Creature | _MainTex=babyseeker_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB), _BumpMap=babyseeker_n (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_UNorm), _EmissionMap=babyseeker_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB) | _TWOSIDEDNORMALS_ON _USEGLOSSMAP_ON | True | 2000 |

Material values `babyseeker` (renderer Visual/Plane.004, shader `Custom/Creature`). Active keywords: [_TWOSIDEDNORMALS_ON _USEGLOSSMAP_ON].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | babyseeker_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB); scale (4.00, 4.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 0.915, 0.552, 1) |
| _MainTex_ST | Vector | RGBA(4, 4, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | babyseeker_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB); scale (0.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0.156, 0.031, 0.016, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 0.222, 0.222, 1) |
| _UseGlossmap | Float | 1 |
| _Glossiness | Range | 0.76 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | babyseeker_n (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 1 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 0 |
| _Cutoff | Range | 0.779 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

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
| Visual/SeekerBrute | SkinnedMeshRenderer | SeekerBrute (4042 v) | seeker_Brute_mat | Custom/Creature | _MainTex=seekerBrute_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_SRGB), _BumpMap=seekerBrute_n (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm), _EmissionMap=seekerBrute_e (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB) | _TWOSIDEDNORMALS_ON | True | 2000 |

Material values `seeker_Brute_mat` (renderer Visual/SeekerBrute, shader `Custom/Creature`). Active keywords: [_TWOSIDEDNORMALS_ON].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | seekerBrute_d (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | seekerBrute_e (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT1_SRGB); scale (0.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0.326, 0, 0.034, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 0.099, 0.099, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.55 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | seekerBrute_n (256x256, filter Point, wrap Repeat, aniso 1, mips 9, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 1 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 0 |
| _Cutoff | Range | 0.779 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

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
| Visual/Cube | SkinnedMeshRenderer | Cube (7308 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_SRGB), _BumpMap=SeekerQueen_n (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_UNorm) | _TWOSIDEDNORMALS_ON | True | 2000 |
| Visual/Plane | SkinnedMeshRenderer | Plane (1032 v) | seeker_queen | Custom/Creature | _MainTex=SeekerQueen_d (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_SRGB), _BumpMap=SeekerQueen_n (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_UNorm) | _TWOSIDEDNORMALS_ON | True | 2000 |

Material values `seeker_queen` (renderer Visual/Cube, shader `Custom/Creature`). Active keywords: [_TWOSIDEDNORMALS_ON].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | SeekerQueen_d (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 1, 1, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | none; scale (0.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 0.099, 0.099, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.364 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | SeekerQueen_n (512x512, filter Point, wrap Repeat, aniso 1, mips 10, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 1 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 0 |
| _Cutoff | Range | 0.3 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

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
| Visual/FeastingMesh | SkinnedMeshRenderer | FeastingMesh (1960 v) | Feasting_mat | Custom/Creature | _MainTex=Feasting_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB), _BumpMap=Feasting_n (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_UNorm), _MetallicGlossMap=Feasting_m (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB) |  | True | 2000 |

Material values `Feasting_mat` (renderer Visual/FeastingMesh, shader `Custom/Creature`). Active keywords: [].

| Property | Type | Value |
| --- | --- | --- |
| _ColorFoldout | Float | 0 |
| _MainTex | Texture | Feasting_d (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Color | Color | RGBA(1, 0.675, 0.675, 1) |
| _MainTex_ST | Vector | RGBA(1, 1, 0, 0) |
| _HSVHeader | Float | 0 |
| _Hue | Range | 0 (range -0.5..0.5) |
| _Saturation | Range | 0 (range -1..1) |
| _Value | Range | 0 (range -1..1) |
| _EmissionSpacer | Float | 0 |
| _EmissionMap | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _EmissionColor | Color | RGBA(0, 0, 0, 1) |
| _SmoothnessFoldout | Float | 0 |
| _MetallicGlossMap | Texture | Feasting_m (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_SRGB); scale (1.00, 1.00), offset (0.00, 0.00) |
| _Metallic | Range | 0 (range 0..1) |
| _MetalColor | Color | RGBA(1, 1, 1, 1) |
| _UseGlossmap | Float | 0 |
| _Glossiness | Range | 0.422 (range 0..1) |
| _MetalGloss | Range | 0 (range 0..1) |
| _NormalFoldout | Float | 0 |
| _BumpMap | Texture | Feasting_n (64x64, filter Point, wrap Repeat, aniso 1, mips 7, format RGBA_DXT5_UNorm); scale (1.00, 1.00), offset (0.00, 0.00) |
| _BumpScale | Range | 1 (range 0..10) |
| _TwoSidedNormals | Float | 0 |
| _MiscFoldout | Float | 0 |
| _Cull | Float | 2 |
| _Cutoff | Range | 0.466 (range 0..1) |
| _AddRain | Float | 0 |
| _NoiseGlowEnabled | Float | 0 |
| _UseStyles | Float | 0 |
| _SnowCover | Range | 0 (range 0..1) |
| _StylesFoldout | Float | 0 |
| _StyleTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _Style | Float | 0 |
| _NoiseGlowFoldout | Float | 0 |
| _SnapNoiseToPixel | Float | 1 |
| _NoiseGlowTex | Texture | none; scale (1.00, 1.00), offset (0.00, 0.00) |
| _NoiseGlowSize | Float | 3 |
| _NoiseGlowColor | Color | RGBA(1, 1, 1, 1) |
| _NoiseGlowEdge | Range | 0 (range 0..1) |

### Other prefabs matching Seeker / Tick / Leech / Blob

| Prefab | Components |
| --- | --- |
| BlobLava_explosion | Aoe, ZNetView, ZSyncTransform |
| BlobMorkBig | LODGroup, ZNetView, Destructible, SpawnArea, HoverText, StaticPhysics, DropOnDestroyed |
| BlobVial | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_Frost | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_Frost_projectile | Projectile, ZNetView, ZSyncTransform |
| BombBlob_Lava | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_Lava_projectile | Projectile, ZNetView, ZSyncTransform |
| BombBlob_Morkhalla | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_Morkhalla_projectile | Projectile, ZNetView, ZSyncTransform |
| BombBlob_Poison | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_PoisonElite | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_PoisonElite_projectile | Projectile, ZNetView, ZSyncTransform |
| BombBlob_Poison_projectile | Projectile, ZNetView, ZSyncTransform |
| BombBlob_Tar | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| BombBlob_Tar_projectile | Projectile, ZNetView, ZSyncTransform |
| SeekerAspic | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, Piece, WearNTear |
| SeekerBrute_Taunt | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerEgg | LODGroup, ZNetView, Destructible, DropOnDestroyed, EggHatch |
| SeekerEgg_alwayshatch | LODGroup, ZNetView, Destructible, DropOnDestroyed, EggHatch |
| SeekerQueen_Call | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerQueen_Teleport | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| SeekerQueen_projectile_spit | Projectile, ZNetView, ZSyncTransform |
| SeekerQueen_projectile_teleport | ZNetView, TeleportAbility |
| SeekerQueen_spithit | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction |
| Spawner_Blob | ZNetView, CreatureSpawner |
| Spawner_BlobElite | ZNetView, CreatureSpawner |
| Spawner_BlobTar | ZNetView, CreatureSpawner |
| Spawner_BlobTar_respawn_30 | ZNetView, CreatureSpawner |
| Spawner_Leech_cave | ZNetView, CreatureSpawner |
| Spawner_Seeker | ZNetView, CreatureSpawner |
| Spawner_SeekerBrute | ZNetView, CreatureSpawner |
| Spawner_SeekerBrute_respawn_240 | ZNetView, CreatureSpawner |
| Spawner_Seeker_respawn_240 | ZNetView, CreatureSpawner |
| Spawner_Tick | ZNetView, CreatureSpawner |
| Spawner_Tick_stared | ZNetView, CreatureSpawner |
| Spawner_Tick_stared_respawn_240 | ZNetView, CreatureSpawner |
| TriggerSpawner_Seeker | ZNetView, TriggerSpawner, LODGroup |
| TrophyBlob | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophyBlob_Frost | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophyBlob_Lava | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophyBlob_Morkhalla | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophyLeech | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophySeeker | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophySeekerBrute | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| TrophySeekerQueen | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem, Floating |
| TrophyTick | Rigidbody, ZNetView, ZSyncTransform, ItemDrop, ParticleSystemRenderer, ParticleSystem |
| aspect_SeekerQueen_projectile_spit | Projectile, ZNetView, ZSyncTransform |
| fx_TickBloodHit | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction |
| prop_itemstand_TrophySeekerBrute | ZNetView, LODGroup, Destructible, DropOnDestroyed |
| vfx_BigBlob_destroyed | Gibber, ZNetView |
| vfx_BombBlob_explode_frost | ZNetView, TimedDestruction |
| vfx_BombBlob_explode_lava | ZNetView, TimedDestruction |
| vfx_BombBlob_explode_morkhalla | ZNetView, TimedDestruction |
| vfx_BombBlob_explode_poison | ZNetView, TimedDestruction |
| vfx_BombBlob_explode_poisonelite | ZNetView, TimedDestruction |
| vfx_BombBlob_explode_tar | ZNetView, TimedDestruction |

### Shader properties (creature materials)

`Custom/Distortion` (9 properties): `_Color` Color, `_MainTex` Texture, `_NormalTex` Texture, `_Glossiness` Range, `_Metallic` Range, `_NormalScale` Float, `_RefractionIntensity` Float, `_WaveVel` Float, `_DepthFade` Float

`Custom/Creature` (38 properties): `_ColorFoldout` Float, `_MainTex` Texture, `_Color` Color, `_MainTex_ST` Vector, `_HSVHeader` Float, `_Hue` Range, `_Saturation` Range, `_Value` Range, `_EmissionSpacer` Float, `_EmissionMap` Texture, `_EmissionColor` Color, `_SmoothnessFoldout` Float, `_MetallicGlossMap` Texture, `_Metallic` Range, `_MetalColor` Color, `_UseGlossmap` Float, `_Glossiness` Range, `_MetalGloss` Range, `_NormalFoldout` Float, `_BumpMap` Texture, `_BumpScale` Range, `_TwoSidedNormals` Float, `_MiscFoldout` Float, `_Cull` Float, `_Cutoff` Range, `_AddRain` Float, `_NoiseGlowEnabled` Float, `_UseStyles` Float, `_SnowCover` Range, `_StylesFoldout` Float, `_StyleTex` Texture, `_Style` Float, `_NoiseGlowFoldout` Float, `_SnapNoiseToPixel` Float, `_NoiseGlowTex` Texture, `_NoiseGlowSize` Float, `_NoiseGlowColor` Color, `_NoiseGlowEdge` Range

`Custom/Blob` (13 properties): `_Color` Color, `_MainTex` Texture, `_Glossiness` Range, `_Metallic` Range, `_BumpScale` Float, `_BumpMap` Texture, `_FlowSpeedAll` Float, `_FlowMaskTex` Texture, `_FlowSpeedY` Float, `_FlowTexture` Texture, `_FlowColor` Color, `_EmissiveColor` Color, `_SSS` Color

`Standard` (27 properties): `_Color` Color, `_MainTex` Texture, `_Cutoff` Range, `_Glossiness` Range, `_GlossMapScale` Range, `_SmoothnessTextureChannel` Float, `_Metallic` Range, `_MetallicGlossMap` Texture, `_SpecularHighlights` Float, `_GlossyReflections` Float, `_BumpScale` Float, `_BumpMap` Texture, `_Parallax` Range, `_ParallaxMap` Texture, `_OcclusionStrength` Range, `_OcclusionMap` Texture, `_EmissionColor` Color, `_EmissionMap` Texture, `_DetailMask` Texture, `_DetailAlbedoMap` Texture, `_DetailNormalMapScale` Float, `_DetailNormalMap` Texture, `_UVSec` Float, `_Mode` Float, `_SrcBlend` Float, `_DstBlend` Float, `_ZWrite` Float

`Custom/FlowOpaque` (14 properties): `_Color` Color, `_MainTex` Texture, `_FoamTex` Texture, `_FoamColor` Color, `_Glossiness` Range, `_Metallic` Range, `_SpeedX` Float, `_SpeedY` Float, `_FoamSpeed` Float, `_BumpMap` Texture, `_BumpScale` Float, `_EmissionMap` Texture, `_EmissionColor` Color, `_Cull` Float

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

Keyword filter: tick, seeker, brood, egg, bug, insect, crawl, squish, death, blood, hit, hurt, leech, blob, boar, ooze, goo, slime, green, puke, guck, poison.

| Prefab | List | Details |
| --- | --- | --- |
| fx_Abomination_attack_hit | net | 4 ParticleSystem; TimedDestruction |
| fx_BonemawSerpent_Spit_Hit | net | 4 ParticleSystem; TimedDestruction |
| fx_BonemawSerpent_poisonbreath | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 3 ParticleSystem; TimedDestruction |
| fx_Cinder_hit | net | 3 ParticleSystem; TimedDestruction |
| fx_Cinder_storm_hit | net | ZSFX clips [Amb_Ashlands_OneShot_Burn_01] pitch 1.3-0.8 vol 1.2-1; 3 ParticleSystem; TimedDestruction |
| fx_DvergerMage_Fire_hit | net | ZSFX clips [Enemy_Dverger_Fireball_Impact_01, Enemy_Dverger_Fireball_Impact_02, Enemy_Dverger_Fireball_Impact_03, Enemy_Dverger_Fireball_Impact_04] pitch 0.85-1.1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_DvergerMage_Ice_hit | net | ZSFX clips [Enemy_Dverger_Ice_Projectile_Impact_01, Enemy_Dverger_Ice_Projectile_Impact_02, Enemy_Dverger_Ice_Projectile_Impact_03, Enemy_Dverger_Ice_Projectile_Impact_04, Enemy_Dverger_Ice_Projectile_Impact_05, Enemy_Dverger_Ice_Projectile_Impact_06, Enemy_Dverger_Ice_Projectile_Impact_07] pitch 0.9-1 vol 0.6-0.7; 4 ParticleSystem; TimedDestruction |
| fx_DvergerMage_Support_hit | net | ZSFX clips [Enemy_Dverger_Ring_Impact_01, Enemy_Dverger_Ring_Impact_02, Enemy_Dverger_Ring_Impact_03] pitch 0.9-1.1 vol 1-1; ZSFX clips [ElecEnergy_Impact_Shock] pitch 0.9-1 vol 1-1; 4 ParticleSystem; TimedDestruction |
| fx_Dverger_death | net | ZSFX clips [Enemy_Dverger_VO_Death_01, Enemy_Dverger_VO_Death_02, Enemy_Dverger_VO_Death_03, Enemy_Dverger_VO_Death_04, Enemy_Dverger_VO_Death_05, Enemy_Dverger_VO_Death_06, Enemy_Dverger_VO_Death_07, Enemy_Dverger_VO_Death_08, Enemy_Dverger_VO_Death_09, Enemy_Dverger_VO_Death_10, Enemy_Dverger_VO_Death_11] pitch 1-1.1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_Dverger_hit | net | ZSFX clips [Male_LS_D_HurtGrunt01, Male_LS_D_HurtGrunt02, Male_LS_D_HurtGrunt03, Male_LS_D_HurtGrunt04, Male_LS_D_HurtGrunt05, Male_LS_D_HurtGrunt06, Male_LS_D_HurtGrunt07, Male_LS_D_HurtGrunt08, Male_LS_D_HurtGrunt09] pitch 1.2-1.3 vol 0.5-0.5; 6 ParticleSystem; TimedDestruction |
| fx_Eat_Green | net | 4 ParticleSystem; TimedDestruction |
| fx_Fader_Roar_Projectile_Hit | net | ZSFX clips [Enemy_Father_CharredSummon_Impact_01, Enemy_Father_CharredSummon_Impact_02, Enemy_Father_CharredSummon_Impact_03, Enemy_Father_CharredSummon_Impact_04, Enemy_Father_CharredSummon_Impact_05, Enemy_Father_CharredSummon_Impact_06] pitch 0.9-1.1 vol 2.2-2.2; 5 ParticleSystem; TimedDestruction |
| fx_GoblinShieldHit | net | ZSFX clips [Scifi_Forcefield_Impact2, Scifi_Forcefield_Impact3] pitch 0.9-1 vol 1-1; 2 ParticleSystem; TimedDestruction |
| fx_JotunWitch_Death | net | 5 ParticleSystem; TimedDestruction |
| fx_Puke | net | ZSFX clips [] pitch 0.9-1 vol 1-1; 5 ParticleSystem |
| fx_Queen_Death | net | ZSFX clips [HogMonster_DeathRoar5, HogMonster_DeathRoar6] pitch 0.9-1 vol 1-1; 18 ParticleSystem; TimedDestruction |
| fx_StaffShield_Hit | net | ZSFX clips [Weapons_Protector_Shield_Impact_01, Weapons_Protector_Shield_Impact_02, Weapons_Protector_Shield_Impact_03, Weapons_Protector_Shield_Impact_04, Weapons_Protector_Shield_Impact_05] pitch 0.9-1.1 vol 1-1; 3 ParticleSystem; TimedDestruction |
| fx_TickBloodHit | net | 5 ParticleSystem; TimedDestruction |
| fx_aspect_death | net | ZSFX clips [Enemy_FrozenKing_Spirit_Death_01, Enemy_FrozenKing_Spirit_Death_02, Enemy_FrozenKing_Spirit_Death_03] pitch 0.9-1.1 vol 1.5-1.5; 16 ParticleSystem; TimedDestruction |
| fx_babyseeker_death | net | ZSFX clips [Enemy_BabySeeker_Fx_Death_01, Enemy_BabySeeker_Fx_Death_02, Enemy_BabySeeker_Fx_Death_03, Enemy_BabySeeker_Fx_Death_04] pitch 0.9-1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_babyseeker_hurt | net | 6 ParticleSystem; TimedDestruction |
| fx_bat_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.2-1.3 vol 0.5-0.5; ZSFX clips [SmallCreature3_Hurt1, SmallCreature3_Hurt2] pitch 1-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_bat_hit | net | ZSFX clips [Blood_Splat_Gib02, Blood_Splat_Gib17] pitch 1-1.1 vol 0.8-0.8; ZSFX clips [SmallCreature3_Roar1, SmallCreature3_Roar2, SmallCreature3_Roar3] pitch 1.1-1.2 vol 0.6-0.6; 4 ParticleSystem; TimedDestruction |
| fx_bjorn_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_bjorn_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; 6 ParticleSystem; TimedDestruction |
| fx_blobLava_explosion | net | 7 ParticleSystem; TimedDestruction |
| fx_blobtar_tarball_hit | net | ZSFX clips [Blood_Splat_Gib02, Blood_Splat_Gib11, Blood_Splat_Gib12, Blood_Splat_Gib13, Blood_Splat_Gib16] pitch 0.8-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_bloodweapon_hit | net | 5 ParticleSystem; TimedDestruction |
| fx_boar_pet | net | ZSFX clips [HogMonster_Breath1, HogMonster_Breath2] pitch 0.9-1.1 vol 0.7-0.7; 1 ParticleSystem; TimedDestruction |
| fx_chainlightning_hit | net | ZSFX clips [Explo_Medium_Electricity1] pitch 1-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_charred_death | net | 7 ParticleSystem; TimedDestruction |
| fx_charred_hit | net | 3 ParticleSystem; TimedDestruction |
| fx_charred_summoned_death | net | 7 ParticleSystem; TimedDestruction |
| fx_chicken_death | net | ZSFX clips [Wildlife_Chick_Hurt_01, Wildlife_Chick_Hurt_02, Wildlife_Chick_Hurt_03, Wildlife_Chick_Hurt_04, Wildlife_Chick_Hurt_05, Wildlife_Chick_Hurt_06, Wildlife_Chick_Hurt_07, Wildlife_Chick_Hurt_08, Wildlife_Chick_Hurt_09, Wildlife_Chick_Hurt_10, Wildlife_Chick_Hurt_11, Wildlife_Chick_Hurt_12, Wildlife_Chick_Hurt_13] pitch 0.9-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_chicken_lay_egg | net | ZSFX clips [Grass Step 04, Grass Step_03] pitch 1.2-1.3 vol 1-1; 1 ParticleSystem; TimedDestruction |
| fx_clusterbombstaff_hit | net | ZSFX clips [Player_Weapons_GrenadeStaff_ImpactBig_01, Player_Weapons_GrenadeStaff_ImpactBig_02, Player_Weapons_GrenadeStaff_ImpactBig_03, Player_Weapons_GrenadeStaff_ImpactBig_04] pitch 0.8-1.2 vol 2.5-2.5; 4 ParticleSystem; TimedDestruction |
| fx_clusterbombstaff_splinter_hit | net | ZSFX clips [Player_Weapons_GrenadeStaff_ImpactSmall_01, Player_Weapons_GrenadeStaff_ImpactSmall_02, Player_Weapons_GrenadeStaff_ImpactSmall_03, Player_Weapons_GrenadeStaff_ImpactSmall_04, Player_Weapons_GrenadeStaff_ImpactSmall_05] pitch 0.7-1.3 vol 2.7-2.7; 4 ParticleSystem; TimedDestruction |
| fx_deathsquito_hit | net | ZSFX clips [Blood_Splat_Gib02, Blood_Splat_Gib17] pitch 1-1.1 vol 0.8-0.8; 4 ParticleSystem; TimedDestruction |
| fx_deatsquito_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.1-1.2 vol 0.7-0.7; 12 ParticleSystem; TimedDestruction |
| fx_egg_splash | net | ZSFX clips [Enemy_Tick_Death_01, Enemy_Tick_Death_02, Enemy_Tick_Death_03, Enemy_Tick_Death_04, Enemy_Tick_Death_05] pitch 0.8-0.8 vol 1-1; 9 ParticleSystem; TimedDestruction |
| fx_fader_meteor_hit | net | ZSFX clips [Enemy_Father_MeteorCall_Impact_01, Enemy_Father_MeteorCall_Impact_02, Enemy_Father_MeteorCall_Impact_03, Enemy_Father_MeteorCall_Impact_04, Enemy_Father_MeteorCall_Impact_05, Enemy_Father_MeteorCall_Impact_06] pitch 0.9-1.1 vol 1.6-1.8; 6 ParticleSystem; TimedDestruction |
| fx_fallenvalkyrie_death | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib11, Blood_Splat_Gib12] pitch 1.2-1.3 vol 0.5-0.5; 7 ParticleSystem; TimedDestruction |
| fx_fallenvalkyrie_poisonbreath | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 2 ParticleSystem; TimedDestruction |
| fx_fimbulvinter_meteor_hit | net | ZSFX clips [Smash_HugeRockRip2] pitch 0.9-1.1 vol 1-1; ZSFX clips [Explo_Medium_10] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_float_hitwater | net | ZSFX clips [Fishing_BaitSplash1, Fishing_BaitSplash2, Fishing_BaitSplash3, Fishing_BaitSplash4] pitch 0.8-0.9 vol 1-1; 4 ParticleSystem; TimedDestruction |
| fx_frozenking_spikerain_hit | net | ZSFX clips [Enemy_FrozenKing_SpikeRain_Projectile_Explosion_01, Enemy_FrozenKing_SpikeRain_Projectile_Explosion_02, Enemy_FrozenKing_SpikeRain_Projectile_Explosion_03, Enemy_FrozenKing_SpikeRain_Projectile_Explosion_04] pitch 0.85-1.15 vol 1.2-1.45; 6 ParticleSystem; TimedDestruction |
| fx_gjall_death | net | ZSFX clips [Enemy_Gjall_Death_FX_01, Enemy_Gjall_Death_FX_02, Enemy_Gjall_Death_FX_03] pitch 0.9-1 vol 1-1; 18 ParticleSystem; TimedDestruction |
| fx_gjall_egg_splat | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.9-1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_goblinking_beam_hit | net | ZSFX clips [Explo_EnergyFireball01] pitch 0.9-1.1 vol 1-1; 3 ParticleSystem; TimedDestruction |
| fx_goblinking_death | net | ZSFX clips [Explo_Large_13] pitch 1-1 vol 1-1; ZSFX clips [Blood_Splat_Gib11] pitch 0.9-0.9 vol 1-1; 10 ParticleSystem; TimedDestruction |
| fx_goblinking_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; 3 ParticleSystem; TimedDestruction |
| fx_goblinking_meteor_hit | net | ZSFX clips [Smash_HugeRockRip2] pitch 0.9-1.1 vol 1-1; ZSFX clips [Explo_Medium_10] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_greenroots_projectile_hit | net | 4 ParticleSystem; TimedDestruction |
| fx_hare_death | net | 5 ParticleSystem; TimedDestruction |
| fx_hen_death | net | ZSFX clips [seagull1, seagull4, seagull5, seagull6] pitch 0.9-1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_himminafl_hit | net | ZSFX clips [Explo_Medium_Electricity1] pitch 1-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_iceshard_hit | net | ZSFX clips [Weapons_IceStaff_Impact_01, Weapons_IceStaff_Impact_02, Weapons_IceStaff_Impact_03, Weapons_IceStaff_Impact_04, Weapons_IceStaff_Impact_05, Weapons_IceStaff_Impact_06, Weapons_IceStaff_Impact_07] pitch 1-1.1 vol 0.6-0.8; 4 ParticleSystem; TimedDestruction |
| fx_jotunbane_hit | net | 5 ParticleSystem; TimedDestruction |
| fx_lightningstaffprojectile_hit | net | ZSFX clips [Explo_Medium_Electricity1] pitch 1-1 vol 1-1; 3 ParticleSystem; TimedDestruction |
| fx_lightningweapon_hit | net | ZSFX clips [magic_spell_electro_22, magic_spell_electro_23, magic_spell_electro_24, magic_spell_electro_25] pitch 0.9-1.1 vol 0.75-0.6; 4 ParticleSystem; TimedDestruction |
| fx_lox_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_lox_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; 6 ParticleSystem; TimedDestruction |
| fx_loxcalf_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 1.5-2 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_moose_death | net | ZSFX clips [Enemy_Moose_DeathExplode_01, Enemy_Moose_DeathExplode_02, Enemy_Moose_DeathExplode_03, Enemy_Moose_DeathExplode_04, Enemy_Moose_DeathExplode_05] pitch 1-1 vol 0.3-0.45; 5 ParticleSystem; TimedDestruction |
| fx_moose_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; 6 ParticleSystem; TimedDestruction |
| fx_morgen_death | net | 6 ParticleSystem; TimedDestruction |
| fx_morgen_hit | net | 2 ParticleSystem; TimedDestruction |
| fx_natureweapon_hit | net | 1 ParticleSystem; TimedDestruction |
| fx_radiation_hit | net | ZSFX clips [Eiter_Impact_01, Eiter_Impact_02, Eiter_Impact_03, Eiter_Impact_04, Eiter_Impact_05, Eiter_Impact_06, Eiter_Impact_07, Eiter_Impact_08] pitch 0.8-1.4 vol 0.4-0.8; 1 ParticleSystem; TimedDestruction |
| fx_seeker_death | net | ZSFX clips [Enemy_Seeker_FX_Death_01, Enemy_Seeker_FX_Death_02, Enemy_Seeker_FX_Death_03, Enemy_Seeker_FX_Death_04, Enemy_Seeker_FX_Death_05] pitch 0.9-1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_seeker_hurt | net | 6 ParticleSystem; TimedDestruction |
| fx_seeker_melee_hit | net | ZSFX clips [Enemy_Seeker_Attack_VO_01, Enemy_Seeker_Attack_VO_02, Enemy_Seeker_Attack_VO_03, Enemy_Seeker_Attack_VO_04, Enemy_Seeker_Attack_VO_05, Enemy_Seeker_Attack_VO_06, Enemy_Seeker_Attack_VO_07] pitch 0.7-0.8 vol 0.6-0.8; TimedDestruction |
| fx_seeker_spawn | net | ZSFX clips [Hit_SlimeSplat3, Hit_SlimeSplat4, Hit_WaterSplash_JumpOnBody] pitch 0.9-0.6 vol 1-1; 4 ParticleSystem; TimedDestruction |
| fx_seekerbrute_death | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.8-0.9 vol 1-1; 12 ParticleSystem; TimedDestruction |
| fx_shieldgenerator_domehit | net | 3 ParticleSystem; TimedDestruction |
| fx_sledge_demolisher_hit | net | ZSFX clips [Player_Movement_SledgeHit_M_01, Player_Movement_SledgeHit_M_02, Player_Movement_SledgeHit_M_03] pitch 0.85-1 vol 0.7-0.8; ZSFX clips [Weapons_Sledge_Demolisher_Impact_01, Weapons_Sledge_Demolisher_Impact_02, Weapons_Sledge_Demolisher_Impact_03, Weapons_Sledge_Demolisher_Impact_04, Weapons_Sledge_Demolisher_Impact_05] pitch 0.9-1 vol 0.7-0.8; 9 ParticleSystem; TimedDestruction |
| fx_tendril_death | net | ZSFX clips [Enemy_FrozenKing_Tendril_Death_01, Enemy_FrozenKing_Tendril_Death_02, Enemy_FrozenKing_Tendril_Death_03, Enemy_FrozenKing_Tendril_Death_04, Enemy_FrozenKing_Tendril_Death_05, Enemy_FrozenKing_Tendril_Death_06] pitch 0.85-1.2 vol 0.55-0.85; 8 ParticleSystem; TimedDestruction |
| fx_tentaroot_death | net | ZSFX clips [Blood_Splat_Gib11, Blood_Splat_Gib12, Blood_Splat_Gib13] pitch 1.1-1.2 vol 1-1; 8 ParticleSystem; TimedDestruction |
| fx_tick_death | net | ZSFX clips [Enemy_Tick_Death_01, Enemy_Tick_Death_02, Enemy_Tick_Death_03, Enemy_Tick_Death_04, Enemy_Tick_Death_05] pitch 0.9-1 vol 1-1; 6 ParticleSystem; TimedDestruction |
| fx_unbjorn_death | net | ZSFX clips [PlantMonster_DeathRoar1, PlantMonster_DeathRoar2, PlantMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; 5 ParticleSystem; TimedDestruction |
| fx_unbjorn_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; 6 ParticleSystem; TimedDestruction |
| fx_vinegreen_destruction | net | 3 ParticleSystem; TimedDestruction |
| fx_vines_hit | net | ZSFX clips [Hit_Leaves01] pitch 1.1-0.9 vol 1-1; 1 ParticleSystem; TimedDestruction |
| sfx_Bonemass_Hit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_Bonemass_death | net | ZSFX clips [RockMonster_DeathRoar2] pitch 1-1 vol 1-1; ZSFX clips [TreeMonster_HurtRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_GoblinShaman_death | net | ZSFX clips [Male_LS_D_Laugh06, Male_LS_D_Laugh07, Male_LS_D_MadScream01, Male_LS_D_MadScream02, Male_LS_D_MadScream03, Male_LS_D_MadScream04] pitch 1-1.2 vol 1-1; TimedDestruction |
| sfx_GoblinShaman_hurt | net | ZSFX clips [Male_LS_D_PainScream04, Male_LS_D_PainScream05, Male_LS_D_PainScream06, Male_LS_D_PainScream10] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_GuckSackDestroyed | net | ZSFX clips [Hit_WaterSplash_JumpOnBody] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_GuckSackHit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_MudHit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_Poison_Start | net | ZSFX clips [IcePlant_ExpelGas4] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_ProjectileHit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_Puke_female | net | ZSFX clips [UI_Vomit_Female_M_01, UI_Vomit_Female_M_02, UI_Vomit_Female_M_03] pitch 0.95-1 vol 1-1 |
| sfx_Puke_male | net | ZSFX clips [UI_Vomit_Male_M_01, UI_Vomit_Male_M_02, UI_Vomit_Male_M_03] pitch 0.95-1 vol 1-1 |
| sfx_archery_target_hit | net | ZSFX clips [Player_Weapons_Archery_Target_Arrow_Hit_01, Player_Weapons_Archery_Target_Arrow_Hit_02, Player_Weapons_Archery_Target_Arrow_Hit_03, Player_Weapons_Archery_Target_Arrow_Hit_04, Player_Weapons_Archery_Target_Arrow_Hit_05] pitch 0.9-1 vol 0.6-0.5; TimedDestruction |
| sfx_arrow_hit | net | ZSFX clips [Weapons_Arrow_Hit_M_01, Weapons_Arrow_Hit_M_02, Weapons_Arrow_Hit_M_03] pitch 0.9-1 vol 0.5-0.7; TimedDestruction |
| sfx_asksvin_death | net | ZSFX clips [Enemy_Asksvin_Death_01, Enemy_Asksvin_Death_02, Enemy_Asksvin_Death_03, Enemy_Asksvin_Death_04, Enemy_Asksvin_Death_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_axe_flint_hit | net | ZSFX clips [Player_Movement_Axe_Flint_Hit_M_01, Player_Movement_Axe_Flint_Hit_M_02, Player_Movement_Axe_Flint_Hit_M_03, Player_Movement_Axe_Flint_Hit_M_04] pitch 0.9-1.1 vol 0.7-0.9; TimedDestruction |
| sfx_axe_hit | net | ZSFX clips [Player_Movement_Axe_Hit_M_01, Player_Movement_Axe_Hit_M_02, Player_Movement_Axe_Hit_M_03, Player_Movement_Axe_Hit_M_04, Player_Movement_Axe_Hit_M_05] pitch 0.95-1 vol 0.4-0.45; TimedDestruction |
| sfx_barley_hit | net | ZSFX clips [Hit_Leaves02] pitch 1.1-0.9 vol 1-1; TimedDestruction |
| sfx_battleaxe_hit | net | ZSFX clips [Player_Movement_Axe_Hit_M_01, Player_Movement_Axe_Hit_M_02, Player_Movement_Axe_Hit_M_03, Player_Movement_Axe_Hit_M_04, Player_Movement_Axe_Hit_M_05] pitch 0.9-1 vol 0.4-0.5; TimedDestruction |
| sfx_bear_death | net | ZSFX clips [Enemy_Bear_Death_01, Enemy_Bear_Death_02, Enemy_Bear_Death_03, Enemy_Bear_Death_04, Enemy_Bear_Death_05, Enemy_Bear_Death_06, Enemy_Bear_Death_07] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_bear_hurt | net | ZSFX clips [Enemy_Bear_Hurt_01, Enemy_Bear_Hurt_02, Enemy_Bear_Hurt_03, Enemy_Bear_Hurt_04, Enemy_Bear_Hurt_05, Enemy_Bear_Hurt_06, Enemy_Bear_Hurt_07, Enemy_Bear_Hurt_08, Enemy_Bear_Hurt_09] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_beehive_hit | net | ZSFX clips [Smash_TorchBreak1] pitch 1.1-0.9 vol 1-1; TimedDestruction |
| sfx_blobLava_explosion | net | ZSFX clips [DarkMagic_SoulSteal2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_blob_alerted | net | ZSFX clips [FatMonster_Growl2] pitch 0.9-1.1 vol 0.4-0.5; TimedDestruction |
| sfx_blob_attack | net | ZSFX clips [FatMonster_Growl1] pitch 1.1-1.3 vol 0.8-0.8; TimedDestruction |
| sfx_blob_death | net | ZSFX clips [FishMonster_DeathRoar1, FishMonster_DeathRoar2, FishMonster_DeathRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_blob_hit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_blob_idle | net | ZSFX clips [FatMonster_Growl3] pitch 0.9-1.1 vol 0.4-0.5; TimedDestruction |
| sfx_blob_jump | net | ZSFX clips [MudMonster_AttackSplat1] pitch 0.8-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_bloblava_alert | net | ZSFX clips [Enemy_LavaBlob_Alert_01, Enemy_LavaBlob_Alert_02, Enemy_LavaBlob_Alert_03] pitch 1-1 vol 1.6-1.8; TimedDestruction |
| sfx_bloblava_death | net | ZSFX clips [Enemy_LavaBlob_DeathBurst_01, Enemy_LavaBlob_DeathBurst_02, Enemy_LavaBlob_DeathBurst_03, Enemy_LavaBlob_DeathBurst_04, Enemy_LavaBlob_DeathBurst_05, Enemy_LavaBlob_DeathBurst_06, Enemy_LavaBlob_DeathBurst_07, Enemy_LavaBlob_DeathBurst_08] pitch 0.9-1.1 vol 2.2-2.4; TimedDestruction |
| sfx_bloblava_grow | net | ZSFX clips [Enemy_LavaBlob_Grow_01, Enemy_LavaBlob_Grow_02, Enemy_LavaBlob_Grow_03, Enemy_LavaBlob_Grow_04] pitch 1-1 vol 1.5-1.8; TimedDestruction |
| sfx_bloblava_idle | net | ZSFX clips [Enemy_LavaBlob_Idle_01, Enemy_LavaBlob_Idle_02, Enemy_LavaBlob_Idle_03, Enemy_LavaBlob_Idle_04, Enemy_LavaBlob_Idle_05, Enemy_LavaBlob_Idle_06] pitch 1-1 vol 2-2.2; TimedDestruction |
| sfx_bloblava_jump | net | ZSFX clips [Enemy_LavaBlob_Jump_01, Enemy_LavaBlob_Jump_02, Enemy_LavaBlob_Jump_03, Enemy_LavaBlob_Jump_04] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_blobtar_attack_spit | net | ZSFX clips [Blood_Splat_Gib14, Blood_Splat_Gib17] pitch 0.9-1 vol 0.8-0.8; TimedDestruction |
| sfx_blobtar_idle | net | ZSFX clips [Squish_Wet_FleshBlood01, Squish_Wet_FleshBlood03, Squish_Wet_FleshBlood04, Squish_Wet_FleshBlood05, Squish_Wet_FleshBlood06, Squish_Wet_FleshBlood07] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_boar_alerted | net | ZSFX clips [HogMonster_DeathRoar4, HogMonster_DeathRoar2] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_boar_attack | net | ZSFX clips [Zombie06_SwipeAttack5, Zombie06_SwipeAttack6] pitch 1-1.2 vol 0.8-0.8; TimedDestruction |
| sfx_boar_birth | net | ZSFX clips [HogMonster_DeathRoar5] pitch 1.2-1.3 vol 1-1; TimedDestruction |
| sfx_boar_death | net | ZSFX clips [HogMonster_DeathRoar5, HogMonster_DeathRoar6] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_boar_hit | net | ZSFX clips [HogMonster_HurtGrowl1, HogMonster_HurtGrowl2, HogMonster_HurtGrowl3] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_boar_idle | net | ZSFX clips [HogMonster_Breath1, HogMonster_Breath2, HogMonster_Breath3, HogMonster_Breath4, HogMonster_Breath5, HogMonster_Breath6, HogMonster_GrowlScrape] pitch 1.3-1.5 vol 0.8-0.8; TimedDestruction |
| sfx_boar_love | net | ZSFX clips [HogMonster_Breath1, HogMonster_Breath2, HogMonster_Breath3, HogMonster_Breath4] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_bonemaw_serpent_death | net | ZSFX clips [Enemy_Bonemaw_Death_01, Enemy_Bonemaw_Death_02, Enemy_Bonemaw_Death_03, Enemy_Bonemaw_Death_04] pitch 0.7-1.3 vol 1.6-1.7; TimedDestruction |
| sfx_bonemaw_serpent_spit_hit | net | ZSFX clips [Enemy_Bonemaw_Attack_Spit_Proj_Hit_01, Enemy_Bonemaw_Attack_Spit_Proj_Hit_02, Enemy_Bonemaw_Attack_Spit_Proj_Hit_03, Enemy_Bonemaw_Attack_Spit_Proj_Hit_04, Enemy_Bonemaw_Attack_Spit_Proj_Hit_05] pitch 0.7-1.3 vol 1.4-1.5; TimedDestruction |
| sfx_branch_hit | net | ZSFX clips [Branch_Hit_01, Branch_Hit_02, Branch_Hit_03, Branch_Hit_04] pitch 0.85-1.1 vol 0.95-1.3; TimedDestruction |
| sfx_bush_hit | net | ZSFX clips [Hit_Leaves01] pitch 1.1-0.9 vol 1-1; TimedDestruction |
| sfx_cart_hit | net | ZSFX clips [Hit_LargeWoodImpact1] pitch 0.8-1.2 vol 0.5-0.5; TimedDestruction |
| sfx_charred_death | net | ZSFX clips [Enemy_CharredMelee_Death_01, Enemy_CharredMelee_Death_02, Enemy_CharredMelee_Death_03, Enemy_CharredMelee_Death_04, Enemy_CharredMelee_Death_05, Enemy_CharredMelee_Death_06, Enemy_CharredMelee_Death_07, Enemy_CharredMelee_Death_08, Enemy_CharredMelee_Death_09, Enemy_CharredMelee_Death_10] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_charred_hurt | net | ZSFX clips [Enemy_CharredMelee_Hurt_01, Enemy_CharredMelee_Hurt_02, Enemy_CharredMelee_Hurt_03, Enemy_CharredMelee_Hurt_04, Enemy_CharredMelee_Hurt_05, Enemy_CharredMelee_Hurt_06, Enemy_CharredMelee_Hurt_07, Enemy_CharredMelee_Hurt_08, Enemy_CharredMelee_Hurt_09, Enemy_CharredMelee_Hurt_10, Enemy_CharredMelee_Hurt_11, Enemy_CharredMelee_Hurt_12, Enemy_CharredMelee_Hurt_13, Enemy_CharredMelee_Hurt_14, Enemy_CharredMelee_Hurt_15] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_charred_twitcher_death | net | ZSFX clips [Enemy_Twitcher_Death_01, Enemy_Twitcher_Death_02, Enemy_Twitcher_Death_03, Enemy_Twitcher_Death_04, Enemy_Twitcher_Death_05] pitch 1.2-0.8 vol 0.8-1; TimedDestruction |
| sfx_charred_twitcher_hurt | net | ZSFX clips [Enemy_Twitcher_Hurt_01, Enemy_Twitcher_Hurt_02, Enemy_Twitcher_Hurt_03, Enemy_Twitcher_Hurt_04, Enemy_Twitcher_Hurt_05, Enemy_Twitcher_Hurt_06, Enemy_Twitcher_Hurt_07, Enemy_Twitcher_Hurt_08] pitch 0.7-1.3 vol 1.5-1.8; TimedDestruction |
| sfx_chick_hurt | net | ZSFX clips [Wildlife_Chick_Hurt_01, Wildlife_Chick_Hurt_02, Wildlife_Chick_Hurt_03, Wildlife_Chick_Hurt_04, Wildlife_Chick_Hurt_05, Wildlife_Chick_Hurt_06, Wildlife_Chick_Hurt_07, Wildlife_Chick_Hurt_08, Wildlife_Chick_Hurt_09, Wildlife_Chick_Hurt_10, Wildlife_Chick_Hurt_11, Wildlife_Chick_Hurt_12, Wildlife_Chick_Hurt_13] pitch 0.9-1.1 vol 0.6-0.8; TimedDestruction |
| sfx_chicken_death | net | ZSFX clips [Wildlife_Chicken_Death_01, Wildlife_Chicken_Death_02, Wildlife_Chicken_Death_03, Wildlife_Chicken_Death_04, Wildlife_Chicken_Death_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_chicken_hurt | net | ZSFX clips [Wildlife_Chicken_Hurt_01, Wildlife_Chicken_Hurt_02, Wildlife_Chicken_Hurt_03, Wildlife_Chicken_Hurt_04, Wildlife_Chicken_Hurt_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_cinder_rain_hit | net | ZSFX clips [World_Cinder_Burn_Hit_01, World_Cinder_Burn_Hit_02, World_Cinder_Burn_Hit_03, World_Cinder_Burn_Hit_04, World_Cinder_Burn_Hit_05, World_Cinder_Burn_Hit_06] pitch 0.8-1.2 vol 0.2-0.4; TimedDestruction |
| sfx_club_hit | net | ZSFX clips [sWeaponHit02] pitch 0.7-1.1 vol 0.4-0.4; TimedDestruction |
| sfx_crow_death | net | ZSFX clips [crow_splatt] pitch 1.1-0.9 vol 0.8-0.8; TimedDestruction |
| sfx_deathsquito_attack | net | ZSFX clips [sScorpionTailAttack01, sScorpionTailAttack02] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_deer_death | net | ZSFX clips [Rhinosaur_HurtGrowl1, Rhinosaur_HurtGrowl2, Rhinosaur_HurtGrowl3] pitch 1.6-1.8 vol 1-1; TimedDestruction |
| sfx_dragon_death | net | ZSFX clips [Dragon_DeathRoar4] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_dragon_hurt | net | ZSFX clips [Blood_Splat_Gib04, Blood_Splat_Gib03] pitch 0.9-1 vol 0.7-0.7; TimedDestruction |
| sfx_dragon_melee_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 1-1; TimedDestruction |
| sfx_dragonegg_destroy | net | ZSFX clips [Blood_Splat_Gib08, Blood_Splat_Gib09, Blood_Splat_Gib10] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_draugr_death | net | ZSFX clips [Zombie09_Death1, Zombie09_Death2, Zombie09_Death3, Zombie09_Death4, Zombie09_Death5] pitch 1-1.1 vol 0.7-0.7; TimedDestruction |
| sfx_draugr_hit | net | ZSFX clips [Zombie06_Hurt_Grunt1, Zombie06_Hurt_Grunt2, Zombie06_Hurt_Grunt3, Zombie06_Hurt_Grunt4, Zombie06_Hurt_Grunt5] pitch 1.1-1.2 vol 0.5-0.5; TimedDestruction |
| sfx_draugrpile_hit | net | ZSFX clips [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_dverger_vo_death | net | ZSFX clips [Enemy_Dverger_VO_Death_01, Enemy_Dverger_VO_Death_02, Enemy_Dverger_VO_Death_03, Enemy_Dverger_VO_Death_04, Enemy_Dverger_VO_Death_05, Enemy_Dverger_VO_Death_06, Enemy_Dverger_VO_Death_07, Enemy_Dverger_VO_Death_08, Enemy_Dverger_VO_Death_09, Enemy_Dverger_VO_Death_10, Enemy_Dverger_VO_Death_11] pitch 1-1.1 vol 0.9-1; TimedDestruction |
| sfx_dverger_vo_hurt | net | ZSFX clips [Enemy_Dverger_VO_Hurt_01, Enemy_Dverger_VO_Hurt_02, Enemy_Dverger_VO_Hurt_03, Enemy_Dverger_VO_Hurt_04, Enemy_Dverger_VO_Hurt_05, Enemy_Dverger_VO_Hurt_06, Enemy_Dverger_VO_Hurt_07, Enemy_Dverger_VO_Hurt_08, Enemy_Dverger_VO_Hurt_09, Enemy_Dverger_VO_Hurt_10, Enemy_Dverger_VO_Hurt_11, Enemy_Dverger_VO_Hurt_12] pitch 0.95-1.1 vol 0.9-1; TimedDestruction |
| sfx_eikthyr_death | net | ZSFX clips [Rhinosaur_DeathGrowl2, Rhinosaur_DeathGrowl1] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_eikthyr_hit | net | ZSFX clips [Rhinosaur_HurtGrowl1, Rhinosaur_HurtGrowl2, Rhinosaur_HurtGrowl3, Rhinosaur_HurtGrowl4, Rhinosaur_HurtGrowl5] pitch 1.1-1.2 vol 0.6-0.6; TimedDestruction |
| sfx_elaking_death | net | ZSFX clips [Enemy_Elaking_Death_01, Enemy_Elaking_Death_02, Enemy_Elaking_Death_03, Enemy_Elaking_Death_04] pitch 1-1 vol 0.25-0.35; TimedDestruction |
| sfx_elaking_death_old | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 2-2.6 vol 1-1; TimedDestruction |
| sfx_elaking_hit | net | ZSFX clips [Enemy_Elaking_Hurt_01, Enemy_Elaking_Hurt_02, Enemy_Elaking_Hurt_03, Enemy_Elaking_Hurt_04] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_elaking_hit_old | net | ZSFX clips [Env_Wood_Break_Snow_01, Env_Wood_Break_Snow_02, Env_Wood_Break_Snow_03, Env_Wood_Break_Snow_04, Env_Wood_Break_Snow_05, Env_Wood_Break_Snow_06, Env_Wood_Break_Snow_07, Env_Wood_Break_Snow_08] pitch 0.85-1.1 vol 0.75-0.95; 7 ParticleSystem; TimedDestruction |
| sfx_fader_death_explosion | net | ZSFX clips [Enemy_Father_Death_Explosion] pitch 1-1 vol 6-2; TimedDestruction |
| sfx_fader_death_start | net | ZSFX clips [Enemy_Father_Death_Start] pitch 1-1 vol 1.4-1.6; TimedDestruction |
| sfx_fallenvalkyrie_death | net | ZSFX clips [Enemy_FallenValkyrie_Death_01, Enemy_FallenValkyrie_Death_02, Enemy_FallenValkyrie_Death_03, Enemy_FallenValkyrie_Death_04, Enemy_FallenValkyrie_Death_05, Enemy_FallenValkyrie_Death_06, Enemy_FallenValkyrie_Death_07, Enemy_FallenValkyrie_Death_08] pitch 1-1 vol 0.8-1; TimedDestruction |
| sfx_fallenwarrior_death | net | ZSFX clips [Enemy_FallenWarrior_Death_01, Enemy_FallenWarrior_Death_02, Enemy_FallenWarrior_Death_03, Enemy_FallenWarrior_Death_04, Enemy_FallenWarrior_Death_05] pitch 0.9-1.05 vol 0.3-0.45; TimedDestruction |
| sfx_fallenwarrior_hurt | net | ZSFX clips [Enemy_FallenWarrior_Hurt_01, Enemy_FallenWarrior_Hurt_02, Enemy_FallenWarrior_Hurt_03, Enemy_FallenWarrior_Hurt_04, Enemy_FallenWarrior_Hurt_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_fenring_claw_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-1 vol 0.5-0.5; TimedDestruction |
| sfx_fenring_death | net | ZSFX clips [Zombie09_Death4, Zombie09_Death5] pitch 1-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_frozenking_death_crystal_break | net | ZSFX clips [Enemy_FrozenKing_Death_Crystal_Break_01] pitch 1-1 vol 0.85-0.85; TimedDestruction |
| sfx_frozenking_death_crystal_sequence | net | ZSFX clips [Enemy_FrozenKing_Death_Crystal_Sequence_01] pitch 1-1 vol 0.85-0.85; TimedDestruction |
| sfx_frozenking_death_p3_sequence | net | ZSFX clips [Enemy_frozenKing_p3_Death] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_frozenking_spirit_death | net | ZSFX clips [Enemy_FrozenKing_Spirit_Death_01, Enemy_FrozenKing_Spirit_Death_02, Enemy_FrozenKing_Spirit_Death_03] pitch 0.9-1.1 vol 1.5-1.5; TimedDestruction |
| sfx_frozenking_tendril_death | net | ZSFX clips [Enemy_FrozenKing_Tendril_Death_01, Enemy_FrozenKing_Tendril_Death_02, Enemy_FrozenKing_Tendril_Death_03, Enemy_FrozenKing_Tendril_Death_04, Enemy_FrozenKing_Tendril_Death_05, Enemy_FrozenKing_Tendril_Death_06] pitch 0.85-1.2 vol 0.55-0.85; TimedDestruction |
| sfx_gameltroll_death | net | ZSFX clips [Enemy_Gameltroll_Death_01, Enemy_Gameltroll_Death_02, Enemy_Gameltroll_Death_03, Enemy_Gameltroll_Death_04, Enemy_Gameltroll_Death_05] pitch 0.95-1.05 vol 0.35-0.5; TimedDestruction |
| sfx_gameltroll_hurt | net | ZSFX clips [Enemy_Gameltroll_Hurt_01, Enemy_Gameltroll_Hurt_02, Enemy_Gameltroll_Hurt_03, Enemy_Gameltroll_Hurt_04, Enemy_Gameltroll_Hurt_05] pitch 0.95-1.05 vol 0.3-0.45; TimedDestruction |
| sfx_gameltroll_stoneturn_death | net | ZSFX clips [Enemy_Gameltroll_Death_StoneTurn_01, Enemy_Gameltroll_Death_StoneTurn_02, Enemy_Gameltroll_Death_StoneTurn_03, Enemy_Gameltroll_Death_StoneTurn_04, Enemy_Gameltroll_Death_StoneTurn_05] pitch 0.92-1.05 vol 0.45-0.55; TimedDestruction |
| sfx_gdking_death | net | ZSFX clips [TreeMonster_DeathRoar2] pitch 0.9-1.1 vol 1-1; ZSFX clips [TreeMonster_HurtRoar3] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_ghost_attack_hit | net | ZSFX clips [EvilGhost_EnergyHit1, EvilGhost_EnergyHit2] pitch 0.8-0.9 vol 0.8-0.8; TimedDestruction |
| sfx_ghost_death | net | ZSFX clips [EvilGhost_SpecterDeath2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_ghost_hurt | net | ZSFX clips [EvilGhost_Death5] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_ghosts_death | net | ZSFX clips [Enemy_Ghost_Death_01, Enemy_Ghost_Death_02, Enemy_Ghost_Death_03, Enemy_Ghost_Death_04, Enemy_Ghost_Death_05, Enemy_Ghost_Death_06] pitch 1-1 vol 0.4-0.5; TimedDestruction |
| sfx_ghosts_hurt | net | ZSFX clips [Enemy_Ghost_Hurt_01, Enemy_Ghost_Hurt_02, Enemy_Ghost_Hurt_03, Enemy_Ghost_Hurt_04, Enemy_Ghost_Hurt_05, Enemy_Ghost_Hurt_06, Enemy_Ghost_Hurt_07] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_gjall_attack_tick_drop | net | ZSFX clips [Enemy_Gjall_Attack_TickDrop_01, Enemy_Gjall_Attack_TickDrop_02, Enemy_Gjall_Attack_TickDrop_03] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_gjall_death | net | ZSFX clips [Enemy_Gjall_Death_FX_01, Enemy_Gjall_Death_FX_02, Enemy_Gjall_Death_FX_03] pitch 1-1 vol 1-1; TimedDestruction |
| sfx_gjall_tick_drop | net | ZSFX clips [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_goblin_death | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_goblin_hit | net | ZSFX clips [Male_LS_D_HurtGrunt01, Male_LS_D_HurtGrunt02, Male_LS_D_HurtGrunt03, Male_LS_D_HurtGrunt04, Male_LS_D_HurtGrunt05, Male_LS_D_HurtGrunt06, Male_LS_D_HurtGrunt07, Male_LS_D_HurtGrunt08, Male_LS_D_HurtGrunt09] pitch 1.2-1.3 vol 0.5-0.5; TimedDestruction |
| sfx_goblinbrute_clubhit | net | ZSFX clips [sWeaponHit08] pitch 0.7-0.9 vol 1-1; TimedDestruction |
| sfx_goblinbrute_death | net | ZSFX clips [Male_LS_D_Death01, Male_LS_D_Death02, Male_LS_D_Death03, Male_LS_D_Death04] pitch 0.5-0.6 vol 1-1; TimedDestruction |
| sfx_goblinbrute_hit | net | ZSFX clips [Male_LS_D_HurtGrunt01, Male_LS_D_HurtGrunt02, Male_LS_D_HurtGrunt03, Male_LS_D_HurtGrunt04, Male_LS_D_HurtGrunt05, Male_LS_D_HurtGrunt06, Male_LS_D_HurtGrunt07, Male_LS_D_HurtGrunt08, Male_LS_D_HurtGrunt09] pitch 0.6-0.7 vol 0.8-0.8; TimedDestruction |
| sfx_grapplinghook_hit | net | ZSFX clips [Player_Weapons_GrapplingHook_Hit_01, Player_Weapons_GrapplingHook_Hit_02, Player_Weapons_GrapplingHook_Hit_03, Player_Weapons_GrapplingHook_Hit_04, Player_Weapons_GrapplingHook_Hit_05] pitch 0.9-1 vol 0.6-0.5; TimedDestruction |
| sfx_greydwarf_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 0.5-0.5; TimedDestruction |
| sfx_greydwarf_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_greydwarf_deepnorth_death | net | ZSFX clips [Enemy_Greydwarf_DeepNorth_Death_01, Enemy_Greydwarf_DeepNorth_Death_02, Enemy_Greydwarf_DeepNorth_Death_03] pitch 1.1-1.2 vol 1-1; TimedDestruction |
| sfx_greydwarf_elite_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_greydwarf_hit | net | ZSFX clips [ScalyCreature_HurtGrowl1, ScalyCreature_HurtGrowl2, ScalyCreature_HurtGrowl3] pitch 1.1-1.2 vol 0.5-0.5; TimedDestruction |
| sfx_greydwarf_stone_hit | net | ZSFX clips [sWeaponHit01] pitch 1.2-1.3 vol 0.4-0.4; TimedDestruction |
| sfx_greydwarfnest_hit | net | ZSFX clips [Smash_WoodBreak06] pitch 0.8-1.1 vol 0.5-0.5; TimedDestruction |
| sfx_greyling_death | net | ZSFX clips [ScalyCreature_DeathRoar13] pitch 1.8-2 vol 1-1; TimedDestruction |
| sfx_greyling_hit | net | ZSFX clips [ScalyCreature_HurtGrowl1, ScalyCreature_HurtGrowl2, ScalyCreature_HurtGrowl3] pitch 1.8-2 vol 0.5-0.5; TimedDestruction |
| sfx_hare_death_vocal | net | ZSFX clips [Hare_Death_Vocal_01, Hare_Death_Vocal_02, Hare_Death_Vocal_03, Hare_Death_Vocal_04, Hare_Death_Vocal_05] pitch 0.95-1 vol 0.8-1; TimedDestruction |
| sfx_hatchling_death | net | ZSFX clips [Dragon_DeathRoar1] pitch 0.9-1 vol 1-1; ZSFX clips [Dragon_FlameBurst] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_hatcling_hurt | net | ZSFX clips [Dragon_HurtRoar2, Dragon_HurtRoar4] pitch 1.2-1.3 vol 0.7-0.7; TimedDestruction |
| sfx_hit | net | ZSFX clips [UI_Damage_Self_S_01, UI_Damage_Self_S_02, UI_Damage_Self_S_03, UI_Damage_Self_S_04, UI_Damage_Self_S_05] pitch 0.8-1 vol 0.5-0.5; TimedDestruction |
| sfx_hit_vibration_only | net | ZSFX clips [UI_Damage_Self_S_01_amplified_vibration, UI_Damage_Self_S_02_amplified_vibration, UI_Damage_Self_S_03_amplified_vibration, UI_Damage_Self_S_04_amplified_vibration, UI_Damage_Self_S_05_amplified_vibration] pitch 0.8-1 vol 1-1; TimedDestruction |
| sfx_ice_hit | net | ZSFX clips [Player_Weapons_Ice_Hit_01, Player_Weapons_Ice_Hit_02, Player_Weapons_Ice_Hit_03, Player_Weapons_Ice_Hit_04, Player_Weapons_Ice_Hit_05] pitch 0.9-1.1 vol 0.7-0.75; TimedDestruction |
| sfx_imp_death | net | ZSFX clips [GiantAnt_Death1, GiantAnt_Death2, GiantAnt_Death3] pitch 0.9-1.1 vol 1-1; ZSFX clips [Dragon_BreathFire1, Dragon_BreathFire2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_imp_hit | net | ZSFX clips [GiantAnt_Hurt1, GiantAnt_Hurt2, GiantAnt_Hurt3] pitch 0.9-1.1 vol 0.5-0.5; TimedDestruction |
| sfx_jotunwarrior_death | net | ZSFX clips [Enemy_JotunWarrior_Death_01, Enemy_JotunWarrior_Death_02, Enemy_JotunWarrior_Death_03, Enemy_JotunWarrior_Death_04, Enemy_JotunWarrior_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_jotunwarrior_hurt | net | ZSFX clips [Enemy_JotunWarrior_Hurt_01, Enemy_JotunWarrior_Hurt_02, Enemy_JotunWarrior_Hurt_03, Enemy_JotunWarrior_Hurt_04, Enemy_JotunWarrior_Hurt_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_jotunwitch_death | net | ZSFX clips [Enemy_JotunWitch_Death_01, Enemy_JotunWitch_Death_02, Enemy_JotunWitch_Death_03, Enemy_JotunWitch_Death_04, Enemy_JotunWitch_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_jotunwitch_hurt | net | ZSFX clips [Enemy_JotunWitch_Hurt_01, Enemy_JotunWitch_Hurt_02, Enemy_JotunWitch_Hurt_03, Enemy_JotunWitch_Hurt_04, Enemy_JotunWitch_Hurt_05] pitch 1-1 vol 0.45-0.65; TimedDestruction |
| sfx_kvastur_death | net | ZSFX clips [EvilGhost_SpecterDeath2] pitch 2-3 vol 1-1; ZSFX clips [Dragon_BreathFire1, Dragon_BreathFire2] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_leech_alerted | net | ZSFX clips [Splash_WaterEmerge01, Splash_WaterEmerge02, Splash_WaterEmerge03, Splash_WaterEmerge04, Splash_WaterEmerge05, Splash_WaterEmerge06, Splash_WaterEmerge07, Splash_WaterEmerge08, Splash_WaterEmerge09] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_leech_attack | net | ZSFX clips [LizardMan_AttackHiss1, LizardMan_AttackHiss2] pitch 0.9-1.1 vol 0.7-0.7; TimedDestruction |
| sfx_leech_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 0.5-0.5; TimedDestruction |
| sfx_leech_death | net | ZSFX clips [Blood_Splat_Gib05, Blood_Splat_Gib06, Blood_Splat_Gib07] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_leech_hit | net | ZSFX clips [Blood_Splat_Gib01, Blood_Splat_Gib02, Blood_Splat_Gib03] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_leech_idle | net | ZSFX clips [Splash_WaterEmerge01, Splash_WaterEmerge02, Splash_WaterEmerge03, Splash_WaterEmerge04, Splash_WaterEmerge05, Splash_WaterEmerge06, Splash_WaterEmerge07, Splash_WaterEmerge08, Splash_WaterEmerge09] pitch 0.9-1 vol 0.8-0.9; TimedDestruction |
| sfx_metalbars_hit | net | ZSFX clips [Env_Metal_Bars_Hit_01, Env_Metal_Bars_Hit_02, Env_Metal_Bars_Hit_03, Env_Metal_Bars_Hit_04, Env_Metal_Bars_Hit_05, Env_Metal_Bars_Hit_06] pitch 0.85-1.1 vol 0.85-1; TimedDestruction |
| sfx_moleman_death | net | ZSFX clips [Enemy_Moleman_Death_01, Enemy_Moleman_Death_02, Enemy_Moleman_Death_03, Enemy_Moleman_Death_04, Enemy_Moleman_Death_05] pitch 1-1 vol 0.5-0.65; TimedDestruction |
| sfx_moleman_hurt | net | ZSFX clips [Enemy_Moleman_Hurt_01, Enemy_Moleman_Hurt_02, Enemy_Moleman_Hurt_03, Enemy_Moleman_Hurt_04, Enemy_Moleman_Hurt_05] pitch 1-1 vol 0.4-0.6; TimedDestruction |
| sfx_moose_attack_melee_hit | net | ZSFX clips [Enemy_Moose_AttackMeleeImpact_01, Enemy_Moose_AttackMeleeImpact_02, Enemy_Moose_AttackMeleeImpact_03, Enemy_Moose_AttackMeleeImpact_04, Enemy_Moose_AttackMeleeImpact_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_moose_death | net | ZSFX clips [Enemy_Moose_Death_01, Enemy_Moose_Death_02, Enemy_Moose_Death_03, Enemy_Moose_Death_04, Enemy_Moose_Death_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_moose_death_explode | net | ZSFX clips [Enemy_Moose_DeathExplode_01, Enemy_Moose_DeathExplode_02, Enemy_Moose_DeathExplode_03, Enemy_Moose_DeathExplode_04, Enemy_Moose_DeathExplode_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_moose_hurt | net | ZSFX clips [Enemy_Moose_Alert_01, Enemy_Moose_Alert_02, Enemy_Moose_Alert_03, Enemy_Moose_Alert_04, Enemy_Moose_Alert_05] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_morgen_death | net | ZSFX clips [Enemy_Morgen_Death_01, Enemy_Morgen_Death_02, Enemy_Morgen_Death_03, Enemy_Morgen_Death_04, Enemy_Morgen_Death_05] pitch 1-1 vol 1.4-1.6; TimedDestruction |
| sfx_neck_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 0.5-0.5; TimedDestruction |
| sfx_neck_death | net | ZSFX clips [Hit_LargeInsectSplat1, Hit_LargeInsectSplat2] pitch 0.7-0.8 vol 0.7-0.7; TimedDestruction |
| sfx_neck_hit | net | ZSFX clips [GiantBeetle_Spit1] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_oozebomb_explode | net | ZSFX clips [Player_Weapon_OozeBomb_Impact_S_01, Player_Weapon_OozeBomb_Impact_S_02, Player_Weapon_OozeBomb_Impact_S_03] pitch 0.95-1 vol 1.1-1.3; TimedDestruction |
| sfx_pickaxe_hit | net | ZSFX clips [Hit_Metal_HighPitch1, Hit_Metal_HighPitch7, Hit_Metal_HighPitch5, Hit_Metal_HighPitch3, Hit_Metal_HighPitch2, Hit_Metal_HighPitch4, Hit_Metal_HighPitch6] pitch 0.9-1.1 vol 0.4-0.4; TimedDestruction |
| sfx_pickaxe_hit_vibration_only | net | ZSFX clips [Hit_Metal_HighPitch1_amplified_vibration, Hit_Metal_HighPitch7_amplified_vibration, Hit_Metal_HighPitch5_amplified_vibration, Hit_Metal_HighPitch3_amplified_vibration, Hit_Metal_HighPitch2_amplified_vibration, Hit_Metal_HighPitch4_amplified_vibration, Hit_Metal_HighPitch6_amplified_vibration] pitch 0.9-1.1 vol 0.9-1; TimedDestruction |
| sfx_rock_hit | net | ZSFX clips [Hit_HammerSmashRock2, Hit_HammerSmashRock1] pitch 0.9-1.1 vol 0.6-0.6; TimedDestruction |
| sfx_seal_death | net | ZSFX clips [Enemy_Seal_Death_01, Enemy_Seal_Death_02, Enemy_Seal_Death_03, Enemy_Seal_Death_04, Enemy_Seal_Death_05, Enemy_Seal_Death_07, Enemy_Seal_Death_06] pitch 1-1 vol 0.45-0.55; TimedDestruction |
| sfx_serpent_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 0.8-0.8; TimedDestruction |
| sfx_serpent_death | net | ZSFX clips [Blood_Splat_Gib05, Blood_Splat_Gib06, Blood_Splat_Gib07] pitch 1-1.1 vol 1-1; TimedDestruction |
| sfx_serpent_hurt | net | ZSFX clips [Blood_Splat_Gib01, Blood_Splat_Gib02, Blood_Splat_Gib03] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_shieldgenerator_hit | net | ZSFX clips [Shield_Generator_Hit_01, Shield_Generator_Hit_02, Shield_Generator_Hit_03, Shield_Generator_Hit_04, Shield_Generator_Hit_05, Shield_Generator_Hit_06] pitch 1-1 vol 1.3-1.5; TimedDestruction |
| sfx_silvermace_hit | net | ZSFX clips [Hit_BigMetalPunch] pitch 1-1.2 vol 0.9-0.9; ZSFX clips [MagicSpell_IceBallShoot] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_skeleton_basic_death | net | ZSFX clips [Enemy_Skeleton_Basic_Death_01, Enemy_Skeleton_Basic_Death_02, Enemy_Skeleton_Basic_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_big_death | net | ZSFX clips [Skeleton_Death_BoneShatter1, Skeleton_Hit_Shatter2, Skeleton_Hit_Shatter1] pitch 0.9-1.1 vol 1-1; ZSFX clips [Zombie08_Death1] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_skeleton_death | net | ZSFX clips [Skeleton_Death_BoneShatter1, Skeleton_Hit_Shatter2, Skeleton_Hit_Shatter1] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_skeleton_frozen_death | net | ZSFX clips [Enemy_Skeleton_DeepNorth_Death_01, Enemy_Skeleton_DeepNorth_Death_02, Enemy_Skeleton_DeepNorth_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_hildir_death | net | ZSFX clips [Enemy_Skeleton_Hildir_Death_01, Enemy_Skeleton_Hildir_Death_02, Enemy_Skeleton_Hildir_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_hit | net | ZSFX clips [Skeleton_Death_BoneHit1, Skeleton_Death_BoneHit2, Skeleton_Death_BoneHit3, Skeleton_Hit_Bone1] pitch 0.9-1.1 vol 0.5-0.5; TimedDestruction |
| sfx_skeleton_mace_hit | net | ZSFX clips [DarkMagic_SpellImpact18] pitch 1-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_poison_attack_melee | net | ZSFX clips [Enemy_Skeleton_Poison_Attack_Melee_01, Enemy_Skeleton_Poison_Attack_Melee_02, Enemy_Skeleton_Poison_Attack_Melee_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_poison_death | net | ZSFX clips [Enemy_Skeleton_Poison_Death_01, Enemy_Skeleton_Poison_Death_02, Enemy_Skeleton_Poison_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_poison_verse_attack | net | ZSFX clips [Enemy_Skeleton_Poison_Verse_Attack_01, Enemy_Skeleton_Poison_Verse_Attack_02, Enemy_Skeleton_Poison_Verse_Attack_03, Enemy_Skeleton_Poison_Verse_Attack_04, Enemy_Skeleton_Poison_Verse_Attack_05, Enemy_Skeleton_Poison_Verse_Attack_06, Enemy_Skeleton_Poison_Verse_Attack_07] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_poison_verse_idle | net | ZSFX clips [Enemy_Skeleton_Poison_Verse_Idle_01, Enemy_Skeleton_Poison_Verse_Idle_02, Enemy_Skeleton_Poison_Verse_Idle_03, Enemy_Skeleton_Poison_Verse_Idle_04, Enemy_Skeleton_Poison_Verse_Idle_05, Enemy_Skeleton_Poison_Verse_Idle_06] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_skeleton_swamp_death | net | ZSFX clips [Enemy_Skeleton_Swamp_Death_01, Enemy_Skeleton_Swamp_Death_02, Enemy_Skeleton_Swamp_Death_03] pitch 0.95-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_sledge_hit | net | ZSFX clips [Player_Movement_SledgeHit_M_01, Player_Movement_SledgeHit_M_02, Player_Movement_SledgeHit_M_03] pitch 0.85-1 vol 0.8-0.9; TimedDestruction |
| sfx_sledge_iron_hit | net | ZSFX clips [Player_Movement_SledgeHit_M_01, Player_Movement_SledgeHit_M_02, Player_Movement_SledgeHit_M_03] pitch 0.85-1 vol 0.7-0.8; ZSFX clips [Weapons_IronSledge_Impact_S_01, Weapons_IronSledge_Impact_S_02, Weapons_IronSledge_Impact_S_03] pitch 0.85-1 vol 1.2-1.4; TimedDestruction |
| sfx_snow_hit_debris | net | ZSFX clips [Snow_Hit_Debris_01, Snow_Hit_Debris_02, Snow_Hit_Debris_03, Snow_Hit_Debris_04, Snow_Hit_Debris_05] pitch 0.8-1.1 vol 1-1; TimedDestruction |
| sfx_snow_hit_transient | net | ZSFX clips [Snow_Hit_Transient_01, Snow_Hit_Transient_02, Snow_Hit_Transient_03, Snow_Hit_Transient_04, Snow_Hit_Transient_05] pitch 0.8-1 vol 1.5-1.5; TimedDestruction |
| sfx_spear_flint_hit | net | ZSFX clips [Player_Movement_Spear_Flint_Hit_M_01, Player_Movement_Spear_Flint_Hit_M_02, Player_Movement_Spear_Flint_Hit_M_03, Player_Movement_Spear_Flint_Hit_M_04] pitch 0.8-1 vol 1-1.2; TimedDestruction |
| sfx_spear_hit | net | ZSFX clips [Player_Movement_Spear_Hit_M_01, Player_Movement_Spear_Hit_M_02, Player_Movement_Spear_Hit_M_03, Player_Movement_Spear_Hit_M_04, Player_Movement_Spear_Hit_M_05] pitch 0.8-1 vol 1-1.2; TimedDestruction |
| sfx_staff_elder_root_hit | net | ZSFX clips [Player_Weapons_ElderStaff_RootHit_01, Player_Weapons_ElderStaff_RootHit_02, Player_Weapons_ElderStaff_RootHit_03, Player_Weapons_ElderStaff_RootHit_04, Player_Weapons_ElderStaff_RootHit_05] pitch 1-1 vol 2.4-2.4; TimedDestruction |
| sfx_staffthunderblood_cast | net | ZSFX clips [Weapons_StaffThunderBlood_Cast_01, Weapons_StaffThunderBlood_Cast_02, Weapons_StaffThunderBlood_Cast_03, Weapons_StaffThunderBlood_Cast_04] pitch 0.85-1.15 vol 1.2-1.45; TimedDestruction |
| sfx_staffthunderblood_thunder | net | ZSFX clips [Weapons_StaffThunderBlood_Thunder_01, Weapons_StaffThunderBlood_Thunder_02, Weapons_StaffThunderBlood_Thunder_03, Weapons_StaffThunderBlood_Thunder_04] pitch 0.85-1.15 vol 1.1-1.3; TimedDestruction |
| sfx_stonegolem_attack_hit | net | ZSFX clips [Smash_RockShatter] pitch 0.8-1 vol 0.8-0.8; TimedDestruction |
| sfx_stonegolem_death | net | ZSFX clips [Smash_HugeRockImpact3] pitch 0.9-1.1 vol 1-1; ZSFX clips [EvilMonster_Breath] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_stonegolem_hurt | net | ZSFX clips [Smash_Gravestone3, Smash_Gravestone4, Smash_Gravestone5] pitch 0.8-1 vol 0.8-0.8; TimedDestruction |
| sfx_sword_hit | net | ZSFX clips [Player_Movement_Sword_Hit_M_01, Player_Movement_Sword_Hit_M_02, Player_Movement_Sword_Hit_M_03, Player_Movement_Sword_Hit_M_04, Player_Movement_Sword_Hit_M_05] pitch 0.75-0.95 vol 0.5-0.5; ZSFX clips [Player_Movement_Sword_Hit_Overlay_M_01, Player_Movement_Sword_Hit_Overlay_M_02, Player_Movement_Sword_Hit_Overlay_M_03, Player_Movement_Sword_Hit_Overlay_M_04, Player_Movement_Sword_Hit_Overlay_M_05] pitch 0.8-1.1 vol 0.7-1; TimedDestruction |
| sfx_tick_alerted | net | ZSFX clips [Enemy_Tick_Alerted_01, Enemy_Tick_Alerted_02, Enemy_Tick_Alerted_03, Enemy_Tick_Alerted_04, Enemy_Tick_Alerted_05, Enemy_Tick_Alerted_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_attack_drain | net | ZSFX clips [Enemy_Tick_Drain_01, Enemy_Tick_Drain_02, Enemy_Tick_Drain_03, Enemy_Tick_Drain_04, Enemy_Tick_Drain_05, Enemy_Tick_Drain_06, Enemy_Tick_Drain_07, Enemy_Tick_Drain_08] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_attack_jump | net | ZSFX clips [Enemy_Tick_DrainAttack_01, Enemy_Tick_DrainAttack_02, Enemy_Tick_DrainAttack_03, Enemy_Tick_DrainAttack_04, Enemy_Tick_DrainAttack_05, Enemy_Tick_DrainAttack_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_hurt | net | ZSFX clips [Enemy_Tick_Hurt_01, Enemy_Tick_Hurt_02, Enemy_Tick_Hurt_03, Enemy_Tick_Hurt_04, Enemy_Tick_Hurt_05, Enemy_Tick_Hurt_06] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_tick_idle | net | ZSFX clips [Enemy_Tick_Idle_01, Enemy_Tick_Idle_02, Enemy_Tick_Idle_03, Enemy_Tick_Idle_04, Enemy_Tick_Idle_05, Enemy_Tick_Idle_06, Enemy_Tick_Idle_07, Enemy_Tick_Idle_08, Enemy_Tick_Idle_09, Enemy_Tick_Idle_10] pitch 0.9-1.1 vol 0.5-0.8; TimedDestruction |
| sfx_tree_fall_hit | net | ZSFX clips [Objects_Tree_Fall_Hit_01, Objects_Tree_Fall_Hit_02, Objects_Tree_Fall_Hit_03, Objects_Tree_Fall_Hit_04, Objects_Tree_Fall_Hit_05, Objects_Tree_Fall_Hit_06] pitch 0.8-1 vol 0.35-0.4; TimedDestruction |
| sfx_tree_hit | net | ZSFX clips [Env_Tree_Hit_S_01, Env_Tree_Hit_S_02, Env_Tree_Hit_S_03, Env_Tree_Hit_S_04, Env_Tree_Hit_S_05, Env_Tree_Hit_S_06, Env_Tree_Hit_S_07] pitch 0.8-1.1 vol 0.4-0.6; TimedDestruction |
| sfx_tree_hit_abomination | net | ZSFX clips [Env_Tree_Hit_S_01, Env_Tree_Hit_S_02, Env_Tree_Hit_S_03, Env_Tree_Hit_S_04, Env_Tree_Hit_S_05, Env_Tree_Hit_S_06, Env_Tree_Hit_S_07] pitch 0.8-1.1 vol 0.4-0.6; TimedDestruction |
| sfx_troll_attack_hit | net | ZSFX clips [Hit_DeepHugeChestThump1] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_troll_death | net | ZSFX clips [RockMonster_DeathRoar1] pitch 0.9-1.1 vol 1-1; TimedDestruction |
| sfx_troll_hit | net | ZSFX clips [Hit_SlimeSplat4, Hit_SlimeSplat8] pitch 0.9-1 vol 0.5-0.5; TimedDestruction |
| sfx_trollfire_death | net | ZSFX clips [Enemy_Firetroll_Death_01, Enemy_Firetroll_Death_02, Enemy_Firetroll_Death_03] pitch 0.9-1.1 vol 1.6-1.6; TimedDestruction |
| sfx_ulv_death | net | ZSFX clips [Zombie09_Death4, Zombie09_Death5] pitch 0.9-1 vol 0.6-0.6; TimedDestruction |
| sfx_unarmed_hit | net | ZSFX clips [Player_Movement_Unarmed_Hit_M_01, Player_Movement_Unarmed_Hit_M_02, Player_Movement_Unarmed_Hit_M_03, Player_Movement_Unarmed_Hit_M_04] pitch 0.8-1.1 vol 0.8-1; TimedDestruction |
| sfx_unbjorn_death | net | ZSFX clips [Enemy_Unbjorn_Death_01, Enemy_Unbjorn_Death_02, Enemy_Unbjorn_Death_03, Enemy_Unbjorn_Death_04, Enemy_Unbjorn_Death_05, Enemy_Unbjorn_Death_06] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_unbjorn_hurt | net | ZSFX clips [Enemy_Unbjorn_Hurt_01, Enemy_Unbjorn_Hurt_02, Enemy_Unbjorn_Hurt_03, Enemy_Unbjorn_Hurt_04, Enemy_Unbjorn_Hurt_05, Enemy_Unbjorn_Hurt_06, Enemy_Unbjorn_Hurt_07, Enemy_Unbjorn_Hurt_08] pitch 1-1 vol 0.3-0.45; TimedDestruction |
| sfx_vulture_death | net | ZSFX clips [Enemy_Vulture_Death_01, Enemy_Vulture_Death_02, Enemy_Vulture_Death_03, Enemy_Vulture_Death_04] pitch 0.9-1.1 vol 0.9-1; TimedDestruction |
| sfx_wolf_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 0.5-0.5; TimedDestruction |
| sfx_wolf_death | net | ZSFX clips [ZombieDog_Death1, ZombieDog_Death2] pitch 0.9-1 vol 1-1; TimedDestruction |
| sfx_wolf_hit | net | ZSFX clips [ZombieDog_Hurt1] pitch 0.9-1.1 vol 0.5-0.5; TimedDestruction |
| sfx_wood_hit | net | ZSFX clips [Crash_WoodBeam5_1] pitch 1-1.2 vol 0.4-0.3; TimedDestruction |
| sfx_wraith_attack_hit | net | ZSFX clips [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] pitch 0.8-0.9 vol 1-1; TimedDestruction |
| sfx_wraith_death | net | ZSFX clips [EvilGhost_Death3] pitch 1-1.1 vol 1-1; ZSFX clips [FlameThrower_ShotBurst2, FlameThrower_ShotBurst3, FlameThrower_ShotBurst4, FlameThrower_ShotBurst5] pitch 0.9-1.1 vol 0.8-0.8; TimedDestruction |
| sfx_wraith_hit | net | ZSFX clips [Ghost05_Hurt_Grunt] pitch 1.1-1.2 vol 0.8-0.8; TimedDestruction |
| sfx_writhan_death_charge | net | ZSFX clips [Enemy_Writhan_Death_Charge_01, Enemy_Writhan_Death_Charge_02, Enemy_Writhan_Death_Charge_03] pitch 1-1 vol 0.6-0.8; TimedDestruction |
| sfx_writhan_death_explosion | net | ZSFX clips [Enemy_Writhan_Death_Explosion_01, Enemy_Writhan_Death_Explosion_02, Enemy_Writhan_Death_Explosion_03] pitch 0.95-1.05 vol 0.75-1; TimedDestruction |
| sfx_writhan_verse_death | net | ZSFX clips [Enemy_Writhan_Verse_Death_01, Enemy_Writhan_Verse_Death_02, Enemy_Writhan_Verse_Death_03, Enemy_Writhan_Verse_Death_04] pitch 1-1 vol 0.6-0.8; TimedDestruction |
| vfx_ArcheryTarget_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_BigBlob_destroyed | net | Gibber, ZNetView |
| vfx_BloodDeath | net | 5 ParticleSystem; TimedDestruction |
| vfx_BloodHit | net | 5 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_frost | net | 3 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_lava | net | 5 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_morkhalla | net | 3 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_poison | net | 3 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_poisonelite | net | 3 ParticleSystem; TimedDestruction |
| vfx_BombBlob_explode_tar | net | 3 ParticleSystem; TimedDestruction |
| vfx_BonemassDeath | net | 8 ParticleSystem; TimedDestruction |
| vfx_BonemassHit | net | 6 ParticleSystem; TimedDestruction |
| vfx_BugRepellent | net | 4 ParticleSystem |
| vfx_Burning_green | net | ZSFX clips [UI_Player_FireDamage_Ignite_01, UI_Player_FireDamage_Ignite_02, UI_Player_FireDamage_Ignite_03, UI_Player_FireDamage_Ignite_04] pitch 1-1 vol 1-1; ZSFX clips [UI_Player_FireDamage_Burn_Loop] pitch 1-1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| vfx_CoalHit | net | 2 ParticleSystem; TimedDestruction |
| vfx_ColdBall_Hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_FallenWarrior_death | net | 2 ParticleSystem; TimedDestruction |
| vfx_FallenWarrior_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_FireballHit | net | 4 ParticleSystem; TimedDestruction |
| vfx_Firework_Rocket_Green | net | 4 ParticleSystem; TimedDestruction |
| vfx_GuckSackDestroyed | net | 4 ParticleSystem; TimedDestruction |
| vfx_GuckSackHit | net | 4 ParticleSystem; TimedDestruction |
| vfx_GuckSackSmall_Destroyed | net | 4 ParticleSystem; TimedDestruction |
| vfx_HitSparks | net | 2 ParticleSystem; TimedDestruction |
| vfx_ImpDeath | net | 3 ParticleSystem; TimedDestruction |
| vfx_MarbleHit | net | 2 ParticleSystem; TimedDestruction |
| vfx_MudHit | net | 4 ParticleSystem; TimedDestruction |
| vfx_Poison | net | 3 ParticleSystem |
| vfx_ProjectileHit | net | 4 ParticleSystem; TimedDestruction |
| vfx_RockHit | net | 2 ParticleSystem; TimedDestruction |
| vfx_RockHit_Marble | net | 2 ParticleSystem; TimedDestruction |
| vfx_RockHit_Obsidian | net | 3 ParticleSystem; TimedDestruction |
| vfx_ShadowPerson_death | net | 2 ParticleSystem; TimedDestruction |
| vfx_ShadowPerson_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_Slimed | net | 1 ParticleSystem |
| vfx_TrainingDummy_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_TrollFrost_Death | net | 2 ParticleSystem; TimedDestruction |
| vfx_arrowhit | net | 2 ParticleSystem; TimedDestruction |
| vfx_ashlandsbow_blood_fire | net | 2 ParticleSystem; TimedDestruction |
| vfx_barnacle_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_beehive_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_blob_attack | net | 4 ParticleSystem; TimedDestruction |
| vfx_blob_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_blob_frost_attack | net | 3 ParticleSystem; TimedDestruction |
| vfx_blob_frost_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_blob_frost_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_blob_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_blobelite_attack | net | 4 ParticleSystem; TimedDestruction |
| vfx_blobmork_attack | net | 4 ParticleSystem; TimedDestruction |
| vfx_blobmork_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_blobmork_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_blobtar_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_blobtar_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_boar_birth | net | 2 ParticleSystem; TimedDestruction |
| vfx_boar_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_boar_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_boar_love | net | 1 ParticleSystem; TimedDestruction |
| vfx_bonemawserpent_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_bush2_e_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_clubhit | net | 1 ParticleSystem; TimedDestruction |
| vfx_crossbow_blood_fire | net | 3 ParticleSystem; TimedDestruction |
| vfx_crow_death | net | 3 ParticleSystem; TimedDestruction |
| vfx_deer_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_deer_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_dragon_death | net | ZSFX clips [FlameThrower_ShotBurst2, FlameThrower_ShotBurst3, FlameThrower_ShotBurst4, FlameThrower_ShotBurst5] pitch 0.9-1 vol 0.8-0.8; 11 ParticleSystem; TimedDestruction |
| vfx_dragon_hurt | net | 6 ParticleSystem; TimedDestruction |
| vfx_dragon_ice_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_dragonegg_destroy | net | 5 ParticleSystem; TimedDestruction |
| vfx_draugr_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_draugr_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_draugrpile_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_eikthyr_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_fenring_cultist_death | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| vfx_fenring_cultist_hildir_death | net | ZSFX clips [FlameThrower_ShotLoop4, FlameThrower_ShotLoop5] pitch 0.9-1.1 vol 1-1; 8 ParticleSystem; TimedDestruction |
| vfx_fenring_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_fenring_hurt | net | 6 ParticleSystem; TimedDestruction |
| vfx_firetreecut_snow_FrostTrollDeath | net | 3 ParticleSystem; TimedDestruction |
| vfx_foresttroll_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_frostarrow_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_frosttroll_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_frozenking_death | net | ZSFX clips [Wind_RushingStormLoop] pitch 1-1 vol 1-1; 13 ParticleSystem; TimedDestruction |
| vfx_frozenking_final_death | net | ZSFX clips [Wind_RushingStormLoop] pitch 1-1 vol 1-1; 51 ParticleSystem; TimedDestruction |
| vfx_frozenking_final_death_ground | net | 8 ParticleSystem; TimedDestruction |
| vfx_ghost_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_ghost_hit | net | 1 ParticleSystem; TimedDestruction |
| vfx_goblin_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblin_dn_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblin_dn_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblin_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblinbrute_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblinbrute_hildir_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_goblinbrute_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_greydwarf_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_greydwarf_elite_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_greydwarf_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_greydwarfnest_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_hatchling_death | net | 13 ParticleSystem; TimedDestruction |
| vfx_hatchling_hurt | net | 6 ParticleSystem; TimedDestruction |
| vfx_hjall_spit_hit | net | ZSFX clips [Enemy_Gjall_Spit_Impact_01, Enemy_Gjall_Spit_Impact_02, Enemy_Gjall_Spit_Impact_03, Enemy_Gjall_Spit_Impact_04] pitch 0.9-1 vol 1-1; 10 ParticleSystem; TimedDestruction |
| vfx_ice_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_leech_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_leech_hit | net | 5 ParticleSystem; TimedDestruction |
| vfx_neck_death | net | 8 ParticleSystem; TimedDestruction |
| vfx_neck_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_player_death | net | 6 ParticleSystem; TimedDestruction |
| vfx_player_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_poisonarrow_hit | net | 3 ParticleSystem; TimedDestruction |
| vfx_seagull_death | net | 3 ParticleSystem; TimedDestruction |
| vfx_seal_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_seekerbrute_groundslam | net | 6 ParticleSystem; TimedDestruction |
| vfx_serpent_death | net | 7 ParticleSystem; TimedDestruction |
| vfx_serpent_hurt | net | 5 ParticleSystem; TimedDestruction |
| vfx_shrub_2_hit | net | 1 ParticleSystem; TimedDestruction |
| vfx_shrub_heath_hit | net | 1 ParticleSystem; TimedDestruction |
| vfx_silvermace_hit | net | 3 ParticleSystem; TimedDestruction |
| vfx_skeleton_big_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_skeleton_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_skeleton_fire_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_skeleton_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_skeleton_mace_hit | net | 3 ParticleSystem; TimedDestruction |
| vfx_sledge_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_sledge_iron_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_stonegolem_attack_hit | net | 4 ParticleSystem; TimedDestruction |
| vfx_stonegolem_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_stonegolem_hurt | net | 4 ParticleSystem; TimedDestruction |
| vfx_torch_hit | net | 3 ParticleSystem; TimedDestruction |
| vfx_tree_fall_hit | net | 1 ParticleSystem; TimedDestruction |
| vfx_troll_attack_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_troll_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_troll_log_hitground | net | 3 ParticleSystem; TimedDestruction |
| vfx_troll_summoned_death | net | 4 ParticleSystem; TimedDestruction |
| vfx_trollsnow_attack_hit | net | 2 ParticleSystem; TimedDestruction |
| vfx_ulv_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_wolf_death | net | 5 ParticleSystem; TimedDestruction |
| vfx_wolf_hit | net | 6 ParticleSystem; TimedDestruction |
| vfx_wraith_death | net | 10 ParticleSystem; TimedDestruction |
| vfx_wraith_hit | net | 1 ParticleSystem; TimedDestruction |

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

ObjectDB: 1525 items, 481 recipes, 358 with durability.

### Wisplight (Demister and tokens containing demister / wisp)

| Prefab | m_name | Type | UseDurability | MaxDurability | DurabilityPerLevel | MaxQuality | Recipe |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Demister | $item_demister (Lumière de feu-follet) | Utility | True | 100 | 0 | 1 | piece_workbench ($piece_workbench) lvl 1: Silver x1 (+1/lvl), Wisp x1 (+1/lvl) |
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

`Legacy Shaders/Diffuse` (2 properties): `_Color` Color, `_MainTex` Texture

`Custom/Vegetation` (35 properties): `_MainTex` Texture, `_Color` Color, `_Glossiness` Range, `_Metallic` Range, `_Cutoff` Range, `_USEMETALMAP` Float, `_MetalTex` Texture, `_MetalGloss` Range, `_BumpMap` Texture, `_BumpScale` Float, `_TwoSidedNormals` Float, `_SphereNormals` Range, `_SphereOffset` Float, `_EmissiveTex` Texture, `_EmissionColor` Color, `_MossTex` Texture, `_MossAlpha` Range, `_MossBlend` Range, `_MossNormal` Range, `_MossTransition` Float, `_AddSnow` Float, `_SnowSparkleLum` Float, `_AddRain` Float, `_UV2Height` Float, `_Height` Float, `_SwaySpeed` Float, `_SwayDistance` Float, `_RippleSpeed` Float, `_RippleDistance` Float, `_RippleDeadzoneMin` Range, `_RippleDeadzoneMax` Range, `_PushDistance` Float, `_PushClothMode` Float, `_CamCull` Float, `_Cull` Float

`Custom/Player` (14 properties): `_Cutoff` Range, `_MainTex` Texture, `_SkinBumpMap` Texture, `_SkinColor` Color, `_ChestTex` Texture, `_ChestBumpMap` Texture, `_ChestMetal` Texture, `_LegsTex` Texture, `_LegsBumpMap` Texture, `_LegsMetal` Texture, `_BumpScale` Float, `_Glossiness` Range, `_MetalGlossiness` Range, `_SnowCover` Range

### Shader usage across all prefabs

| Shader | Material slots | Prefabs | isSupported | Sample prefabs |
| --- | --- | --- | --- | --- |
| Custom/Piece | 7947 | 785 | True | Acorn, ArmorStand, ArmorStand_Female, ArmorStand_Male, Ashland_Steepstair |
| Custom/StaticRock | 6980 | 418 | True | ArrowFrost, ArrowObsidian, ArrowPoison, Ashlands_rock1, Ashlands_rock2 |
| Custom/Trilinearmap | 3709 | 34 | True | HeathRockPillar_frac, RockFingerBroken_frac, RockFinger_frac, RockThumb_frac, Rock_3_deepnorth_frac |
| Standard | 3544 | 1184 | True | Amber, AmberPearl, AncientCoin, AncientGemstoneBlack, AncientGemstoneGreen |
| Custom/Creature | 1585 | 765 | True | Abomination, Abomination_ragdoll, ArmorAshlandsMediumChest, ArmorAshlandsMediumlegs, ArmorBerserkerChest |
| Legacy Shaders/Particles/Alpha Blended | 1330 | 1205 | True | Acorn, Amber, AmberPearl, AncientCoin, AncientGemstoneBlack |
| Lux Lit Particles/ Bumped | 1296 | 804 | True | Abomination, ArrowPoison, Aspect_Bonemass, Aspect_Elder, Aspect_Yagluth |
| Particles/Standard Unlit2 | 1105 | 574 | True | ArrowFrost, ArrowNeedle, Aspect_Eikthyr, Aspect_Fader, AtgeirBlackmetal |
| Particles/Standard Surface2 | 636 | 436 | True | Abomination, Aspect_Bonemass, Aspect_Eikthyr, Aspect_Elder, Aspect_SeekerQueen |
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

### Worm proxy layer (phase 3)

proxy layer matrix (ignore = no physical collision): hitbox(18){character=ignore character_net=ignore character_ghost=ignore character_noenv=ignore Default=ignore static_solid=ignore terrain=ignore } character_noenv(27){character=COLLIDES character_net=COLLIDES character_ghost=ignore character_noenv=COLLIDES Default=ignore static_solid=ignore terrain=ignore } -> hitbox (clean). `Physics.GetIgnoreLayerCollision` read at runtime; see decisions.md 47.

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

## 9. Custom/Creature shader: keywords, defaults, ranges

### Keyword space

27 keywords: `STEREO_INSTANCING_ON`, `UNITY_SINGLE_PASS_STEREO`, `STEREO_MULTIVIEW_ON`, `STEREO_CUBEMAP_RENDER_ON`, `LOD_FADE_CROSSFADE`, `INSTANCING_ON`, `FOG_LINEAR`, `FOG_EXP`, `FOG_EXP2`, `DIRECTIONAL`, `LIGHTPROBE_SH`, `SHADOWS_SHADOWMASK`, `DYNAMICLIGHTMAP_ON`, `LIGHTMAP_ON`, `LIGHTMAP_SHADOW_MIXING`, `DIRLIGHTMAP_COMBINED`, `SHADOWS_SCREEN`, `VERTEXLIGHT_ON`, `NOISEGLOW`, `POINT`, `SPOT`, `POINT_COOKIE`, `DIRECTIONAL_COOKIE`, `UNITY_HDR_ON`, `SHADOWS_DEPTH`, `SHADOWS_CUBE`, `EDITOR_VISUALIZATION`

### Property defaults and range limits

| Property | Type | Default | Range limits |
| --- | --- | --- | --- |
| _Cutoff | Range | 0.5 | 0 .. 1 |
| _Glossiness | Range | 0.5 | 0 .. 1 |
| _MetalGloss | Range | 0 | 0 .. 1 |
| _NoiseGlowEdge | Range | 0 | 0 .. 1 |
| _BumpScale | Range | 1 | 0 .. 10 |

## 10. Particle systems of priority and hit / hurt / blood effects

Detailed: the priority list (19) plus every sfx_/vfx_/fx_ prefab whose name contains blood, hurt or hit (222 prefabs). Missing priority prefabs: none.

| Prefab | Priority | ParticleSystem | Details |
| --- | --- | --- | --- |
| fx_Abomination_attack_hit |  | fx_Abomination_attack_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.01 .. 0.05 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_Abomination_attack_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.03 \| startLifetime: Constant 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| fx_Abomination_attack_hit |  | dust | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| fx_Abomination_attack_hit |  | twiggs | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: TwoConstants 4 .. 7 \| maxParticles 100 \| simulationSpace World \| renderer: mode Mesh; shader Standard; material branch; _MainTex acacia_trunk01 (256x256); _Color RGBA(1, 0.933, 0.933, 1) |
| fx_BonemawSerpent_Spit_Hit |  | fx_BonemawSerpent_Spit_Hit | startColor: Color RGBA(0.753, 0.63, 0.024, 1) \| startSize: TwoConstants 0.1 .. 0.4 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_BonemawSerpent_Spit_Hit |  | Slime | startColor: Color RGBA(0.858, 0.715, 0.061, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_BonemawSerpent_Spit_Hit |  | Stretchy Slime | startColor: Color RGBA(0.858, 0.715, 0.061, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_BonemawSerpent_Spit_Hit |  | Smoke | startColor: Color RGBA(0.632, 0.553, 0.051, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 3 .. 5 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_Cinder_hit |  | Sparks | startColor: Color RGBA(1, 0.182, 0.043, 1) \| startSize: TwoConstants 0.01 .. 0.05 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material fireworks_poof; _MainTex Firework (256x256); _Color RGBA(5.216, 5.216, 5.216, 1) |
| fx_Cinder_hit |  | Sparks/Big Smoke | startColor: TwoColors RGBA(0, 0, 0, 1) .. RGBA(0.108, 0.113, 0.132, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 3 .. 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke_hard; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_Cinder_hit |  | Sparks/flames (1) | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_Cinder_storm_hit |  | Sparks | startColor: Color RGBA(1, 0.182, 0.043, 1) \| startSize: TwoConstants 0.01 .. 0.05 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material fireworks_poof; _MainTex Firework (256x256); _Color RGBA(5.216, 5.216, 5.216, 1) |
| fx_Cinder_storm_hit |  | Sparks/Big Smoke | startColor: TwoColors RGBA(0, 0, 0, 1) .. RGBA(0.108, 0.113, 0.132, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 3 .. 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke_hard; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_Cinder_storm_hit |  | Sparks/flames (1) | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_DvergerMage_Fire_hit |  | fx_DvergerMage_Fire_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material gnista; _MainTex crystal (64x64); _Color RGBA(2, 2, 2, 1) |
| fx_DvergerMage_Fire_hit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_DvergerMage_Fire_hit |  | fire | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.5 .. 2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_DvergerMage_Fire_hit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_DvergerMage_Fire_hit |  | fire | startColor: TwoColors RGBA(1, 0.984, 0.934, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 0.3 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Gradient Mapped Particle (Unlit); material fireball_large; _MainTex flameball_big_flipbook (512x512) |
| fx_DvergerMage_Fire_hit |  | vfx_FireballHit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material gnista; _MainTex crystal (64x64); _Color RGBA(2, 2, 2, 1) |
| fx_DvergerMage_Ice_hit |  | fx_DvergerMage_Ice_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material cold_sparcs; _MainTex none; _Color RGBA(0.961, 1.877, 2.42, 1) |
| fx_DvergerMage_Ice_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| fx_DvergerMage_Ice_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_DvergerMage_Ice_hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_DvergerMage_Support_hit |  | shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 6 \| startLifetime: Constant 0.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material shaman_shield; _MainTex rain_cloud (1024x1024); _Color RGBA(0.714, 0.764, 4.001, 0.098) |
| fx_DvergerMage_Support_hit |  | rising particles | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.2 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material goblinshaman_shield_particles; _MainTex spark 1 (64x64); _Color RGBA(0.162, 0.555, 2.024, 1) |
| fx_DvergerMage_Support_hit |  | delayed_shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 12 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Unlit2; material shaman_shield; _MainTex rain_cloud (1024x1024); _Color RGBA(0.714, 0.764, 4.001, 0.098) |
| fx_DvergerMage_Support_hit |  | dust | startColor: Color RGBA(0.481, 0.481, 0.481, 0.325) \| startSize: TwoConstants 0.3 .. 0.5 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material goblinshaman_shield_particles; _MainTex spark 1 (64x64); _Color RGBA(0.162, 0.555, 2.024, 1) |
| fx_Dverger_hit |  | fx_Dverger_hit | startColor: TwoColors RGBA(0.232, 0.772, 0.097, 0.728) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_Dverger_hit |  | wetsplsh | startColor: Color RGBA(0.743, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_Dverger_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_Dverger_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_Dverger_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_Dverger_hit |  | skinflakes | startColor: Color RGBA(0.122, 0.304, 0.62, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_Eat_Green | yes | fx_TickBloodHit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_Eat_Green | yes | fx_TickBloodHit/wetsplsh | startColor: Color RGBA(0.036, 0.179, 0.044, 0.604) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: TwoConstants 0.3 .. 0.7 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material queenslime; _MainTex slime_splash_2 (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_Eat_Green | yes | fx_TickBloodHit/blood_drops | startColor: TwoColors RGBA(0.575, 0.108, 0.09, 0.491) .. RGBA(0.059, 0.462, 0.11, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_Eat_Green | yes | fx_TickBloodHit/bloodchunks | startColor: Color RGBA(0, 0.557, 0.264, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_Fader_Roar_Projectile_Hit |  | Spores | startColor: TwoColors RGBA(0, 1, 0.373, 1) .. RGBA(0, 1, 0.178, 0.482) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_Fader_Roar_Projectile_Hit |  | Spores/Flame Spikes | startColor: Color RGBA(1, 1, 1, 0.49) \| startSize: TwoConstants 1 .. 4 \| startLifetime: TwoConstants 0.3 .. 0.4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material fireball_large; _MainTex flameball_big_flipbook (512x512) |
| fx_Fader_Roar_Projectile_Hit |  | Spores/Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 10.3 \| startLifetime: Constant 0.3 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_Fader_Roar_Projectile_Hit |  | Spores/Swirls | startColor: TwoColors RGBA(0.259, 1, 0.522, 1) .. RGBA(0.325, 0.831, 0.345, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: None, no sharedMaterial |
| fx_Fader_Roar_Projectile_Hit |  | Spores/Swirly Spores | startColor: TwoColors RGBA(0, 1, 0.491, 1) .. RGBA(0.108, 1, 0.22, 0.482) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_GoblinShieldHit |  | shards | startColor: TwoColors RGBA(1, 0.108, 0.292, 0.684) .. RGBA(0.647, 0.38, 1, 0.753) \| startSize: TwoConstants 0.1 .. 0.4 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Unlit2; material shaman_shield; _MainTex rain_cloud (1024x1024); _Color RGBA(0.714, 0.764, 4.001, 0.098) |
| fx_GoblinShieldHit |  | rising particles | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.2 \| startLifetime: TwoConstants 3 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material goblinshaman_shield_particles; _MainTex spark 1 (64x64); _Color RGBA(0.162, 0.555, 2.024, 1) |
| fx_Puke | yes | tardrop | startColor: TwoColors RGBA(0.613, 0.246, 0.046, 1) .. RGBA(0.282, 0.632, 0.107, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 1.5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material tar_drop; _MainTex blood_drop (32x32); _Color RGBA(1, 1, 1, 1) |
| fx_Puke | yes | tardrop/blob_splat | startColor: TwoColors RGBA(0.755, 0.244, 0, 1) .. RGBA(0.057, 0.726, 0.027, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_Puke | yes | flow_global | startColor: TwoColors RGBA(0.547, 0.175, 0.119, 1) .. RGBA(0.062, 0.292, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Stretch; shader Custom/AlphaParticle; material puke; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_Puke | yes | flow_global/blob_splat | startColor: TwoColors RGBA(0.2, 0.08, 0.049, 1) .. RGBA(0.06, 0.2, 0.051, 1) \| startSize: TwoConstants 0.5 .. 1.2 \| startLifetime: Constant 3 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material puke; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_Puke | yes | flow_local | startColor: TwoColors RGBA(0.613, 0.079, 0, 1) .. RGBA(0, 0.774, 0.033, 1) \| startSize: TwoConstants 0.1 .. 0.5 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/AlphaParticle; material puke; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_StaffShield_Hit |  | shards | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.4 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/Distortion; material staff_shield_shard; _MainTex rain_cloud (1024x1024); _Color RGBA(1, 0, 0, 0.518) |
| fx_StaffShield_Hit |  | rising particles | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: TwoConstants 3 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material staff_shield_particles; _MainTex spark 1 (64x64); _Color RGBA(1.58, 0, 0.054, 1) |
| fx_StaffShield_Hit |  | smoke_world (1) | startColor: TwoColors RGBA(1, 0.108, 0.292, 0.831) .. RGBA(0.647, 0.38, 1, 0.788) \| startSize: TwoConstants 0.3 .. 0.8 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material staff_shield; _MainTex water_ring_thin (128x128); _Color RGBA(4.001, 0.733, 0.895, 0.012) |
| fx_TickBloodHit | yes | fx_TickBloodHit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_TickBloodHit | yes | wetsplsh | startColor: Color RGBA(0.971, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: TwoConstants 0.3 .. 0.7 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_TickBloodHit | yes | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_TickBloodHit | yes | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_TickBloodHit | yes | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_babyseeker_death | yes | fx_babyseeker_death | startColor: Color RGBA(0.691, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material blood_splat2; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_death | yes | skinflakes | startColor: TwoColors RGBA(1, 0.854, 0.505, 0.566) .. RGBA(0.953, 0.914, 0.643, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: TwoConstants 1 .. 1.2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_death | yes | drops-withdecals | startColor: TwoColors RGBA(0.519, 0.604, 0.225, 0.491) .. RGBA(0.642, 0.792, 0.325, 1) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_death | yes | drops-withdecals/blood_decal | startColor: TwoColors RGBA(0.595, 0.623, 0.173, 1) .. RGBA(0.706, 0.571, 0.243, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: TwoConstants 10 .. 11 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/Decal; material seeker_blood_splat; _MainTex brains (64x64); _Color RGBA(0.557, 0.557, 0.557, 1) |
| fx_babyseeker_death | yes | wetsplsh | startColor: TwoColors RGBA(0.651, 0.451, 0.052, 1) .. RGBA(0.726, 0.629, 0.051, 1) \| startSize: TwoConstants 0.4 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_babyseeker_death | yes | Legs | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.3 .. 0.3 \| startLifetime: TwoConstants 5 .. 8 \| maxParticles 10 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material babyseeker; _MainTex babyseeker_d (64x64); _Color RGBA(1, 0.915, 0.552, 1) |
| fx_babyseeker_death | yes | Wings | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.3 .. 0.3 \| startLifetime: TwoConstants 5 .. 8 \| maxParticles 10 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material babyseeker; _MainTex babyseeker_d (64x64); _Color RGBA(1, 0.915, 0.552, 1) |
| fx_babyseeker_death | yes | Carapace | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: TwoConstants 3 .. 4 \| maxParticles 100 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material babyseeker; _MainTex babyseeker_d (64x64); _Color RGBA(1, 0.915, 0.552, 1) |
| fx_babyseeker_hurt | yes | fx_babyseeker_hurt | startColor: Color RGBA(0, 0.859, 0.218, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_hurt | yes | wetsplsh | startColor: TwoColors RGBA(0.784, 0.625, 0.098, 1) .. RGBA(0.783, 0.777, 0.144, 1) \| startSize: TwoConstants 0.4 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_babyseeker_hurt | yes | blood_drops | startColor: TwoColors RGBA(0.726, 0.629, 0.476, 0.491) .. RGBA(0.802, 0.784, 0.42, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_hurt | yes | drops-withdecals | startColor: TwoColors RGBA(0.557, 0.395, 0, 0.491) .. RGBA(0.604, 0.561, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_babyseeker_hurt | yes | drops-withdecals/blood_decal | startColor: Color RGBA(0.623, 0.483, 0.05, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/Decal; material seeker_blood_splat; _MainTex brains (64x64); _Color RGBA(0.557, 0.557, 0.557, 1) |
| fx_babyseeker_hurt | yes | skinflakes | startColor: TwoColors RGBA(0.537, 0.339, 0.275, 1) .. RGBA(1, 0.901, 0.59, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_bat_hit |  | fx_bat_hit | startColor: Color RGBA(0.8, 0.088, 0.043, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_bat_hit |  | vfx_BloodHit 1 | startColor: Color RGBA(0.669, 0.167, 0.167, 1) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_bat_hit |  | vfx_BloodHit_decals | startColor: Color RGBA(0.941, 0.083, 0.083, 1) \| startSize: TwoConstants 0.1 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_bat_hit |  | vfx_BloodHit_decals/blob_splat | startColor: Color RGBA(0.824, 0.177, 0.103, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_bjorn_hit |  | fx_bjorn_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_bjorn_hit |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_bjorn_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_bjorn_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_bjorn_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_bjorn_hit |  | skinflakes | startColor: Color RGBA(0.309, 0.168, 0, 1) \| startSize: TwoConstants 0.25 .. 0.8 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material lox_hair_particle; _MainTex hair_big (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_blobtar_tarball_hit |  | fx_blobtar_tarball_hit | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material pixel_lit_opaque; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_blobtar_tarball_hit |  | fire | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_blobtar_tarball_hit |  | Chunks | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_blobtar_tarball_hit |  | Chunks/blob_splat (1) | startColor: Color RGBA(0.057, 0.057, 0.057, 1) \| startSize: Constant 2 \| startLifetime: Constant 7 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material puke; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_blobtar_tarball_hit |  | wetsplsh | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| fx_blobtar_tarball_hit |  | blobs | startColor: TwoColors RGBA(0.934, 0.764, 0.46, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.1 \| startLifetime: TwoConstants 3 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/Blob; material blob_tar; _MainTex none; _Color RGBA(0, 0, 0, 1) |
| fx_bloodweapon_hit | yes | drops | startColor: Color RGBA(0.472, 0.051, 0.079, 1) \| startSize: TwoConstants 0.06 .. 0.1 \| startLifetime: TwoConstants 0.4 .. 2 \| maxParticles 35 \| simulationSpace World \| renderer: mode Stretch; shader Custom/LitParticles; material water_drop_noemissive; _MainTex drop_pixel (32x8); _Color RGBA(0.745, 0.745, 0.745, 1) |
| fx_bloodweapon_hit | yes | drops/poisonarrow_hit | startColor: TwoColors RGBA(0.66, 0.034, 0.144, 0.255) .. RGBA(0.509, 0.099, 0.156, 0.239) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| fx_bloodweapon_hit | yes | drops/vfx_ice_hit | startColor: TwoColors RGBA(0.415, 0.065, 0.078, 0.497) .. RGBA(0.573, 0.044, 0.008, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_bloodweapon_hit | yes | drops/smoke | startColor: Color RGBA(0.415, 0.053, 0.066, 0.439) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_bloodweapon_hit | yes | drops/Splash | startColor: Color RGBA(0.377, 0.041, 0.075, 1) \| startSize: Constant 6 \| startLifetime: Constant 0.4 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material BloodDripSplash; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_chainlightning_hit |  | fx_chainlightning_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_chainlightning_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| fx_chainlightning_hit |  | poff_ring | startColor: TwoColors RGBA(0.206, 0.159, 0.074, 1) .. RGBA(0.434, 0.382, 0.316, 1) \| startSize: TwoConstants 0.5 .. 1.2 \| startLifetime: TwoConstants 0.6 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_chainlightning_hit |  | shockwave_plane | startColor: Color RGBA(1, 1, 1, 0.518) \| startSize: Constant 35 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_chainlightning_hit |  | shockwave | startColor: Color RGBA(1, 1, 1, 0.098) \| startSize: Constant 22 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Unlit2; material aoe_shockwave; _MainTex soft_cloud (256x256); _Color RGBA(0, 0, 0, 1) |
| fx_chainlightning_hit |  | lightning | startColor: TwoColors RGBA(0.478, 0.87, 1, 1) .. RGBA(0, 0.793, 1, 1) \| startSize: Constant 0.03 \| startLifetime: TwoConstants 0.5 .. 1.2 \| maxParticles 100 \| simulationSpace World \| renderer: mode None; shader Unlit/Lighting; material lighting; _MainTex trail (128x128); _Color RGBA(5.992, 5.992, 5.992, 1) |
| fx_charred_hit |  | fx_charred_hit | startColor: Color RGBA(0.302, 0.229, 0.229, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| fx_charred_hit |  | Bits | startColor: Color RGBA(0.302, 0.229, 0.229, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.5 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material pixel_lit_opaque; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_charred_hit |  | Glow | startColor: Color RGBA(0.925, 0.22, 0.169, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material fireworks; _MainTex Firework (256x256); _Color RGBA(2.996, 2.996, 2.996, 1) |
| fx_clusterbombstaff_hit |  | fx_clusterbombstaff_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.025 .. 0.05 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_unlit_red; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_clusterbombstaff_hit |  | smoke | startColor: Color RGBA(0.057, 0.049, 0.049, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material wispysmoke; _MainTex WispySmoke_Flipbook (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_clusterbombstaff_hit |  | fire | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.4 .. 0.7 \| startLifetime: TwoConstants 0.1 .. 0.4 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_clusterbombstaff_hit |  | Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 0.561) \| startSize: Constant 4 \| startLifetime: Constant 0.2 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_clusterbombstaff_splinter_hit |  | fx_clusterbombstaff_splinter_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.025 .. 0.05 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_unlit_red; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_clusterbombstaff_splinter_hit |  | smoke | startColor: Color RGBA(0.057, 0.049, 0.049, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material wispysmoke; _MainTex WispySmoke_Flipbook (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_clusterbombstaff_splinter_hit |  | fire | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.2 \| startLifetime: TwoConstants 0.1 .. 0.4 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_clusterbombstaff_splinter_hit |  | Distortion Shockwave | startColor: Color RGBA(1, 0, 0, 0.561) \| startSize: Constant 2 \| startLifetime: Constant 0.2 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Legacy Shaders/Particles/Alpha Blended; material glow_pulse; _MainTex shockwave (128x128); _TintColor RGBA(1, 1, 1, 0.5) |
| fx_deathsquito_hit |  | fx_deathsquito_hit | startColor: TwoColors RGBA(0.772, 0.362, 0.097, 0.728) .. RGBA(0.76, 0.801, 0.041, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_deathsquito_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.4, 0.728, 0.086, 1) .. RGBA(0.669, 0.167, 0.167, 1) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_deathsquito_hit |  | vfx_BloodHit_decals | startColor: TwoColors RGBA(0.234, 0.485, 0.114, 1) .. RGBA(0.941, 0.083, 0.083, 1) \| startSize: TwoConstants 0.1 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_deathsquito_hit |  | vfx_BloodHit_decals/blob_splat | startColor: TwoColors RGBA(0.838, 0.017, 0, 1) .. RGBA(0.824, 0.177, 0.103, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_fader_meteor_hit |  | fx_fader_meteor_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material blue_cinder; _MainTex spark 1 (64x64); _Color RGBA(4, 4, 4, 1) |
| fx_fader_meteor_hit |  | smoke | startColor: Color RGBA(0.138, 0.16, 0.152, 1) \| startSize: TwoConstants 4 .. 7 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_fader_meteor_hit |  | Boom Spikes | startColor: Color RGBA(1, 1, 1, 0.49) \| startSize: TwoConstants 2 .. 5 \| startLifetime: TwoConstants 0.5 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_fader_meteor_hit |  | Sparks | startColor: TwoColors RGBA(0, 1, 0.485, 1) .. RGBA(0.024, 1, 0.325, 0.482) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_fader_meteor_hit |  | Rocks | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(0.151, 0.148, 0.146, 1) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 3 .. 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Standard (Specular setup); material coal; _MainTex iron (64x64); _Color RGBA(0, 0, 0, 1) |
| fx_fader_meteor_hit |  | Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 20 \| startLifetime: Constant 0.3 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_fimbulvinter_meteor_hit |  | fx_fimbulvinter_meteor_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material blue_cinder; _MainTex spark 1 (64x64); _Color RGBA(4, 4, 4, 1) |
| fx_fimbulvinter_meteor_hit |  | smoke | startColor: Color RGBA(0.203, 0.568, 0.811, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_fimbulvinter_meteor_hit |  | fire | startColor: TwoColors RGBA(0.161, 0.502, 1, 1) .. RGBA(0.094, 0.729, 1, 1) \| startSize: TwoConstants 2 .. 4 \| startLifetime: TwoConstants 0.3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material flame_large; _MainTex wildfire06 (128x128); _Color RGBA(1.059, 0.488, 0.216, 1) |
| fx_fimbulvinter_meteor_hit |  | zinder | startColor: TwoColors RGBA(0.267, 0.38, 0.741, 1) .. RGBA(0.094, 0.661, 1, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Particle (Unlit); material ashrain_cinder; _MainTex spark 1 (64x64) |
| fx_fimbulvinter_meteor_hit |  | wetsplsh | startColor: TwoColors RGBA(0.185, 0.233, 0.623, 1) .. RGBA(0.165, 0.646, 0.831, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1.5 .. 3 \| maxParticles 2000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_float_hitwater |  | fx_float_hitwater | startColor: Color RGBA(1, 1, 1, 0.459) \| startSize: TwoConstants 2 .. 3 \| startLifetime: Constant 3 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| fx_float_hitwater |  | GameObject | startColor: Color RGBA(1, 1, 1, 0.922) \| startSize: Constant 0.03 \| startLifetime: Constant 3 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_float_hitwater |  | vfx_water_surface | startColor: Color RGBA(0.566, 0.566, 0.566, 0.215) \| startSize: Constant 4 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode HorizontalBillboard; shader Custom/ShadowBlob; material water_surface; _MainTex water_foam_pix (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_float_hitwater |  | ring | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 8 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode HorizontalBillboard; shader Custom/ShadowBlob; material water_ring_thin; _MainTex water_ring_thin (128x128); _Color RGBA(1, 1, 1, 0.197) |
| fx_frozenking_spikerain_hit |  | fx_frozenking_spikerain_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material blue_cinder; _MainTex spark 1 (64x64); _Color RGBA(4, 4, 4, 1) |
| fx_frozenking_spikerain_hit |  | smoke | startColor: Color RGBA(0.138, 0.16, 0.152, 1) \| startSize: TwoConstants 4 .. 7 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_frozenking_spikerain_hit |  | Boom Spikes | startColor: Color RGBA(1, 1, 1, 0.49) \| startSize: TwoConstants 2 .. 5 \| startLifetime: TwoConstants 0.5 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| fx_frozenking_spikerain_hit |  | Sparks | startColor: TwoColors RGBA(0, 0.541, 1, 1) .. RGBA(0.024, 1, 0.961, 0.482) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_frozenking_spikerain_hit |  | Rocks | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(0.151, 0.148, 0.146, 1) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 3 .. 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Standard (Specular setup); material coal; _MainTex iron (64x64); _Color RGBA(0, 0, 0, 1) |
| fx_frozenking_spikerain_hit |  | Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 20 \| startLifetime: Constant 0.3 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_goblinking_beam_hit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_goblinking_beam_hit |  | fire | startColor: TwoColors RGBA(1, 0.162, 0.942, 1) .. RGBA(1, 0.096, 0.339, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: TwoConstants 0.3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material flame_large; _MainTex wildfire06 (128x128); _Color RGBA(1.059, 0.488, 0.216, 1) |
| fx_goblinking_beam_hit |  | zinder | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Particle (Unlit); material ashrain_cinder; _MainTex spark 1 (64x64) |
| fx_goblinking_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.581, 0, 0.036, 0.816) .. RGBA(0.779, 0.742, 0.533, 0.884) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_goblinking_hit |  | cunks | startColor: TwoColors RGBA(0.699, 0.454, 0.087, 0.728) .. RGBA(0.853, 0.031, 0.388, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_goblinking_hit |  | cunks/blob_splat | startColor: TwoColors RGBA(0.971, 0.064, 0.496, 1) .. RGBA(0.831, 0.049, 0.68, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_goblinking_meteor_hit |  | fx_goblinking_meteor_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material blue_cinder; _MainTex spark 1 (64x64); _Color RGBA(4, 4, 4, 1) |
| fx_goblinking_meteor_hit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_goblinking_meteor_hit |  | fire | startColor: TwoColors RGBA(1, 0.162, 0.942, 1) .. RGBA(1, 0.676, 0.096, 1) \| startSize: TwoConstants 2 .. 4 \| startLifetime: TwoConstants 0.3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material flame_large; _MainTex wildfire06 (128x128); _Color RGBA(1.059, 0.488, 0.216, 1) |
| fx_goblinking_meteor_hit |  | zinder | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(1, 0.096, 0.938, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Particle (Unlit); material ashrain_cinder; _MainTex spark 1 (64x64) |
| fx_goblinking_meteor_hit |  | wetsplsh | startColor: TwoColors RGBA(0.864, 0.077, 0.875, 1) .. RGBA(0.831, 0.165, 0.317, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1.5 .. 3 \| maxParticles 2000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_greenroots_projectile_hit |  | fx_greenroots_projectile_hit | startColor: Color RGBA(0.339, 0.575, 0.417, 1) \| startSize: TwoConstants 4 .. 6 \| startLifetime: TwoConstants 3 .. 7 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| fx_greenroots_projectile_hit |  | Spores | startColor: TwoColors RGBA(1, 1, 1, 1) .. RGBA(1, 1, 1, 0.482) \| startSize: TwoConstants 0.025 .. 0.1 \| startLifetime: TwoConstants 1 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material light_glow_gradient; _MainTex Default-Particle (64x64) |
| fx_greenroots_projectile_hit |  | Flame Spikes | startColor: Color RGBA(1, 1, 1, 0.49) \| startSize: TwoConstants 1 .. 4 \| startLifetime: TwoConstants 0.3 .. 0.4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Custom/Gradient Mapped Particle (Unlit); material fireball_large; _MainTex flameball_big_flipbook (512x512) |
| fx_greenroots_projectile_hit |  | Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 10.3 \| startLifetime: Constant 0.3 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_himminafl_hit |  | fx_himminafl_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_himminafl_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| fx_himminafl_hit |  | poff_ring | startColor: TwoColors RGBA(0.206, 0.159, 0.074, 1) .. RGBA(0.434, 0.382, 0.316, 1) \| startSize: TwoConstants 0.5 .. 1.2 \| startLifetime: TwoConstants 0.6 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_himminafl_hit |  | shockwave_plane | startColor: Color RGBA(1, 1, 1, 0.518) \| startSize: Constant 35 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_himminafl_hit |  | shockwave | startColor: Color RGBA(1, 1, 1, 0.098) \| startSize: Constant 22 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Unlit2; material aoe_shockwave; _MainTex soft_cloud (256x256); _Color RGBA(0, 0, 0, 1) |
| fx_himminafl_hit |  | lightning | startColor: TwoColors RGBA(0.478, 0.87, 1, 1) .. RGBA(0, 0.793, 1, 1) \| startSize: Constant 0.03 \| startLifetime: TwoConstants 0.5 .. 1.2 \| maxParticles 100 \| simulationSpace World \| renderer: mode None; shader Unlit/Lighting; material lighting; _MainTex trail (128x128); _Color RGBA(5.992, 5.992, 5.992, 1) |
| fx_iceshard_hit |  | fx_iceshard_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| fx_iceshard_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| fx_iceshard_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_iceshard_hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_jotunbane_hit |  | drops | startColor: Color RGBA(0.877, 0.704, 0.211, 1) \| startSize: TwoConstants 0.06 .. 0.1 \| startLifetime: TwoConstants 0.4 .. 2 \| maxParticles 35 \| simulationSpace World \| renderer: mode Stretch; shader Custom/LitParticles; material water_drop; _MainTex drop_pixel (32x8); _Color RGBA(1, 1, 1, 1) |
| fx_jotunbane_hit |  | drops/poisonarrow_hit | startColor: TwoColors RGBA(0.531, 0.618, 0.132, 1) .. RGBA(0.963, 0.279, 0.163, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| fx_jotunbane_hit |  | drops/vfx_ice_hit | startColor: TwoColors RGBA(0.896, 1, 0.125, 0.497) .. RGBA(0.486, 0.573, 0.008, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_jotunbane_hit |  | drops/smoke | startColor: Color RGBA(0.655, 0.906, 0.098, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_jotunbane_hit |  | drops/Splash | startColor: Color RGBA(0.655, 0.906, 0.098, 1) \| startSize: Constant 6 \| startLifetime: Constant 0.4 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material JotunBaneDripSplash; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 0.472) |
| fx_lightningstaffprojectile_hit |  | fx_lightningstaffprojectile_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_lightningstaffprojectile_hit |  | poff_ring | startColor: TwoColors RGBA(0.206, 0.159, 0.074, 1) .. RGBA(0.434, 0.382, 0.316, 1) \| startSize: TwoConstants 0.5 .. 1.2 \| startLifetime: TwoConstants 0.6 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_lightningstaffprojectile_hit |  | lightning | startColor: TwoColors RGBA(1, 0.478, 0.561, 1) .. RGBA(1, 0, 0.01, 1) \| startSize: Constant 0.03 \| startLifetime: TwoConstants 0.1 .. 0.3 \| maxParticles 100 \| simulationSpace World \| renderer: mode None; shader Unlit/Lighting; material lighting; _MainTex trail (128x128); _Color RGBA(5.992, 5.992, 5.992, 1) |
| fx_lightningweapon_hit |  | fx_lightningweapon_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_lightningweapon_hit |  | poff_ring | startColor: TwoColors RGBA(0.557, 0.557, 0.557, 0.271) .. RGBA(0.802, 0.793, 0.783, 0.271) \| startSize: TwoConstants 0.5 .. 1.2 \| startLifetime: TwoConstants 0.6 .. 1.5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material wispysmoke; _MainTex WispySmoke_Flipbook (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_lightningweapon_hit |  | Sparks | startColor: Color RGBA(0.382, 0.908, 1, 1) \| startSize: TwoConstants 0.025 .. 0.05 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_lightningweapon_hit |  | Lightning | startColor: Color RGBA(0.736, 0.932, 0.981, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 0.25 .. 0.5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Mesh Flipbook Particle; material flipbookmesh_mat; _MainTex none |
| fx_lox_hit |  | fx_lox_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_lox_hit |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_lox_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_lox_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_lox_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_lox_hit |  | skinflakes | startColor: Color RGBA(0.309, 0.168, 0, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material lox_hair_particle; _MainTex hair_big (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_moose_hit |  | fx_moose_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_moose_hit |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_moose_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_moose_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_moose_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_moose_hit |  | skinflakes | startColor: Color RGBA(1, 0.886, 0.75, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material lox_hair_particle; _MainTex hair_big (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_morgen_hit |  | fx_morgen_hit | startColor: Color RGBA(0.755, 0.025, 0.025, 1) \| startSize: TwoConstants 0.1 .. 0.4 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_morgen_hit |  | Blood | startColor: Color RGBA(0.755, 0.025, 0.025, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_natureweapon_hit |  | fx_natureweapon_hit | startColor: TwoColors RGBA(0, 1, 0.077, 0.497) .. RGBA(0.203, 1, 0.437, 1) \| startSize: TwoConstants 0.04 .. 0.03 \| startLifetime: TwoConstants 0.5 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material radiation_particle; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_radiation_hit |  | fx_radiation_hit | startColor: TwoColors RGBA(0, 1, 0.841, 0.497) .. RGBA(0.203, 1, 0.437, 1) \| startSize: TwoConstants 0.04 .. 0.03 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material radiation_particle; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | fx_seeker_death | startColor: Color RGBA(0.691, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material blood_splat2; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | skinflakes | startColor: TwoColors RGBA(0.292, 0.212, 0.134, 0.566) .. RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: TwoConstants 1 .. 1.2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | drops-withdecals | startColor: TwoColors RGBA(0, 0.557, 0.395, 0.491) .. RGBA(0, 0.604, 0.127, 1) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | drops-withdecals/blood_decal | startColor: TwoColors RGBA(0, 0.755, 0.339, 1) .. RGBA(0, 0.462, 0.075, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 10 .. 11 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/Decal; material seeker_blood_splat; _MainTex brains (64x64); _Color RGBA(0.557, 0.557, 0.557, 1) |
| fx_seeker_death | yes | wetsplsh | startColor: TwoColors RGBA(0.267, 0.741, 0.387, 1) .. RGBA(0, 0.816, 0.639, 1) \| startSize: TwoConstants 0.6 .. 1.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_seeker_death | yes | Legs | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.3 .. 0.8 \| startLifetime: TwoConstants 5 .. 8 \| maxParticles 10 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material seeker; _MainTex seeker_d (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | Wings | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.8 .. 0.8 \| startLifetime: TwoConstants 5 .. 8 \| maxParticles 10 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material seeker; _MainTex seeker_d (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_death | yes | Carapace | startColor: TwoColors RGBA(0.415, 0.343, 0.335, 0.566) .. RGBA(0.16, 0.16, 0.16, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: TwoConstants 3 .. 4 \| maxParticles 100 \| simulationSpace World \| renderer: mode Mesh; shader Custom/Creature; material seeker; _MainTex seeker_d (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_hurt | yes | fx_seeker_hurt | startColor: Color RGBA(0, 0.859, 0.218, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_hurt | yes | wetsplsh | startColor: TwoColors RGBA(0.1, 0.783, 0.178, 1) .. RGBA(0, 0.808, 0.544, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_seeker_hurt | yes | blood_drops | startColor: TwoColors RGBA(0.015, 0.329, 0, 0.491) .. RGBA(0, 0.545, 0.344, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_seeker_hurt | yes | drops-withdecals | startColor: TwoColors RGBA(0, 0.557, 0.395, 0.491) .. RGBA(0, 0.604, 0.127, 0.534) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_seeker_hurt | yes | drops-withdecals/blood_decal | startColor: TwoColors RGBA(0, 0.443, 0.202, 1) .. RGBA(0, 0.509, 0.089, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/Decal; material seeker_blood_splat; _MainTex brains (64x64); _Color RGBA(0.557, 0.557, 0.557, 1) |
| fx_seeker_hurt | yes | skinflakes | startColor: TwoColors RGBA(0.537, 0.339, 0.275, 1) .. RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_seeker_melee_hit |  | - | no ParticleSystem |
| fx_shieldgenerator_domehit |  | fx_shieldgenerator_domehit | startColor: Color RGBA(0, 0.83, 0.296, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| fx_shieldgenerator_domehit |  | shockwave | startColor: Color RGBA(0, 0.831, 0.294, 0.42) \| startSize: Constant 6 \| startLifetime: Constant 0.3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_shieldgenerator_domehit |  | Distortion Shockwave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 5.54 \| startLifetime: Constant 0.3 \| maxParticles 1 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material shockwave_distortion; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | waves | startColor: Color RGBA(1, 1, 1, 0.666) \| startSize: Constant 30 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: HorizontalBillboard, no sharedMaterial |
| fx_sledge_demolisher_hit |  | bubble wave | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 10 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Unlit2; material demolisher_shockwave; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | Cloud | startColor: TwoColors RGBA(0.604, 0.385, 0.174, 1) .. RGBA(1, 0.635, 0.061, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | vfx_troll_rock_destroyed | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.351, 0.403, 0.434, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | vfx_Splosh (1) | startColor: TwoColors RGBA(0.783, 0.456, 0.129, 0.978) .. RGBA(0.717, 0.623, 0.254, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | vfx_troll_rock_destroyed (1) | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.351, 0.403, 0.434, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | waves (1) | startColor: Color RGBA(1, 1, 1, 0.666) \| startSize: Constant 20 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | waves (2) | startColor: Color RGBA(1, 1, 1, 0.666) \| startSize: Constant 20 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material demolisher_shockwave; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_sledge_demolisher_hit |  | vfx_Splosh (2) | startColor: TwoColors RGBA(0.783, 0.456, 0.129, 0.341) .. RGBA(0.717, 0.623, 0.254, 0.349) \| startSize: TwoConstants 1 .. 3 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| fx_tick_death | yes | fx_tick_death | startColor: Color RGBA(0.691, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material blood_splat2; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_tick_death | yes | skinflakes | startColor: TwoColors RGBA(0.604, 0.436, 0.419, 0.566) .. RGBA(1, 0.332, 0.297, 1) \| startSize: TwoConstants 0.3 .. 0.8 \| startLifetime: TwoConstants 1 .. 1.2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material tick_flake; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_tick_death | yes | drops-withdecals | startColor: Color RGBA(0.472, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_tick_death | yes | drops-withdecals/blood_decal | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_tick_death | yes | wetsplsh | startColor: Color RGBA(0.816, 0, 0, 1) \| startSize: TwoConstants 0.6 .. 1.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_tick_death | yes | Blood Cloud | startColor: Color RGBA(0.575, 0, 0, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 3 .. 6 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| fx_unbjorn_hit |  | fx_unbjorn_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_unbjorn_hit |  | wetsplsh | startColor: Color RGBA(0.33, 0.048, 0.048, 1) \| startSize: TwoConstants 0.2 .. 1 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| fx_unbjorn_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.189, 0.049, 0.049, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| fx_unbjorn_hit |  | bloodchunks | startColor: Color RGBA(0.255, 0.054, 0.054, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_unbjorn_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| fx_unbjorn_hit |  | skinflakes | startColor: Color RGBA(0.264, 0.208, 0.141, 1) \| startSize: TwoConstants 0.25 .. 0.8 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material tick_flake; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| fx_vines_hit |  | fx_vines_hit | startColor: Color RGBA(0.356, 0.764, 0.123, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Surface2; material leaf; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| sfx_Bonemass_Hit |  | - | no ParticleSystem |
| sfx_GoblinShaman_hurt |  | - | no ParticleSystem |
| sfx_GuckSackHit |  | - | no ParticleSystem |
| sfx_MudHit |  | - | no ParticleSystem |
| sfx_ProjectileHit |  | - | no ParticleSystem |
| sfx_archery_target_hit |  | - | no ParticleSystem |
| sfx_arrow_hit |  | - | no ParticleSystem |
| sfx_axe_flint_hit |  | - | no ParticleSystem |
| sfx_axe_hit |  | - | no ParticleSystem |
| sfx_barley_hit |  | - | no ParticleSystem |
| sfx_battleaxe_hit |  | - | no ParticleSystem |
| sfx_bear_hurt |  | - | no ParticleSystem |
| sfx_beehive_hit |  | - | no ParticleSystem |
| sfx_blob_hit |  | - | no ParticleSystem |
| sfx_boar_hit |  | - | no ParticleSystem |
| sfx_bonemaw_serpent_spit_hit |  | - | no ParticleSystem |
| sfx_branch_hit |  | - | no ParticleSystem |
| sfx_bush_hit |  | - | no ParticleSystem |
| sfx_cart_hit |  | - | no ParticleSystem |
| sfx_charred_hurt |  | - | no ParticleSystem |
| sfx_charred_twitcher_hurt |  | - | no ParticleSystem |
| sfx_chick_hurt |  | - | no ParticleSystem |
| sfx_chicken_hurt |  | - | no ParticleSystem |
| sfx_cinder_rain_hit |  | - | no ParticleSystem |
| sfx_club_hit |  | - | no ParticleSystem |
| sfx_dragon_hurt |  | - | no ParticleSystem |
| sfx_dragon_melee_hit |  | - | no ParticleSystem |
| sfx_draugr_hit |  | - | no ParticleSystem |
| sfx_draugrpile_hit |  | - | no ParticleSystem |
| sfx_dverger_vo_hurt |  | - | no ParticleSystem |
| sfx_eikthyr_hit |  | - | no ParticleSystem |
| sfx_elaking_hit |  | - | no ParticleSystem |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow | startColor: Color RGBA(1, 1, 1, 0.634) \| startSize: TwoConstants 1.5 .. 2.2 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/Snow flakes | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.06 \| startLifetime: TwoConstants 6 .. 8 \| maxParticles 40 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/flying lumps | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.03 \| startLifetime: TwoConstants 6 .. 8 \| maxParticles 25 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material snowlump; _MainTex none; _Color RGBA(0.871, 0.871, 0.871, 1) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/Snow lumps (1) | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 10 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material snowlump; _MainTex none; _Color RGBA(0.871, 0.871, 0.871, 1) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/blob_splat (2) | startColor: Color RGBA(0.132, 0.132, 0.132, 1) \| startSize: TwoConstants 1.5 .. 2 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode HorizontalBillboard; shader Custom/ParticleDecal; material splat_decal_snowstep; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/bloodchunks | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 1 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| sfx_elaking_hit_old |  | fx_wnt_damage_snow/bloodchunks/blob_splat (1) | startColor: Color RGBA(0, 0, 0, 0.628) \| startSize: TwoConstants 0.8 .. 1.4 \| startLifetime: Constant 6 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_snowstep; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| sfx_fallenwarrior_hurt |  | - | no ParticleSystem |
| sfx_fenring_claw_hit |  | - | no ParticleSystem |
| sfx_gameltroll_hurt |  | - | no ParticleSystem |
| sfx_ghost_attack_hit |  | - | no ParticleSystem |
| sfx_ghost_hurt |  | - | no ParticleSystem |
| sfx_ghosts_hurt |  | - | no ParticleSystem |
| sfx_goblin_hit |  | - | no ParticleSystem |
| sfx_goblinbrute_clubhit |  | - | no ParticleSystem |
| sfx_goblinbrute_hit |  | - | no ParticleSystem |
| sfx_grapplinghook_hit |  | - | no ParticleSystem |
| sfx_greydwarf_attack_hit |  | - | no ParticleSystem |
| sfx_greydwarf_hit |  | - | no ParticleSystem |
| sfx_greydwarf_stone_hit |  | - | no ParticleSystem |
| sfx_greydwarfnest_hit |  | - | no ParticleSystem |
| sfx_greyling_hit |  | - | no ParticleSystem |
| sfx_hatcling_hurt |  | - | no ParticleSystem |
| sfx_hit |  | - | no ParticleSystem |
| sfx_hit_vibration_only |  | - | no ParticleSystem |
| sfx_ice_hit |  | - | no ParticleSystem |
| sfx_imp_hit |  | - | no ParticleSystem |
| sfx_jotunwarrior_hurt |  | - | no ParticleSystem |
| sfx_jotunwitch_hurt |  | - | no ParticleSystem |
| sfx_leech_attack_hit |  | - | no ParticleSystem |
| sfx_leech_hit |  | - | no ParticleSystem |
| sfx_metalbars_hit |  | - | no ParticleSystem |
| sfx_moleman_hurt |  | - | no ParticleSystem |
| sfx_moose_attack_melee_hit |  | - | no ParticleSystem |
| sfx_moose_hurt |  | - | no ParticleSystem |
| sfx_neck_attack_hit |  | - | no ParticleSystem |
| sfx_neck_hit |  | - | no ParticleSystem |
| sfx_pickaxe_hit |  | - | no ParticleSystem |
| sfx_pickaxe_hit_vibration_only |  | - | no ParticleSystem |
| sfx_rock_hit |  | - | no ParticleSystem |
| sfx_serpent_attack_hit |  | - | no ParticleSystem |
| sfx_serpent_hurt |  | - | no ParticleSystem |
| sfx_shieldgenerator_hit |  | - | no ParticleSystem |
| sfx_silvermace_hit |  | - | no ParticleSystem |
| sfx_skeleton_hit |  | - | no ParticleSystem |
| sfx_skeleton_mace_hit |  | - | no ParticleSystem |
| sfx_sledge_hit |  | - | no ParticleSystem |
| sfx_sledge_iron_hit |  | - | no ParticleSystem |
| sfx_snow_hit_debris |  | - | no ParticleSystem |
| sfx_snow_hit_transient |  | - | no ParticleSystem |
| sfx_spear_flint_hit |  | - | no ParticleSystem |
| sfx_spear_hit |  | - | no ParticleSystem |
| sfx_staff_elder_root_hit |  | - | no ParticleSystem |
| sfx_staffthunderblood_cast |  | - | no ParticleSystem |
| sfx_staffthunderblood_thunder |  | - | no ParticleSystem |
| sfx_stonegolem_attack_hit |  | - | no ParticleSystem |
| sfx_stonegolem_hurt |  | - | no ParticleSystem |
| sfx_sword_hit |  | - | no ParticleSystem |
| sfx_tick_hurt |  | - | no ParticleSystem |
| sfx_tree_fall_hit |  | - | no ParticleSystem |
| sfx_tree_hit |  | - | no ParticleSystem |
| sfx_tree_hit_abomination |  | - | no ParticleSystem |
| sfx_troll_attack_hit |  | - | no ParticleSystem |
| sfx_troll_hit |  | - | no ParticleSystem |
| sfx_unarmed_hit |  | - | no ParticleSystem |
| sfx_unbjorn_hurt |  | - | no ParticleSystem |
| sfx_wolf_attack_hit |  | - | no ParticleSystem |
| sfx_wolf_hit |  | - | no ParticleSystem |
| sfx_wood_hit |  | - | no ParticleSystem |
| sfx_wraith_attack_hit |  | - | no ParticleSystem |
| sfx_wraith_hit |  | - | no ParticleSystem |
| vfx_ArcheryTarget_hit |  | Fibers | startColor: Color RGBA(0.659, 0.569, 0.455, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material feather_white; _MainTex feather_particle_white (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_ArcheryTarget_hit |  | Fibers/Dust | startColor: Color RGBA(0.761, 0.724, 0.677, 1) \| startSize: TwoConstants 1 .. 1.5 \| startLifetime: TwoConstants 1 .. 5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke_hard; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| vfx_BloodDeath | yes | vfx_BloodDeath | startColor: Color RGBA(0.691, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material blood_splat2; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_BloodDeath | yes | skinflakes | startColor: Color RGBA(0.566, 0.393, 0.2, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_BloodDeath | yes | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_BloodDeath | yes | vfx_BloodHit 1/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_BloodDeath | yes | wetsplsh | startColor: Color RGBA(0.971, 0, 0, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_BloodHit | yes | vfx_BloodHit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_BloodHit | yes | wetsplsh | startColor: Color RGBA(0.971, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_BloodHit | yes | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_BloodHit | yes | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_BloodHit | yes | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_BonemassHit |  | vfx_BonemassHit | startColor: TwoColors RGBA(0.338, 0.258, 0.114, 1) .. RGBA(0.397, 0.298, 0.12, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_BonemassHit |  | GameObject | startColor: TwoColors RGBA(0.412, 0.302, 0.106, 1) .. RGBA(0.426, 0.311, 0.103, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_BonemassHit |  | bones | startColor: Color RGBA(0.794, 0.547, 0.105, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_BonemassHit |  | wetsplsh | startColor: TwoColors RGBA(0.294, 0.234, 0.125, 1) .. RGBA(0.551, 0.434, 0.223, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_BonemassHit |  | Chunks | startColor: TwoColors RGBA(0.618, 0.416, 0.054, 1) .. RGBA(0.229, 0.324, 0.102, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_BonemassHit |  | Chunks/blob_splat | startColor: TwoColors RGBA(0.61, 0.316, 0, 1) .. RGBA(0.316, 0.463, 0.215, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_CoalHit |  | vfx_CoalHit | startColor: TwoColors RGBA(0.162, 0.162, 0.162, 1) .. RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_CoalHit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard (Specular setup); material coal; _MainTex iron (64x64); _Color RGBA(0, 0, 0, 1) |
| vfx_ColdBall_Hit |  | vfx_ColdBall_Hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material cold_sparcs; _MainTex none; _Color RGBA(0.961, 1.877, 2.42, 1) |
| vfx_ColdBall_Hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| vfx_ColdBall_Hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_ColdBall_Hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_FallenWarrior_hit |  | Ethereal Smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.5 .. 1.5 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Gradient Mapped Particle (Unlit); material slowwispysmoke_gradient; _MainTex slowwispysmokeloop (512x512) |
| vfx_FallenWarrior_hit |  | Ethereal Smoke/Bits | startColor: Color RGBA(0.269, 0.593, 1, 1) \| startSize: TwoConstants 0.05 .. 0.01 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| vfx_FireballHit |  | vfx_FireballHit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.4 \| startLifetime: TwoConstants 0.2 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Legacy Shaders/Particles/Additive; material flame; _MainTex leaf_low (8x8); _TintColor RGBA(1, 1, 1, 1) |
| vfx_FireballHit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_FireballHit |  | fire | startColor: TwoColors RGBA(1, 0.984, 0.934, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 0.3 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material flameball_flipbook; _MainTex flameball_flipbook (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_FireballHit |  | fire (1) | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Legacy Shaders/Particles/Additive; material flame; _MainTex leaf_low (8x8); _TintColor RGBA(1, 1, 1, 1) |
| vfx_GuckSackHit | yes | vfx_GuckSackHit | startColor: TwoColors RGBA(0.859, 1, 0.503, 1) .. RGBA(0.663, 1, 0.337, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_GuckSackHit | yes | GameObject | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_GuckSackHit | yes | wetsplsh | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_GuckSackHit | yes | vfx_RockHit (1) | startColor: TwoColors RGBA(0.493, 0.404, 0.246, 1) .. RGBA(0.676, 0.512, 0.219, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_HitSparks |  | vfx_HitSparks | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.03 .. 0.03 \| startLifetime: TwoConstants 0.3 .. 0.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material gnista; _MainTex crystal (64x64); _Color RGBA(2, 2, 2, 1) |
| vfx_HitSparks |  | vfx_Splosh (1) | startColor: TwoColors RGBA(1, 0.783, 0.074, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.8 .. 1 \| startLifetime: Constant 0.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_MarbleHit |  | vfx_MarbleHit | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_MarbleHit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/Piece; material blackmarble_item; _MainTex marble_item_d (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_MudHit |  | vfx_MudHit | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_MudHit |  | GameObject | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_MudHit |  | wetsplsh | startColor: TwoColors RGBA(0.382, 0.292, 0.129, 1) .. RGBA(0.537, 0.632, 0.295, 1) \| startSize: TwoConstants 0.5 .. 1.5 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| vfx_MudHit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.493, 0.404, 0.246, 1) .. RGBA(0.676, 0.512, 0.219, 1) \| startSize: TwoConstants 0.01 .. 0.1 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material mudlump; _MainTex mudpile_d (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_Poison | yes | vfx_Poison | startColor: Color RGBA(1, 0.206, 0.206, 1) \| startSize: TwoConstants 2 .. 2 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_Poison | yes | smok_small | startColor: TwoColors RGBA(0.779, 1, 0, 0.684) .. RGBA(0.475, 0.846, 0.056, 0.753) \| startSize: Constant 2 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| vfx_Poison | yes | smoke_world | startColor: TwoColors RGBA(0.779, 1, 0, 0.684) .. RGBA(0.475, 0.846, 0.056, 0.753) \| startSize: Constant 1 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material poison; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| vfx_ProjectileHit |  | vfx_ProjectileHit | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_ProjectileHit |  | GameObject | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_ProjectileHit |  | wetsplsh | startColor: TwoColors RGBA(0.34, 0.426, 0.141, 1) .. RGBA(0.434, 0.373, 0.121, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1.5 .. 2 \| maxParticles 2000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_ProjectileHit |  | ooz | startColor: TwoColors RGBA(0.957, 1, 0.559, 0.291) .. RGBA(0.724, 1, 0.5, 0.309) \| startSize: Constant 4 \| startLifetime: TwoConstants 5 .. 6 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_RockHit |  | vfx_RockHit | startColor: TwoColors RGBA(0.162, 0.162, 0.162, 1) .. RGBA(0.559, 0.559, 0.559, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_RockHit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_RockHit_Marble |  | vfx_RockHit_Marble | startColor: TwoColors RGBA(0.162, 0.162, 0.162, 1) .. RGBA(0.559, 0.559, 0.559, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Piece; material blackmarble; _MainTex marble_d (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_RockHit_Marble |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_RockHit_Obsidian |  | vfx_RockHit_Obsidian | startColor: TwoColors RGBA(0.162, 0.162, 0.162, 1) .. RGBA(0.559, 0.559, 0.559, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_RockHit_Obsidian |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_RockHit_Obsidian |  | vfx_RockHit (2) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material ObsidianRock_mat; _MainTex ObsidanRock_d (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_ShadowPerson_hit |  | Ethereal Smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.5 .. 1.5 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Gradient Mapped Particle (Unlit); material slowwispysmoke_gradient; _MainTex slowwispysmokeloop (512x512) |
| vfx_ShadowPerson_hit |  | Ethereal Smoke/Bits | startColor: Color RGBA(0.404, 0.404, 0.404, 1) \| startSize: TwoConstants 0.05 .. 0.01 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_additive; _MainTex none; _Color RGBA(2, 2, 2, 1) |
| vfx_Slimed | yes | splush | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material queenslime; _MainTex slime_splash_2 (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_TrainingDummy_hit |  | Woodchips | startColor: Color RGBA(0.478, 0.334, 0.215, 1) \| startSize: TwoConstants 0.01 .. 0.04 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Surface2; material pixel_lit_opaque; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_TrainingDummy_hit |  | Woodchips/Smoke | startColor: Color RGBA(0.774, 0.67, 0.586, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| vfx_arrowhit |  | vfx_arrowhit | startColor: TwoColors RGBA(0.603, 0.509, 0.408, 1) .. RGBA(1, 0.937, 0.801, 1) \| startSize: TwoConstants 0.5 .. 0.8 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_arrowhit |  | vfx_BloodHit 1 | startColor: Color RGBA(1, 1, 1, 0.778) \| startSize: TwoConstants 0.04 .. 0.04 \| startLifetime: Constant 0.3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material pixel_unlit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_ashlandsbow_blood_fire |  | smoke | startColor: Color RGBA(0.443, 0.136, 0.136, 0.369) \| startSize: TwoConstants 0.3 .. 1.5 \| startLifetime: TwoConstants 2 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| vfx_ashlandsbow_blood_fire |  | smoke/spikes | startColor: Color RGBA(0.321, 0.026, 0.075, 1) \| startSize: TwoConstants 0.05 .. 0.2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 100 \| simulationSpace World \| renderer: mode Stretch; shader Custom/LitParticles; material water_drop_noemissive; _MainTex drop_pixel (32x8); _Color RGBA(0.745, 0.745, 0.745, 1) |
| vfx_barnacle_hit |  | flakes | startColor: TwoColors RGBA(0.559, 0.529, 0.407, 1) .. RGBA(0.669, 0.554, 0.305, 1) \| startSize: TwoConstants 0.01 .. 0.01 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/AlphaParticle; material barnacle_piece; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_barnacle_hit |  | pixels | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.757, 0.705, 0.568, 1) \| startSize: TwoConstants 0.04 .. 0.07 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_beehive_hit |  | vfx_beehive_hit | startColor: Color RGBA(0.301, 0.301, 0.268, 1) \| startSize: TwoConstants 0.3 .. 0.6 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material hive; _MainTex feather_particle_white (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_beehive_hit |  | smokepoff | startColor: Color RGBA(0.846, 0.846, 0.846, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: TwoConstants 5 .. 2 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_beehive_hit |  | vfx_beehive_hit | startColor: TwoColors RGBA(0.64, 0.61, 0.583, 1) .. RGBA(0.882, 0.878, 0.72, 1) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material pixel_lit_opaque; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_beehive_hit |  | honney | startColor: Color RGBA(1, 0.69, 0.022, 1) \| startSize: TwoConstants 0.05 .. 0.02 \| startLifetime: Constant 3 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material pixel_lit_opaque; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blob_death | yes | vfx_blob_death | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blob_death | yes | Chunks | startColor: TwoColors RGBA(0.618, 0.416, 0.054, 1) .. RGBA(0.4, 0.581, 0.158, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_blob_death | yes | Chunks/blob_splat | startColor: TwoColors RGBA(0.61, 0.316, 0, 1) .. RGBA(0.316, 0.463, 0.215, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_blob_death | yes | bones | startColor: TwoColors RGBA(0.934, 0.764, 0.46, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.3 .. 0.4 \| startLifetime: TwoConstants 3 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_blob_death | yes | wetsplsh | startColor: TwoColors RGBA(0.276, 0.324, 0.109, 1) .. RGBA(0.287, 0.239, 0.154, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_blob_frost_hit |  | vfx_blob_frost_hit | startColor: TwoColors RGBA(0.091, 0.348, 1, 1) .. RGBA(0.236, 1, 0.977, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blob_frost_hit |  | wetsplsh | startColor: TwoColors RGBA(0.206, 0.432, 0.648, 1) .. RGBA(0.233, 0.89, 0.937, 1) \| startSize: TwoConstants 0.7 .. 1 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Surface2; material queenslime; _MainTex slime_splash_2 (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_blob_hit | yes | vfx_blob_hit | startColor: TwoColors RGBA(0.743, 0.572, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blob_hit | yes | chunks | startColor: TwoColors RGBA(0.632, 0.446, 0.112, 1) .. RGBA(0.316, 0.574, 0.139, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_blob_hit | yes | chunks/blob_splat | startColor: TwoColors RGBA(0.493, 0.377, 0, 1) .. RGBA(0.239, 0.544, 0.092, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_blob_hit | yes | vfx_RockHit (1) | startColor: TwoColors RGBA(0.934, 0.764, 0.46, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_blob_hit | yes | wetsplsh | startColor: TwoColors RGBA(0.638, 0.743, 0.268, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_blobmork_hit |  | vfx_blobmork_hit | startColor: TwoColors RGBA(0.114, 0.725, 0.694, 1) .. RGBA(0.286, 0.482, 0.463, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blobmork_hit |  | chunks | startColor: TwoColors RGBA(0.286, 0.482, 0.463, 1) .. RGBA(0.114, 0.725, 0.694, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_blobmork_hit |  | chunks/blob_splat | startColor: TwoColors RGBA(0.286, 0.482, 0.463, 1) .. RGBA(0.114, 0.725, 0.694, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_blobmork_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(1, 0.914, 0.759, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_blobmork_hit |  | wetsplsh | startColor: TwoColors RGBA(0.286, 0.482, 0.463, 1) .. RGBA(0.114, 0.725, 0.694, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_teal; _MainTex slime_splash (64x64); _Color RGBA(0, 1, 0.741, 1) |
| vfx_blobtar_hit |  | vfx_blobtar_hit | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_blobtar_hit |  | chunks | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.1 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_blobtar_hit |  | chunks/blob_splat (1) | startColor: Color RGBA(0.057, 0.057, 0.057, 1) \| startSize: Constant 2 \| startLifetime: Constant 7 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/AlphaParticle; material puke; _MainTex water_foam (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_blobtar_hit |  | wetsplsh | startColor: Color RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| vfx_boar_hit | yes | vfx_boar_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_boar_hit | yes | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_boar_hit | yes | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_boar_hit | yes | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_boar_hit | yes | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_bush2_e_hit |  | ps | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.5 \| startLifetime: Constant 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material leaf_en; _MainTex en_particle (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_bush2_e_hit |  | smokepoff | startColor: Color RGBA(0.669, 0.613, 0.492, 0.228) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1 .. 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_clubhit |  | vfx_clubhit | startColor: TwoColors RGBA(0.603, 0.509, 0.408, 1) .. RGBA(1, 0.937, 0.801, 1) \| startSize: TwoConstants 0.3 .. 0.6 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_crossbow_blood_fire |  | smoke | startColor: Color RGBA(0.443, 0.136, 0.136, 0.369) \| startSize: TwoConstants 0.3 .. 1.5 \| startLifetime: TwoConstants 2 .. 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slowwispysmoke; _MainTex slowwispysmokeloop (512x512); _Color RGBA(1, 1, 1, 1) |
| vfx_crossbow_blood_fire |  | smoke/spikes | startColor: Color RGBA(0.321, 0.026, 0.075, 1) \| startSize: TwoConstants 0.05 .. 0.2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 100 \| simulationSpace World \| renderer: mode Stretch; shader Custom/LitParticles; material water_drop_noemissive; _MainTex drop_pixel (32x8); _Color RGBA(0.745, 0.745, 0.745, 1) |
| vfx_crossbow_blood_fire |  | smoke/shockwave | startColor: Color RGBA(1, 0.476, 0.476, 0.216) \| startSize: Constant 4 \| startLifetime: Constant 0.3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_deer_hit |  | vfx_deer_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_deer_hit |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_deer_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_deer_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_deer_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_dragon_hurt |  | vfx_dragon_hurt | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_dragon_hurt |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_dragon_hurt |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_dragon_hurt |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_dragon_hurt |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_dragon_hurt |  | skinflakes | startColor: TwoColors RGBA(0.537, 0.339, 0.275, 1) .. RGBA(0.357, 0.216, 0.201, 1) \| startSize: TwoConstants 0.2 .. 0.6 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_dragon_ice_hit |  | vfx_dragon_ice_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material cold_sparcs; _MainTex none; _Color RGBA(0.961, 1.877, 2.42, 1) |
| vfx_dragon_ice_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| vfx_dragon_ice_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_dragon_ice_hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_draugr_hit |  | vfx_draugr_hit | startColor: TwoColors RGBA(0.315, 0.449, 0.063, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_draugr_hit |  | chunks | startColor: TwoColors RGBA(0.346, 0.241, 0.053, 1) .. RGBA(0.212, 0.412, 0.076, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_draugr_hit |  | chunks/blob_splat | startColor: TwoColors RGBA(0.647, 0.524, 0.01, 1) .. RGBA(0.284, 0.684, 0.01, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_draugr_hit |  | wetsplsh | startColor: TwoColors RGBA(0.309, 0.36, 0.127, 1) .. RGBA(0.382, 0.287, 0.115, 1) \| startSize: TwoConstants 0.2 .. 1 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_draugrpile_hit |  | vfx_draugrpile_hit | startColor: TwoColors RGBA(0.315, 0.449, 0.063, 1) .. RGBA(0.574, 0.478, 0.308, 1) \| startSize: TwoConstants 0.05 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_draugrpile_hit |  | chunks | startColor: TwoColors RGBA(0.346, 0.241, 0.053, 1) .. RGBA(0.212, 0.412, 0.076, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_draugrpile_hit |  | chunks/blob_splat | startColor: TwoColors RGBA(0.647, 0.524, 0.01, 1) .. RGBA(0.284, 0.684, 0.01, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_draugrpile_hit |  | wetsplsh | startColor: TwoColors RGBA(0.309, 0.36, 0.127, 1) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 2 \| startLifetime: TwoConstants 1 .. 1.5 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_fenring_hurt |  | vfx_fenring_hurt | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_fenring_hurt |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.3 .. 0.6 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_fenring_hurt |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_fenring_hurt |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_fenring_hurt |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_fenring_hurt |  | skinflakes | startColor: TwoColors RGBA(0.36, 0.36, 0.36, 0.566) .. RGBA(0.368, 0.368, 0.368, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_foresttroll_hit |  | vfx_foresttroll_hit | startColor: TwoColors RGBA(0.337, 0.535, 0.765, 0.728) .. RGBA(0.203, 0.326, 0.919, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_foresttroll_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.581, 0, 0.036, 0.816) .. RGBA(0.824, 0, 0, 0.884) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_foresttroll_hit |  | cunks | startColor: TwoColors RGBA(0.511, 0.765, 0.337, 0.728) .. RGBA(0.559, 0.411, 0.189, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_foresttroll_hit |  | cunks/blob_splat | startColor: TwoColors RGBA(0.691, 0, 0, 1) .. RGBA(0.831, 0.049, 0.049, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_frostarrow_hit |  | vfx_frostarrow_hit | startColor: TwoColors RGBA(0.603, 0.509, 0.408, 1) .. RGBA(1, 0.937, 0.801, 1) \| startSize: TwoConstants 0.5 .. 0.8 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_frostarrow_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.03 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| vfx_frostarrow_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_frostarrow_hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_frosttroll_hit |  | vfx_frosttroll_hit | startColor: TwoColors RGBA(0.337, 0.535, 0.765, 0.728) .. RGBA(0.203, 0.326, 0.919, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_frosttroll_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.581, 0, 0.036, 0.816) .. RGBA(0.824, 0, 0, 0.884) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_frosttroll_hit |  | cunks | startColor: TwoColors RGBA(0.337, 0.535, 0.765, 0.728) .. RGBA(0.203, 0.326, 0.919, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_frosttroll_hit |  | cunks/blob_splat | startColor: TwoColors RGBA(0.691, 0, 0, 1) .. RGBA(0.831, 0.049, 0.049, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_ghost_hit |  | vfx_ghost_hit | startColor: TwoColors RGBA(0.046, 0.046, 0.046, 1) .. RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: Constant 0.5 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/LitParticles; material wraith_smoke; _MainTex wildfire06 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_dn_hit |  | vfx_goblin_dn_hit | startColor: TwoColors RGBA(0.408, 0.431, 0.38, 1) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_dn_hit |  | wetsplsh | startColor: Color RGBA(0.743, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_goblin_dn_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_dn_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_dn_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_goblin_dn_hit |  | skinflakes | startColor: Color RGBA(0.408, 0.431, 0.38, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_hit |  | vfx_goblin_hit | startColor: TwoColors RGBA(0.232, 0.772, 0.097, 0.728) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_hit |  | wetsplsh | startColor: Color RGBA(0.743, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_goblin_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblin_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_goblin_hit |  | skinflakes | startColor: Color RGBA(0.385, 0.618, 0.123, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblinbrute_hit |  | vfx_goblinbrute_hit | startColor: TwoColors RGBA(0.232, 0.772, 0.097, 0.728) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblinbrute_hit |  | wetsplsh | startColor: Color RGBA(0.743, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_goblinbrute_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_goblinbrute_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_goblinbrute_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_goblinbrute_hit |  | skinflakes | startColor: Color RGBA(0.385, 0.618, 0.123, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarf_hit |  | vfx_greydwarf_hit | startColor: TwoColors RGBA(0.772, 0.362, 0.097, 0.728) .. RGBA(0.76, 0.801, 0.041, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: Constant 0.7 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarf_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.404, 0.262, 0.048, 1) .. RGBA(0.419, 0.347, 0.046, 1) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarf_hit |  | vfx_BloodHit_decals | startColor: TwoColors RGBA(0.404, 0.262, 0.048, 1) .. RGBA(0.419, 0.347, 0.046, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarf_hit |  | vfx_BloodHit_decals/blob_splat | startColor: TwoColors RGBA(0.838, 0.468, 0, 1) .. RGBA(0.824, 0.804, 0.103, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_greydwarfnest_hit |  | vfx_greydwarfnest_hit | startColor: TwoColors RGBA(0.772, 0.362, 0.097, 0.728) .. RGBA(0.76, 0.801, 0.041, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: Constant 0.7 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarfnest_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.404, 0.262, 0.048, 1) .. RGBA(0.419, 0.347, 0.046, 1) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarfnest_hit |  | vfx_BloodHit_decals | startColor: TwoColors RGBA(0.404, 0.262, 0.048, 1) .. RGBA(0.419, 0.347, 0.046, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_greydwarfnest_hit |  | vfx_BloodHit_decals/blob_splat | startColor: TwoColors RGBA(0.838, 0.468, 0, 1) .. RGBA(0.824, 0.804, 0.103, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_greydwarfnest_hit |  | twiggs | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.2 \| startLifetime: TwoConstants 4 .. 7 \| maxParticles 100 \| simulationSpace World \| renderer: mode Mesh; shader Standard; material branch; _MainTex acacia_trunk01 (256x256); _Color RGBA(1, 0.933, 0.933, 1) |
| vfx_hatchling_hurt |  | vfx_hatchling_hurt | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_hatchling_hurt |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_hatchling_hurt |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_hatchling_hurt |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_hatchling_hurt |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_hatchling_hurt |  | skinflakes | startColor: TwoColors RGBA(0.529, 0.649, 1, 1) .. RGBA(0.338, 0.617, 1, 1) \| startSize: TwoConstants 0.2 .. 0.6 \| startLifetime: TwoConstants 2 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Mesh; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | vfx_hjall_spit_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.1 .. 0.05 \| startLifetime: TwoConstants 0.3 .. 0.6 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material cold_sparcs; _MainTex none; _Color RGBA(0.961, 1.877, 2.42, 1) |
| vfx_hjall_spit_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material coldball; _MainTex iron (64x64); _Color RGBA(0.897, 0.949, 1, 1) |
| vfx_hjall_spit_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | smoke | startColor: Color RGBA(0.544, 0.83, 1, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | smoke (1) | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | zinder | startColor: TwoColors RGBA(0.741, 0.462, 0.267, 1) .. RGBA(1, 0.936, 0.094, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material pixel_unlit_red; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | wetsplsh | startColor: TwoColors RGBA(0.875, 0.385, 0.078, 1) .. RGBA(0.67, 0.831, 0.165, 1) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 1.5 .. 3 \| maxParticles 2000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| vfx_hjall_spit_hit |  | wet trailers | startColor: TwoColors RGBA(0.34, 0.426, 0.141, 1) .. RGBA(0.434, 0.373, 0.121, 1) \| startSize: TwoConstants 0.2 .. 1 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 2000 \| simulationSpace World \| renderer: mode None; shader Lux Lit Particles/ Bumped; material slime; _MainTex slime_splash (64x64); _Color RGBA(0.699, 0.699, 0.699, 1) |
| vfx_hjall_spit_hit |  | splash decals | startColor: TwoColors RGBA(0, 0.557, 0.395, 0.491) .. RGBA(0, 0.604, 0.127, 1) \| startSize: TwoConstants 0.05 .. 0.08 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_hjall_spit_hit |  | splash decals/blood_decal | startColor: Color RGBA(1, 0.657, 0, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: TwoConstants 10 .. 11 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/Decal; material seeker_blood_splat; _MainTex brains (64x64); _Color RGBA(0.557, 0.557, 0.557, 1) |
| vfx_ice_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.647, 0.956, 1, 0.497) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_ice_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.596, 0.596, 0.596, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.04 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material ice; _MainTex none; _Color RGBA(0.838, 0.973, 1, 1) |
| vfx_leech_death | yes | vfx_leech_death | startColor: Color RGBA(0.691, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 1 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Standard; material blood_splat2; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_leech_death | yes | skinflakes | startColor: TwoColors RGBA(0.868, 0.051, 0.304, 0.566) .. RGBA(0.566, 0.2, 0.2, 1) \| startSize: TwoConstants 0.1 .. 0.4 \| startLifetime: TwoConstants 2 .. 4 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_leech_death | yes | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 3 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_leech_death | yes | vfx_BloodHit 1/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_leech_death | yes | wetsplsh | startColor: Color RGBA(0.816, 0, 0, 1) \| startSize: TwoConstants 0.4 .. 1 \| startLifetime: TwoConstants 0.5 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_leech_hit | yes | vfx_leech_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_leech_hit | yes | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.3 .. 0.6 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_leech_hit | yes | blood_drops | startColor: TwoColors RGBA(0.331, 0, 0.171, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_leech_hit | yes | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_leech_hit | yes | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_neck_hit |  | vfx_neck_hit | startColor: TwoColors RGBA(0.318, 0.434, 0.051, 1) .. RGBA(0.26, 0.441, 0.114, 1) \| startSize: TwoConstants 1.5 .. 2 \| startLifetime: Constant 0.5 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_neck_hit |  | blood_drips | startColor: TwoColors RGBA(0.048, 0.404, 0.198, 1) .. RGBA(0.374, 0.507, 0, 1) \| startSize: TwoConstants 0.04 .. 0.04 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_neck_hit |  | vfx_BloodHit_decals | startColor: TwoColors RGBA(0.17, 0.551, 0.049, 1) .. RGBA(0.09, 0.647, 0.279, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 2 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_neck_hit |  | vfx_BloodHit_decals/blob_splat | startColor: TwoColors RGBA(0.549, 0.838, 0, 1) .. RGBA(0.128, 0.824, 0.103, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: Constant 5 \| maxParticles 10 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_player_hit |  | vfx_player_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: Constant 1 \| startLifetime: Constant 5 \| maxParticles 0 \| simulationSpace Local \| renderer: Billboard, no sharedMaterial |
| vfx_player_hit |  | vfx_BloodHit 1 | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_player_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_player_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_player_hit |  | skinflakes | startColor: Color RGBA(0.831, 0.462, 0.214, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: TwoConstants 0.5 .. 1.2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_player_hit |  | wetsplsh | startColor: Color RGBA(0.971, 0, 0, 1) \| startSize: TwoConstants 0.3 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_poisonarrow_hit |  | vfx_poisonarrow_hit | startColor: TwoColors RGBA(0.531, 0.618, 0.132, 1) .. RGBA(0.963, 0.279, 0.163, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_poisonarrow_hit |  | vfx_ice_hit | startColor: TwoColors RGBA(0.3, 1, 0.125, 0.497) .. RGBA(0.262, 0.574, 0.008, 1) \| startSize: TwoConstants 0.04 .. 0.06 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_poisonarrow_hit |  | smoke | startColor: Color RGBA(0.1, 0.904, 0.105, 0.216) \| startSize: TwoConstants 2 .. 3 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_seal_hit |  | vfx_seal_hit | startColor: TwoColors RGBA(0.232, 0.772, 0.097, 0.728) .. RGBA(1, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.2 \| startLifetime: Constant 0.7 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_seal_hit |  | wetsplsh | startColor: Color RGBA(0.743, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.7 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_seal_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_seal_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_seal_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_seal_hit |  | skinflakes | startColor: Color RGBA(0.774, 0.774, 0.774, 1) \| startSize: TwoConstants 0.4 .. 0.6 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_serpent_hurt |  | vfx_serpent_hurt | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_serpent_hurt |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_serpent_hurt |  | blood_drops | startColor: TwoColors RGBA(0.331, 0, 0.171, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.07 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_serpent_hurt |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.3 .. 0.4 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_serpent_hurt |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_shrub_2_hit |  | vfx_shrub_2_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.3 .. 0.5 \| startLifetime: Constant 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Surface2; material shrub2_leafparticle; _MainTex shrub_2_particle (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_shrub_heath_hit |  | vfx_shrub_heath_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.3 .. 0.5 \| startLifetime: Constant 3 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Mesh; shader Particles/Standard Surface2; material shrub2_leafparticle_heath; _MainTex shrub_2_particle_heath (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_silvermace_hit |  | vfx_silvermace_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.5 .. 1 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_silvermace_hit |  | Snow flakes | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.06 \| startLifetime: Constant 3 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material pixel_lit; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_silvermace_hit |  | flying lumps | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.03 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Mesh; shader Standard; material snowlump; _MainTex none; _Color RGBA(0.871, 0.871, 0.871, 1) |
| vfx_skeleton_hit |  | vfx_skeleton_hit | startColor: TwoColors RGBA(0.456, 0.448, 0.335, 1) .. RGBA(0.676, 0.653, 0.612, 1) \| startSize: TwoConstants 0.02 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_skeleton_hit |  | dust | startColor: TwoColors RGBA(0.603, 0.509, 0.408, 1) .. RGBA(1, 0.937, 0.801, 1) \| startSize: TwoConstants 0.5 .. 0.8 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle_pixel; _MainTex wildfire01_pixel (32x32); _Color RGBA(1, 1, 1, 1) |
| vfx_skeleton_mace_hit |  | wetsplsh | startColor: Color RGBA(0.274, 0.816, 0.102, 0.222) \| startSize: TwoConstants 1 .. 1.5 \| startLifetime: TwoConstants 1 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Unlit2; material flame_large; _MainTex wildfire06 (128x128); _Color RGBA(1.059, 0.488, 0.216, 1) |
| vfx_skeleton_mace_hit |  | Chunks | startColor: Color RGBA(0.425, 0.625, 0.156, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 3 .. 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material blood_cloud; _MainTex brains (64x64); _Color RGBA(1, 1, 1, 1) |
| vfx_skeleton_mace_hit |  | Chunks/blob_splat | startColor: TwoColors RGBA(0.375, 0.61, 0, 1) .. RGBA(0.316, 0.463, 0.215, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 5 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_sledge_hit |  | waves | startColor: Color RGBA(1, 1, 1, 0.666) \| startSize: Constant 12 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_hit |  | Cloud | startColor: TwoColors RGBA(0.206, 0.159, 0.074, 1) .. RGBA(0.434, 0.382, 0.316, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 2 .. 4 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_hit |  | vfx_troll_rock_destroyed | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.544, 0.448, 0.36, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_hit |  | vfx_Splosh (1) | startColor: TwoColors RGBA(0.228, 0.198, 0.154, 0.978) .. RGBA(0.529, 0.437, 0.319, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_iron_hit |  | waves | startColor: Color RGBA(1, 1, 1, 0.666) \| startSize: Constant 12 \| startLifetime: Constant 0.8 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode HorizontalBillboard; shader Particles/Standard Unlit2; material block_wave; _MainTex shockwave (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_iron_hit |  | Cloud | startColor: TwoColors RGBA(0.279, 0.279, 0.279, 1) .. RGBA(0.721, 0.721, 0.721, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 2 .. 4 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_iron_hit |  | vfx_troll_rock_destroyed | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.351, 0.403, 0.434, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_sledge_iron_hit |  | vfx_Splosh (1) | startColor: TwoColors RGBA(0.346, 0.538, 0.588, 0.978) .. RGBA(0.319, 0.434, 0.529, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_stonegolem_attack_hit |  | vfx_stonegolem_attack_hit | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.01 .. 0.05 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_stonegolem_attack_hit |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.03 \| startLifetime: Constant 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_stonegolem_attack_hit |  | dust | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 3 .. 4 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| vfx_stonegolem_attack_hit |  | sparcs | startColor: TwoColors RGBA(0.169, 0.066, 0.066, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.01 .. 0.06 \| startLifetime: Constant 3 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material rock_sparcs; _MainTex none; _Color RGBA(3.428, 1.631, 0, 1) |
| vfx_stonegolem_hurt |  | vfx_stonegolem_hurt | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.01 .. 0.05 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_stonegolem_hurt |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.02 .. 0.05 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_stonegolem_hurt |  | dust | startColor: Color RGBA(1, 1, 1, 0.841) \| startSize: TwoConstants 2 .. 1 \| startLifetime: Constant 4 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 0.472) |
| vfx_stonegolem_hurt |  | sparcs | startColor: TwoColors RGBA(0.169, 0.066, 0.066, 1) .. RGBA(1, 0.683, 0, 1) \| startSize: TwoConstants 0.01 .. 0.06 \| startLifetime: Constant 3 \| maxParticles 200 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Unlit2; material rock_sparcs; _MainTex none; _Color RGBA(3.428, 1.631, 0, 1) |
| vfx_torch_hit |  | vfx_torch_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.03 .. 0.05 \| startLifetime: TwoConstants 0.2 .. 1 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Stretch; shader Legacy Shaders/Particles/Additive; material flame_small; _MainTex leaf_low (8x8); _TintColor RGBA(1, 1, 1, 1) |
| vfx_torch_hit |  | smoke | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 2 \| startLifetime: TwoConstants 4 .. 6 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material smoke; _MainTex dust01_bw (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_torch_hit |  | fire | startColor: TwoColors RGBA(1, 0.984, 0.934, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 1 .. 0.75 \| startLifetime: TwoConstants 0.3 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/Gradient Mapped Particle (Unlit); material flameball_flipbook_gradient; _MainTex flameball_flipbook (256x256) |
| vfx_tree_fall_hit |  | vfx_tree_fall_hit | startColor: TwoColors RGBA(0.603, 0.509, 0.408, 0.578) .. RGBA(1, 0.937, 0.801, 0.516) \| startSize: TwoConstants 2 .. 3 \| startLifetime: Constant 1 \| maxParticles 10 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material dust_particle; _MainTex wildfire01 (128x128); _Color RGBA(1, 1, 1, 1) |
| vfx_troll_attack_hit |  | vfx_troll_attack_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.03 .. 0.03 \| startLifetime: TwoConstants 0.3 .. 0.5 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material gnista; _MainTex crystal (64x64); _Color RGBA(2, 2, 2, 1) |
| vfx_troll_attack_hit |  | vfx_Splosh (1) | startColor: TwoColors RGBA(0.522, 0.347, 0.084, 0.766) .. RGBA(0.566, 0.397, 0.183, 0.797) \| startSize: TwoConstants 3 .. 5 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_troll_log_hitground |  | vfx_troll_log_hitground | startColor: TwoColors RGBA(0.176, 0.176, 0.176, 1) .. RGBA(0.574, 0.574, 0.574, 1) \| startSize: TwoConstants 0.05 .. 0.1 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material rocks; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_troll_log_hitground |  | vfx_RockHit (1) | startColor: TwoColors RGBA(0.419, 0.419, 0.419, 1) .. RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.05 .. 0.15 \| startLifetime: Constant 4 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Mesh; shader Custom/StaticRock; material stone; _MainTex rock_low (32x32); _Color RGBA(0.779, 0.779, 0.779, 1) |
| vfx_troll_log_hitground |  | poff_ring | startColor: TwoColors RGBA(0.206, 0.159, 0.074, 1) .. RGBA(0.434, 0.382, 0.316, 1) \| startSize: TwoConstants 1 .. 1.5 \| startLifetime: TwoConstants 0.6 .. 2 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_trollsnow_attack_hit |  | vfx_trollsnow_attack_hit | startColor: Color RGBA(1, 1, 1, 1) \| startSize: TwoConstants 0.03 .. 0.03 \| startLifetime: TwoConstants 0.3 .. 0.5 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Stretch; shader Particles/Standard Unlit2; material gnista; _MainTex crystal (64x64); _Color RGBA(2, 2, 2, 1) |
| vfx_trollsnow_attack_hit |  | vfx_Splosh (1) | startColor: TwoColors RGBA(0.624, 0.761, 0.764, 0.766) .. RGBA(0.736, 0.736, 0.736, 0.797) \| startSize: TwoConstants 3 .. 5 \| startLifetime: TwoConstants 1 .. 4 \| maxParticles 100 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material fog; _MainTex dirt (256x256); _Color RGBA(1, 1, 1, 1) |
| vfx_wolf_hit |  | vfx_wolf_hit | startColor: Color RGBA(0.86, 0, 0, 1) \| startSize: TwoConstants 1 .. 1 \| startLifetime: Constant 0.4 \| maxParticles 0 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_wolf_hit |  | wetsplsh | startColor: Color RGBA(0.809, 0, 0, 1) \| startSize: TwoConstants 0.2 .. 0.5 \| startLifetime: TwoConstants 0.5 .. 1 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Lux Lit Particles/ Bumped; material slime_green; _MainTex slime_splash (64x64); _Color RGBA(0.557, 0.702, 0.384, 1) |
| vfx_wolf_hit |  | blood_drops | startColor: TwoColors RGBA(0.331, 0.048, 0, 0.491) .. RGBA(0.544, 0, 0, 0.534) \| startSize: TwoConstants 0.05 .. 0.05 \| startLifetime: Constant 2 \| maxParticles 1000 \| simulationSpace Local \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_drop; _MainTex none; _Color RGBA(1, 1, 1, 1) |
| vfx_wolf_hit |  | bloodchunks | startColor: Color RGBA(1, 0.007, 0.007, 1) \| startSize: TwoConstants 0.2 .. 0.3 \| startLifetime: Constant 1 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_wolf_hit |  | bloodchunks/blob_splat (1) | startColor: TwoColors RGBA(0.868, 0.006, 0.006, 1) .. RGBA(0.934, 0.034, 0.034, 1) \| startSize: TwoConstants 1 .. 3 \| startLifetime: Constant 10 \| maxParticles 100 \| simulationSpace World \| renderer: mode Billboard; shader Custom/ParticleDecal; material splat_decal_blend; _MainTex brains (64x64); _Color RGBA(0.502, 0.502, 0.502, 1) |
| vfx_wolf_hit |  | skinflakes | startColor: TwoColors RGBA(0.36, 0.36, 0.36, 0.566) .. RGBA(0.838, 0.838, 0.838, 1) \| startSize: TwoConstants 0.4 .. 0.8 \| startLifetime: TwoConstants 2 .. 3 \| maxParticles 1000 \| simulationSpace World \| renderer: mode Billboard; shader Particles/Standard Surface2; material blood_splat; _MainTex leaf_low (8x8); _Color RGBA(1, 1, 1, 1) |
| vfx_wraith_hit |  | vfx_wraith_hit | startColor: TwoColors RGBA(0.046, 0.046, 0.046, 1) .. RGBA(0, 0, 0, 1) \| startSize: TwoConstants 0.5 .. 0.5 \| startLifetime: Constant 0.5 \| maxParticles 50 \| simulationSpace Local \| renderer: mode Billboard; shader Custom/LitParticles; material wraith_smoke; _MainTex wildfire06 (128x128); _Color RGBA(1, 1, 1, 1) |

## 11. Effects referenced by the dumped creatures

Creatures scanned (reflection over every public `EffectList` field of their components and of their Humanoid items SharedData / Attack): Aspect_SeekerQueen, Blob, BlobAspect, BlobElite, BlobFrost, BlobLava, BlobMork, BlobMorkMini, BlobTar, Leech, Leech_cave, Seeker, SeekerBrood, SeekerBrute, SeekerQueen, Tick. Only sfx_ / vfx_ / fx_ prefabs are listed, whether or not they are in ZNetScene.

| Prefab | Referenced by | ZNetScene | Root components | ZSFX clips |
| --- | --- | --- | --- | --- |
| fx_QueenPierceGround | Aspect_SeekerQueen:item aspect_SeekerQueen_PierceAOE.m_triggerEffect; SeekerQueen:item SeekerQueen_PierceAOE.m_triggerEffect | net | ZNetView, TimedDestruction, CamShaker | [Hit_Metal_HighPitch1, Hit_Metal_HighPitch7, Hit_Metal_HighPitch5, Hit_Metal_HighPitch3, Hit_Metal_HighPitch2, Hit_Metal_HighPitch4, Hit_Metal_HighPitch6], [Hit_Metal_HighPitch1, Hit_Metal_HighPitch7, Hit_Metal_HighPitch5, Hit_Metal_HighPitch3, Hit_Metal_HighPitch2, Hit_Metal_HighPitch4, Hit_Metal_HighPitch6], [Hit_Metal_HighPitch1, Hit_Metal_HighPitch7, Hit_Metal_HighPitch5, Hit_Metal_HighPitch3, Hit_Metal_HighPitch2, Hit_Metal_HighPitch4, Hit_Metal_HighPitch6], [Hit_Metal_HighPitch1, Hit_Metal_HighPitch7, Hit_Metal_HighPitch5, Hit_Metal_HighPitch3, Hit_Metal_HighPitch2, Hit_Metal_HighPitch4, Hit_Metal_HighPitch6] |
| fx_Queen_Death | SeekerQueen:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction, CamShaker | [HogMonster_DeathRoar5, HogMonster_DeathRoar6] |
| fx_TickBloodHit | Tick:item tick_attack_attach.m_hitEffect | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| fx_aspect_death | Aspect_SeekerQueen:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction, CamShaker | [Enemy_FrozenKing_Spirit_Death_01, Enemy_FrozenKing_Spirit_Death_02, Enemy_FrozenKing_Spirit_Death_03] |
| fx_babyseeker_death | SeekerBrood:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | [Enemy_BabySeeker_Fx_Death_01, Enemy_BabySeeker_Fx_Death_02, Enemy_BabySeeker_Fx_Death_03, Enemy_BabySeeker_Fx_Death_04] |
| fx_babyseeker_hurt | SeekerBrood:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| fx_backstab | Aspect_SeekerQueen:Humanoid.m_backstabHitEffects; Blob:Humanoid.m_backstabHitEffects; BlobAspect:Humanoid.m_backstabHitEffects; BlobElite:Humanoid.m_backstabHitEffects; BlobFrost:Humanoid.m_backstabHitEffects; BlobLava:Humanoid.m_backstabHitEffects; BlobMork:Humanoid.m_backstabHitEffects; BlobMorkMini:Humanoid.m_backstabHitEffects; BlobTar:Humanoid.m_backstabHitEffects; Leech:Humanoid.m_backstabHitEffects; Leech_cave:Humanoid.m_backstabHitEffects; Seeker:Humanoid.m_backstabHitEffects; SeekerBrood:Humanoid.m_backstabHitEffects; SeekerBrute:Humanoid.m_backstabHitEffects; SeekerQueen:Humanoid.m_backstabHitEffects; Tick:Humanoid.m_backstabHitEffects | net | TimedDestruction, ZNetView | [Magic_AiryHit1, Magic_AiryHit3] |
| fx_blobLava_explosion | BlobLava:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, CamShaker, ZNetView, TimedDestruction | - |
| fx_crit | Aspect_SeekerQueen:Humanoid.m_critHitEffects; Blob:Humanoid.m_critHitEffects; BlobAspect:Humanoid.m_critHitEffects; BlobElite:Humanoid.m_critHitEffects; BlobFrost:Humanoid.m_critHitEffects; BlobLava:Humanoid.m_critHitEffects; BlobMork:Humanoid.m_critHitEffects; BlobMorkMini:Humanoid.m_critHitEffects; BlobTar:Humanoid.m_critHitEffects; Leech:Humanoid.m_critHitEffects; Leech_cave:Humanoid.m_critHitEffects; Seeker:Humanoid.m_critHitEffects; SeekerBrood:Humanoid.m_critHitEffects; SeekerBrute:Humanoid.m_critHitEffects; SeekerQueen:Humanoid.m_critHitEffects; Tick:Humanoid.m_critHitEffects | net | TimedDestruction, ZNetView | [Smash_SpikeWallImpact] |
| fx_seeker_death | Seeker:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | [Enemy_Seeker_FX_Death_01, Enemy_Seeker_FX_Death_02, Enemy_Seeker_FX_Death_03, Enemy_Seeker_FX_Death_04, Enemy_Seeker_FX_Death_05] |
| fx_seeker_hurt | Aspect_SeekerQueen:Humanoid.m_hitEffects; Seeker:Humanoid.m_hitEffects; SeekerBrute:Humanoid.m_hitEffects; SeekerQueen:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| fx_seeker_melee_hit | Seeker:item seeker_claw_left.m_hitEffect; Seeker:item seeker_claw_right.m_hitEffect; Seeker:item seeker_groundslam.m_hitEffect; Seeker:item seeker_groundslam_flying.m_hitEffect; Seeker:item seeker_pincers.m_hitEffect; SeekerBrood:item babyseeker_attack.m_hitEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView, CamShaker | [Enemy_Seeker_Attack_VO_01, Enemy_Seeker_Attack_VO_02, Enemy_Seeker_Attack_VO_03, Enemy_Seeker_Attack_VO_04, Enemy_Seeker_Attack_VO_05, Enemy_Seeker_Attack_VO_06, Enemy_Seeker_Attack_VO_07] |
| fx_seekerbrute_death | SeekerBrute:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction, CamShaker | [Enemy_Gjall_TickDrop_FX_01, Enemy_Gjall_TickDrop_FX_02, Enemy_Gjall_TickDrop_FX_03] |
| fx_tick_death | Tick:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | [Enemy_Tick_Death_01, Enemy_Tick_Death_02, Enemy_Tick_Death_03, Enemy_Tick_Death_04, Enemy_Tick_Death_05] |
| sfx_GoblinShaman_fireball_launch | BlobTar:item blobtar_attack.m_trailStartEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Magic_Spell04] |
| sfx_HiveQueen_alerted | Aspect_SeekerQueen:MonsterAI.m_alertedEffects; SeekerQueen:MonsterAI.m_alertedEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [HiveQueen_Alerted_01, HiveQueen_Alerted_02, HiveQueen_Alerted_03, HiveQueen_Alerted_04, HiveQueen_Alerted_05] |
| sfx_HiveQueen_idle | Aspect_SeekerQueen:MonsterAI.m_idleSound; SeekerQueen:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [HiveQueen_Idle_01, HiveQueen_Idle_02, HiveQueen_Idle_03, HiveQueen_Idle_04, HiveQueen_Idle_05, HiveQueen_Idle_06] |
| sfx_baby_seeker_alerted | SeekerBrood:MonsterAI.m_alertedEffects | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_BabySeeker_Alerted_01, Enemy_BabySeeker_Alerted_02, Enemy_BabySeeker_Alerted_03, Enemy_BabySeeker_Alerted_04] |
| sfx_baby_seeker_attack_start | SeekerBrood:item babyseeker_attack.m_startEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_BabySeeker_Attack_Start_01, Enemy_BabySeeker_Attack_Start_02, Enemy_BabySeeker_Attack_Start_03, Enemy_BabySeeker_Attack_Start_04] |
| sfx_baby_seeker_idle | SeekerBrood:MonsterAI.m_idleSound | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_BabySeeker_Idle_01, Enemy_BabySeeker_Idle_02, Enemy_BabySeeker_Idle_03, Enemy_BabySeeker_Idle_04, Enemy_BabySeeker_Idle_05, Enemy_BabySeeker_Idle_06, Enemy_BabySeeker_Idle_07, Enemy_BabySeeker_Idle_08, Enemy_BabySeeker_Idle_09, Enemy_BabySeeker_Idle_10, Enemy_BabySeeker_Idle_11] |
| sfx_blob_alerted | Blob:MonsterAI.m_alertedEffects; BlobAspect:MonsterAI.m_alertedEffects; BlobElite:MonsterAI.m_alertedEffects; BlobFrost:MonsterAI.m_alertedEffects; BlobMork:MonsterAI.m_alertedEffects; BlobMorkMini:MonsterAI.m_alertedEffects; BlobTar:MonsterAI.m_alertedEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [FatMonster_Growl2] |
| sfx_blob_attack | Blob:item blob_attack_aoe.m_triggerEffect; BlobAspect:item blob_attack_aoe.m_triggerEffect; BlobElite:item blobelite_attack_aoe.m_triggerEffect; BlobFrost:item blob_frost_attack_aoe.m_triggerEffect; BlobMork:item blobmork_attack_aoe.m_triggerEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [FatMonster_Growl1] |
| sfx_blob_death | Blob:Humanoid.m_deathEffects; BlobAspect:Humanoid.m_deathEffects; BlobElite:Humanoid.m_deathEffects; BlobFrost:Humanoid.m_deathEffects; BlobMork:Humanoid.m_deathEffects; BlobMorkMini:Humanoid.m_deathEffects; BlobTar:Humanoid.m_deathEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [FishMonster_DeathRoar1, FishMonster_DeathRoar2, FishMonster_DeathRoar3] |
| sfx_blob_hit | Blob:Humanoid.m_hitEffects; BlobAspect:Humanoid.m_hitEffects; BlobElite:Humanoid.m_hitEffects; BlobFrost:Humanoid.m_hitEffects; BlobLava:Humanoid.m_hitEffects; BlobMork:Humanoid.m_hitEffects; BlobMorkMini:Humanoid.m_hitEffects; BlobTar:Humanoid.m_hitEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Hit_SlimeSplat5, Hit_SlimeSplat4, Hit_SlimeSplat3] |
| sfx_blob_idle | Blob:MonsterAI.m_idleSound; BlobAspect:MonsterAI.m_idleSound; BlobElite:MonsterAI.m_idleSound; BlobFrost:MonsterAI.m_idleSound; BlobMork:MonsterAI.m_idleSound; BlobMorkMini:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [FatMonster_Growl3] |
| sfx_blob_jump | Blob:Humanoid.m_jumpEffects; BlobAspect:Humanoid.m_jumpEffects; BlobElite:Humanoid.m_jumpEffects; BlobFrost:Humanoid.m_jumpEffects; BlobMork:Humanoid.m_jumpEffects; BlobMorkMini:Humanoid.m_jumpEffects; BlobTar:Humanoid.m_jumpEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [MudMonster_AttackSplat1] |
| sfx_bloblava_alert | BlobLava:MonsterAI.m_alertedEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView, AudioHighPassFilter | [Enemy_LavaBlob_Alert_01, Enemy_LavaBlob_Alert_02, Enemy_LavaBlob_Alert_03] |
| sfx_bloblava_death | BlobLava:Humanoid.m_deathEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_LavaBlob_DeathBurst_01, Enemy_LavaBlob_DeathBurst_02, Enemy_LavaBlob_DeathBurst_03, Enemy_LavaBlob_DeathBurst_04, Enemy_LavaBlob_DeathBurst_05, Enemy_LavaBlob_DeathBurst_06, Enemy_LavaBlob_DeathBurst_07, Enemy_LavaBlob_DeathBurst_08] |
| sfx_bloblava_grow | BlobLava:item blobLava_attack_aoe.m_attack.m_startEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_LavaBlob_Grow_01, Enemy_LavaBlob_Grow_02, Enemy_LavaBlob_Grow_03, Enemy_LavaBlob_Grow_04] |
| sfx_bloblava_idle | BlobLava:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView, AudioHighPassFilter | [Enemy_LavaBlob_Idle_01, Enemy_LavaBlob_Idle_02, Enemy_LavaBlob_Idle_03, Enemy_LavaBlob_Idle_04, Enemy_LavaBlob_Idle_05, Enemy_LavaBlob_Idle_06] |
| sfx_bloblava_jump | BlobLava:Humanoid.m_jumpEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_LavaBlob_Jump_01, Enemy_LavaBlob_Jump_02, Enemy_LavaBlob_Jump_03, Enemy_LavaBlob_Jump_04] |
| sfx_blobtar_attack_spit | BlobTar:item blobtar_attack.m_triggerEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Blood_Splat_Gib14, Blood_Splat_Gib17] |
| sfx_blobtar_idle | BlobTar:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Squish_Wet_FleshBlood01, Squish_Wet_FleshBlood03, Squish_Wet_FleshBlood04, Squish_Wet_FleshBlood05, Squish_Wet_FleshBlood06, Squish_Wet_FleshBlood07] |
| sfx_creature_consume | Tick:Humanoid.m_consumeItemEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Eat] |
| sfx_dragon_melee_hit | Aspect_SeekerQueen:item aspect_SeekerQueen_Rush.m_hitEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_Slap.m_hitEffect; SeekerQueen:item SeekerQueen_Rush.m_hitEffect; SeekerQueen:item SeekerQueen_Slap.m_hitEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView, CamShaker | [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] |
| sfx_dragon_melee_start | Aspect_SeekerQueen:item aspect_SeekerQueen_Bite.m_startEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_PierceAOE.m_startEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_Rush.m_startEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_Slap.m_startEffect; SeekerQueen:item SeekerQueen_Bite.m_startEffect; SeekerQueen:item SeekerQueen_PierceAOE.m_startEffect; SeekerQueen:item SeekerQueen_Rush.m_startEffect; SeekerQueen:item SeekerQueen_Slap.m_startEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Dragon_JumpRoar] |
| sfx_goblinking_beam | Aspect_SeekerQueen:item aspect_SeekerQueen_Spit.m_triggerEffect; SeekerQueen:item SeekerQueen_Spit.m_triggerEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView, CamShaker | [LaserBeam_Loop04] |
| sfx_greydwarf_attack_hit | Aspect_SeekerQueen:item aspect_SeekerQueen_Spit.m_hitEffect; Blob:item blob_attack_aoe.m_hitEffect; BlobAspect:item blob_attack_aoe.m_hitEffect; BlobElite:item blobelite_attack_aoe.m_hitEffect; BlobFrost:item blob_frost_attack_aoe.m_hitEffect; BlobLava:item blobLava_attack_aoe.m_hitEffect; BlobMork:item blobmork_attack_aoe.m_hitEffect; BlobTar:item blobtar_attack.m_hitEffect; Seeker:item seeker_land.m_hitEffect; Seeker:item seeker_takeoff.m_hitEffect; SeekerQueen:item SeekerQueen_Spit.m_hitEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] |
| sfx_leech_alerted | Leech:MonsterAI.m_alertedEffects; Leech_cave:MonsterAI.m_alertedEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Splash_WaterEmerge01, Splash_WaterEmerge02, Splash_WaterEmerge03, Splash_WaterEmerge04, Splash_WaterEmerge05, Splash_WaterEmerge06, Splash_WaterEmerge07, Splash_WaterEmerge08, Splash_WaterEmerge09] |
| sfx_leech_attack | Leech:item Leech_BiteAttack.m_startEffect; Leech_cave:item Leech_BiteAttack.m_startEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [LizardMan_AttackHiss1, LizardMan_AttackHiss2] |
| sfx_leech_attack_hit | Leech:item Leech_BiteAttack.m_hitEffect; Leech_cave:item Leech_BiteAttack.m_hitEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Zombie04_Claw_Swipe2, Zombie04_Claw_Swipe3] |
| sfx_leech_death | Leech:Humanoid.m_deathEffects; Leech_cave:Humanoid.m_deathEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Blood_Splat_Gib05, Blood_Splat_Gib06, Blood_Splat_Gib07] |
| sfx_leech_hit | Leech:Humanoid.m_hitEffects; Leech_cave:Humanoid.m_hitEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Blood_Splat_Gib01, Blood_Splat_Gib02, Blood_Splat_Gib03] |
| sfx_leech_idle | Leech:MonsterAI.m_idleSound; Leech_cave:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Splash_WaterEmerge01, Splash_WaterEmerge02, Splash_WaterEmerge03, Splash_WaterEmerge04, Splash_WaterEmerge05, Splash_WaterEmerge06, Splash_WaterEmerge07, Splash_WaterEmerge08, Splash_WaterEmerge09] |
| sfx_seeker_alerted | Seeker:MonsterAI.m_alertedEffects | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_Seeker_Alerted_01, Enemy_Seeker_Alerted_02, Enemy_Seeker_Alerted_03, Enemy_Seeker_Alerted_04, Enemy_Seeker_Alerted_05] |
| sfx_seeker_attack_start | Seeker:item seeker_claw_left.m_startEffect; Seeker:item seeker_claw_right.m_startEffect; Seeker:item seeker_groundslam.m_startEffect; Seeker:item seeker_groundslam_flying.m_startEffect; Seeker:item seeker_pincers.m_startEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_Seeker_Attack_Start_01, Enemy_Seeker_Attack_Start_02, Enemy_Seeker_Attack_Start_03, Enemy_Seeker_Attack_Start_04, Enemy_Seeker_Attack_Start_05] |
| sfx_seeker_brute_alerted | SeekerBrute:MonsterAI.m_alertedEffects | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Alerted_01, SeekerBrute_Alerted_02, SeekerBrute_Alerted_03, SeekerBrute_Alerted_04] |
| sfx_seeker_brute_attack_bite | SeekerBrute:item SeekerBrute_bite.m_hitEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Attack_Bite_01, SeekerBrute_Attack_Bite_02, SeekerBrute_Attack_Bite_03, SeekerBrute_Attack_Bite_04] |
| sfx_seeker_brute_attack_ram_hit | SeekerBrute:item SeekerBrute_ram.m_hitEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Attack_Ram_Hit_01, SeekerBrute_Attack_Ram_Hit_02, SeekerBrute_Attack_Ram_Hit_03] |
| sfx_seeker_brute_attack_ram_start | SeekerBrute:item SeekerBrute_ram.m_startEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Attack_Ram_Start_01, SeekerBrute_Attack_Ram_Start_02, SeekerBrute_Attack_Ram_Start_03] |
| sfx_seeker_brute_groundslam_Start | SeekerBrute:item SeekerBrute_bite.m_startEffect; SeekerBrute:item SeekerBrute_groundslam.m_startEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Attack_Slam_Start_01, SeekerBrute_Attack_Slam_Start_02, SeekerBrute_Attack_Slam_Start_03] |
| sfx_seeker_brute_groundslam_impact | SeekerBrute:item SeekerBrute_groundslam.m_triggerEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Attack_Slam_Impact_01, SeekerBrute_Attack_Slam_Impact_02, SeekerBrute_Attack_Slam_Impact_03, SeekerBrute_Attack_Slam_Impact_04, SeekerBrute_Attack_Slam_Impact_05, SeekerBrute_Attack_Slam_Impact_06] |
| sfx_seeker_brute_idle | SeekerBrute:MonsterAI.m_idleSound | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [SeekerBrute_Vocal_Idle_01, SeekerBrute_Vocal_Idle_02, SeekerBrute_Vocal_Idle_03, SeekerBrute_Vocal_Idle_04, SeekerBrute_Vocal_Idle_05, SeekerBrute_Vocal_Idle_06, SeekerBrute_Vocal_Idle_07] |
| sfx_seeker_flying | Seeker:Humanoid.m_flyingContinuousEffect | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_Seeker_Flying_Loop_01, Enemy_Seeker_Flying_Loop_02, Enemy_Seeker_Flying_Loop_03] |
| sfx_seeker_hurt | Seeker:Humanoid.m_hitEffects | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_Seeker_Hurt_01, Enemy_Seeker_Hurt_02, Enemy_Seeker_Hurt_03, Enemy_Seeker_Hurt_04, Enemy_Seeker_Hurt_05, Enemy_Seeker_Hurt_06, Enemy_Seeker_Hurt_07, Enemy_Seeker_Hurt_08] |
| sfx_seeker_idle | Seeker:MonsterAI.m_idleSound | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_BabySeeker_Idle_01, Enemy_BabySeeker_Idle_02, Enemy_BabySeeker_Idle_03, Enemy_BabySeeker_Idle_04, Enemy_BabySeeker_Idle_05, Enemy_BabySeeker_Idle_06, Enemy_BabySeeker_Idle_07, Enemy_BabySeeker_Idle_08, Enemy_BabySeeker_Idle_09, Enemy_BabySeeker_Idle_10, Enemy_BabySeeker_Idle_11] |
| sfx_seeker_overlay | Seeker:MonsterAI.m_idleSound | NOT in ZNetScene | TimedDestruction, ZSFX, AudioSource | [Enemy_Seeker_Idle_Overlay_01, Enemy_Seeker_Idle_Overlay_02, Enemy_Seeker_Idle_Overlay_03, Enemy_Seeker_Idle_Overlay_04, Enemy_Seeker_Idle_Overlay_05, Enemy_Seeker_Idle_Overlay_06, Enemy_Seeker_Idle_Overlay_07, Enemy_Seeker_Idle_Overlay_08] |
| sfx_tick_alerted | Tick:MonsterAI.m_alertedEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_Tick_Alerted_01, Enemy_Tick_Alerted_02, Enemy_Tick_Alerted_03, Enemy_Tick_Alerted_04, Enemy_Tick_Alerted_05, Enemy_Tick_Alerted_06] |
| sfx_tick_attack_drain | Tick:item tick_attack_attach.m_hitEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_Tick_Drain_01, Enemy_Tick_Drain_02, Enemy_Tick_Drain_03, Enemy_Tick_Drain_04, Enemy_Tick_Drain_05, Enemy_Tick_Drain_06, Enemy_Tick_Drain_07, Enemy_Tick_Drain_08] |
| sfx_tick_attack_jump | Tick:item tick_attack_attach.m_startEffect | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_Tick_DrainAttack_01, Enemy_Tick_DrainAttack_02, Enemy_Tick_DrainAttack_03, Enemy_Tick_DrainAttack_04, Enemy_Tick_DrainAttack_05, Enemy_Tick_DrainAttack_06] |
| sfx_tick_hurt | Tick:Humanoid.m_hitEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_Tick_Hurt_01, Enemy_Tick_Hurt_02, Enemy_Tick_Hurt_03, Enemy_Tick_Hurt_04, Enemy_Tick_Hurt_05, Enemy_Tick_Hurt_06] |
| sfx_tick_idle | Tick:MonsterAI.m_idleSound | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Enemy_Tick_Idle_01, Enemy_Tick_Idle_02, Enemy_Tick_Idle_03, Enemy_Tick_Idle_04, Enemy_Tick_Idle_05, Enemy_Tick_Idle_06, Enemy_Tick_Idle_07, Enemy_Tick_Idle_08, Enemy_Tick_Idle_09, Enemy_Tick_Idle_10] |
| sfx_troll_rock_destroyed | Aspect_SeekerQueen:Humanoid.m_jumpEffects; Seeker:Humanoid.m_jumpEffects; SeekerBrood:Humanoid.m_jumpEffects; SeekerBrute:Humanoid.m_jumpEffects; SeekerQueen:Humanoid.m_jumpEffects | net | TimedDestruction, ZSFX, AudioSource, ZNetView | [Hit_LargeRockSmash1, Hit_LargeRockSmash2, Hit_LargeRockSmash3] |
| vfx_HitSparks | Aspect_SeekerQueen:item aspect_SeekerQueen_Rush.m_hitEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_Slap.m_hitEffect; Aspect_SeekerQueen:item aspect_SeekerQueen_Spit.m_hitEffect; Blob:item blob_attack_aoe.m_hitEffect; BlobAspect:item blob_attack_aoe.m_hitEffect; BlobElite:item blobelite_attack_aoe.m_hitEffect; BlobFrost:item blob_frost_attack_aoe.m_hitEffect; BlobLava:item blobLava_attack_aoe.m_hitEffect; BlobMork:item blobmork_attack_aoe.m_hitEffect; BlobTar:item blobtar_attack.m_hitEffect; Leech:item Leech_BiteAttack.m_hitEffect; Leech_cave:item Leech_BiteAttack.m_hitEffect; Seeker:item seeker_claw_left.m_hitEffect; Seeker:item seeker_claw_right.m_hitEffect; Seeker:item seeker_groundslam.m_hitEffect; Seeker:item seeker_groundslam_flying.m_hitEffect; Seeker:item seeker_land.m_hitEffect; Seeker:item seeker_pincers.m_hitEffect; Seeker:item seeker_takeoff.m_hitEffect; SeekerBrood:item babyseeker_attack.m_hitEffect; SeekerBrute:item SeekerBrute_bite.m_hitEffect; SeekerBrute:item SeekerBrute_ram.m_hitEffect; SeekerQueen:item SeekerQueen_Rush.m_hitEffect; SeekerQueen:item SeekerQueen_Slap.m_hitEffect; SeekerQueen:item SeekerQueen_Spit.m_hitEffect | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blob_attack | Blob:item blob_attack_aoe.m_triggerEffect; BlobAspect:item blob_attack_aoe.m_triggerEffect | net | ZNetView, TimedDestruction | - |
| vfx_blob_death | Blob:Humanoid.m_deathEffects; BlobAspect:Humanoid.m_deathEffects; BlobElite:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blob_frost_attack | BlobFrost:item blob_frost_attack_aoe.m_triggerEffect | net | ZNetView, TimedDestruction | - |
| vfx_blob_frost_death | BlobFrost:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blob_frost_hit | BlobFrost:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blob_hit | Blob:Humanoid.m_hitEffects; BlobAspect:Humanoid.m_hitEffects; BlobElite:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blobelite_attack | BlobElite:item blobelite_attack_aoe.m_triggerEffect | net | ZNetView, TimedDestruction | - |
| vfx_blobmork_attack | BlobMork:item blobmork_attack_aoe.m_triggerEffect | net | ZNetView, TimedDestruction | - |
| vfx_blobmork_death | BlobMork:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blobmork_hit | BlobMork:Humanoid.m_hitEffects; BlobMorkMini:Humanoid.m_deathEffects; BlobMorkMini:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blobtar_death | BlobTar:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_blobtar_hit | BlobTar:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_boar_hit | Tick:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_leech_death | Leech:Humanoid.m_deathEffects; Leech_cave:Humanoid.m_deathEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_leech_hit | Leech:Humanoid.m_hitEffects; Leech_cave:Humanoid.m_hitEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_seekerbrute_groundslam | SeekerBrute:item SeekerBrute_groundslam.m_startEffect; SeekerBrute:item SeekerBrute_groundslam.m_triggerEffect | net | ZNetView, TimedDestruction | - |
| vfx_tar_surface | BlobTar:Humanoid.m_tarEffects | NOT in ZNetScene | ParticleSystem, ParticleSystemRenderer, TimedDestruction | - |
| vfx_troll_rock_destroyed | Aspect_SeekerQueen:Humanoid.m_jumpEffects; Seeker:Humanoid.m_jumpEffects; SeekerBrood:Humanoid.m_jumpEffects; SeekerBrute:Humanoid.m_jumpEffects; SeekerQueen:Humanoid.m_jumpEffects | net | ParticleSystem, ParticleSystemRenderer, ZNetView, TimedDestruction | - |
| vfx_water_surface | Aspect_SeekerQueen:Humanoid.m_waterEffects; Blob:Humanoid.m_waterEffects; BlobAspect:Humanoid.m_waterEffects; BlobElite:Humanoid.m_waterEffects; BlobFrost:Humanoid.m_waterEffects; BlobLava:Humanoid.m_waterEffects; BlobMork:Humanoid.m_waterEffects; BlobMorkMini:Humanoid.m_waterEffects; BlobTar:Humanoid.m_waterEffects; Leech:Humanoid.m_waterEffects; Leech_cave:Humanoid.m_waterEffects; Seeker:Humanoid.m_waterEffects; SeekerBrood:Humanoid.m_waterEffects; SeekerBrute:Humanoid.m_waterEffects; SeekerQueen:Humanoid.m_waterEffects; Tick:Humanoid.m_waterEffects | NOT in ZNetScene | ParticleSystem, ParticleSystemRenderer, TimedDestruction | - |

## 12. Albedo and gloss texture statistics

Each texture is blitted to a temporary ARGB32 RenderTexture (sRGB read/write matched to the source format so the bytes are the stored values), read back with ReadPixels; values are 0..255. Temporary objects are released.

| Source | Texture | Size / sRGB | Channel | Mean | Min | Max |
| --- | --- | --- | --- | --- | --- | --- |
| by name | babyseeker_d | 64x64 / sRGB | R | 206.365 | 74 | 255 |
| by name | babyseeker_d | 64x64 / sRGB | G | 195.695 | 77 | 251 |
| by name | babyseeker_d | 64x64 / sRGB | B | 181.071 | 74 | 247 |
| by name | babyseeker_d | 64x64 / sRGB | A | 254.969 | 241 | 255 |
| by name | Feasting_m | 64x64 / sRGB | R | 0 | 0 | 0 |
| by name | Feasting_m | 64x64 / sRGB | G | 0 | 0 | 0 |
| by name | Feasting_m | 64x64 / sRGB | B | 0 | 0 | 0 |
| by name | Feasting_m | 64x64 / sRGB | A | 150.738 | 0 | 238 |
| by name | Feasting_d | 64x64 / sRGB | R | 132.08 | 0 | 255 |
| by name | Feasting_d | 64x64 / sRGB | G | 102.086 | 0 | 251 |
| by name | Feasting_d | 64x64 / sRGB | B | 84.925 | 0 | 198 |
| by name | Feasting_d | 64x64 / sRGB | A | 229.14 | 0 | 255 |
| Leech _MainTex | swampfish_d | 128x128 / sRGB | R | 122.622 | 0 | 231 |
| Leech _MainTex | swampfish_d | 128x128 / sRGB | G | 49.568 | 0 | 255 |
| Leech _MainTex | swampfish_d | 128x128 / sRGB | B | 43.687 | 0 | 189 |
| Leech _MainTex | swampfish_d | 128x128 / sRGB | A | 247.573 | 0 | 255 |
| Blob _MainTex | blob_d | 128x128 / sRGB | R | 97.207 | 43 | 239 |
| Blob _MainTex | blob_d | 128x128 / sRGB | G | 88.638 | 37 | 223 |
| Blob _MainTex | blob_d | 128x128 / sRGB | B | 62.542 | 16 | 214 |
| Blob _MainTex | blob_d | 128x128 / sRGB | A | 254.579 | 254 | 255 |
| Blob _MainTex | dust01_bw | 256x256 / sRGB | R | 104.795 | 68 | 255 |
| Blob _MainTex | dust01_bw | 256x256 / sRGB | G | 104.795 | 68 | 255 |
| Blob _MainTex | dust01_bw | 256x256 / sRGB | B | 104.795 | 68 | 255 |
| Blob _MainTex | dust01_bw | 256x256 / sRGB | A | 57.908 | 0 | 161 |
| Blob _MainTex | slime_splash | 64x64 / sRGB | R | 187.469 | 0 | 255 |
| Blob _MainTex | slime_splash | 64x64 / sRGB | G | 187.248 | 0 | 255 |
| Blob _MainTex | slime_splash | 64x64 / sRGB | B | 187.469 | 0 | 255 |
| Blob _MainTex | slime_splash | 64x64 / sRGB | A | 61.64 | 0 | 251 |

## 13. Status effects (ObjectDB.m_StatusEffects, Infested icon candidates)

ObjectDB: 98 status effects, 94 with an icon. Candidates for the Infested icon: name containing `tick` (first with an icon), else `Poison`, else none. Choice made at runtime: icon of 'Poison' (fallback 'Poison'), sprite 'Poison'.

| Name | m_name | Type | Icon (sprite) | m_ttl | Category | Hidden | Candidate |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AdrenalineRush | $se_adrenalinerush 1 (Poussée d'adrénaline 1) | SE_Stats | Warm | 0 |  | False |  |
| AdrenalineRush2 | $se_adrenalinerush 2 (Poussée d'adrénaline 2) | SE_Stats | Warm | 0 |  | False |  |
| AdrenalineRush3 | $se_adrenalinerush 3 (Poussée d'adrénaline 3) | SE_Stats | Warm | 0 |  | False |  |
| AdrenalineRush4 | $se_adrenalinerush 4 (Poussée d'adrénaline 4) | SE_Stats | Warm | 0 |  | False |  |
| BeltStrength | $item_beltstrength (Megingjord) | SE_Stats | BeltStrength | 0 |  | False |  |
| Burning | $se_burning_name (En train de brûler) | SE_Burning | Burning | 5 |  | False |  |
| CampFire | $se_fire_name (Feu) | StatusEffect | firepit | 0 |  | False |  |
| Cold | $se_cold_name (Froid) | SE_Stats | Cold | 0 |  | False |  |
| CorpseRun | $se_corpserun_name (Course fantôme) | SE_Stats | CorpseRun | 50 |  | False |  |
| Crowned | $se_crowned_name (Royauté) | SE_Crowned | CrownofValheim | 0 |  | False |  |
| Demister | $item_demister (Lumière de feu-follet) | SE_Demister | demister | 0 |  | False |  |
| Encumbered | $se_encumbered_name (Encombrement au max) | StatusEffect | Encumbered | 0 |  | False |  |
| Freezing | $se_freezing_name (En train de geler) | SE_Stats | Cold_lvl2 | 0 |  | False |  |
| Frost | $se_frost_name (Glace) | SE_Frost | Frost | 0 |  | False |  |
| GP_Bonemass | $se_bonemass_name (Masse-d'Os) | SE_Stats | TrophyBonemass | 300 |  | False |  |
| GP_Eikthyr | $se_eikthyr_name (Eikthyr) | SE_Stats | TrophyEikthyr | 300 |  | False |  |
| GP_Fader | $se_fader_name (Pouvoir de Fader) | SE_Stats | TrophyFader | 300 |  | False |  |
| GP_Moder | $se_moder_name (Moder) | SE_Stats | TrophyDragonQueen | 300 |  | False |  |
| GP_Queen | $se_queen_name (La Reine) | SE_Stats | TrophySeekerQueen | 300 |  | False |  |
| GP_TheElder | $se_theelder_name (L'Aîné) | SE_Stats | TrophyTheElder | 300 |  | False |  |
| GP_Yagluth | $se_yagluth_name (Yagluth) | SE_Stats | TrophyGoblinKing | 300 |  | False |  |
| GoblinShaman_shield | GD Heal | SE_Shield | - | 40 |  | False |  |
| GrapplingHook | GrapplingHook | SE_Stats | WolfClaw | 0 |  | True |  |
| Harpooned | $se_harpooned_name (Harpon) | SE_Harpooned | SpearChitin | 0 |  | False |  |
| Immobilized | $se_immobilized (Immobilisation) | SE_Stats | immobilized | 5 |  | False |  |
| ImmobilizedAshlands | $se_immobilized (Immobilisation) | SE_Stats | immobilized | 10 |  | False |  |
| ImmobilizedLong | $se_immobilized (Immobilisation) | SE_Stats | immobilized | 60 |  | False |  |
| Lightning | $se_lightning_name (Foudre) | StatusEffect | Lightning | 3 |  | False |  |
| MW_SE_Infested | $mw_se_infested_name (Infesté) | SE_Stats | Poison | 1.5 |  | False |  |
| Poison | $se_poison_name (Poison) | SE_Poison | Poison | 0 |  | False | fallback |
| Potion_BugRepellent | $item_mead_bugrepellent (Concoction anti-morstique) | SE_Stats | potion_bugrepellent | 600 |  | False |  |
| Potion_LightFoot | $item_mead_lightfoot (Hydromel du pied léger) | SE_Stats | potion_lightfoot | 600 |  | False |  |
| Potion_TrollPheromones | $item_mead_trollpheromones (Potion d'amour trolle) | SE_Stats | potion_pheromones | 300 |  | False |  |
| Potion_barleywine | $item_barleywine (Vin d'orge de résistance au feu) | SE_Stats | barleywine | 600 |  | False |  |
| Potion_bzerker | $item_mead_bzerker (Hydromel des berserkir) | SE_Stats | potion_bzerker | 20 |  | False |  |
| Potion_eitr_lingering | $item_mead_eitr_lingering (Hydromel d'Eitr latent) | SE_Stats | potion_eitr_lingering | 300 | eitrpotion | False |  |
| Potion_eitr_minor | $item_mead_eitr_minor (Hydromel d'Eitr mineur) | SE_Stats | potion_eitr_minor | 120 | eitrpotion | False |  |
| Potion_frostresist | $item_mead_frostres (Hydromel de résistance au gel) | SE_Stats | potion_frostresist | 600 |  | False |  |
| Potion_hasty | $item_mead_hasty (Tonique de ratatosk) | SE_Stats | potion_hasty | 600 |  | False |  |
| Potion_health_lingering | $item_mead_hp_lingering (Hydromel de vie latente) | SE_Stats | potion_health_lingering | 300 | healthpotion | False |  |
| Potion_health_major | $item_mead_hp_major (Hydromel extrêmement revigorant) | SE_Stats | potion_health_major | 120 | healthpotion | False |  |
| Potion_health_medium | $item_mead_hp_medium (Hydromel très revigorant) | SE_Stats | potion_health_medium | 120 | healthpotion | False |  |
| Potion_health_minor | $item_mead_hp_minor (Hydromel revigorant) | SE_Stats | potion_health_minor | 120 | healthpotion | False |  |
| Potion_poisonresist | $item_mead_poisonres (Hydromel de résistance au poison) | SE_Stats | potion_poisonresist | 600 |  | False |  |
| Potion_stamina_lingering | $item_mead_stamina_lingering (Hydromel d'endurance latente) | SE_Stats | potion_stamina_lingering | 300 | staminapotion | False |  |
| Potion_stamina_medium | $item_mead_stamina_medium (Hydromel de bonne endurance) | SE_Stats | potion_stamina_medium | 120 | staminapotion | False |  |
| Potion_stamina_minor | $item_mead_stamina_minor (Hydromel d'endurance) | SE_Stats | potion_stamina_minor | 120 | staminapotion | False |  |
| Potion_strength | $item_mead_strength (Hydromel de force des trolls) | SE_Stats | potion_strength | 300 |  | False |  |
| Potion_swimmer | $item_mead_swimmer (Élixir de Vananidir) | SE_Stats | potion_swimmer | 300 |  | False |  |
| Potion_tamer | $item_mead_tamer (Infusion des murmures sauvages) | SE_Stats | potion_tamer | 600 |  | False |  |
| Potion_tasty | $item_mead_tasty (Hydromel savoureux) | SE_Stats | potion_tasty | 10 |  | False |  |
| Puke | $se_puke_name (Malade) | SE_Puke | pukeberries | 15 |  | False |  |
| Rested | $se_rested_name (En forme) | SE_Rested | rested | 1 |  | False |  |
| Resting | $se_resting_name (Repos) | SE_Cozy | resting | 0 |  | False |  |
| SE_Dvergr_buff | $dvergr_buff (Pouvoir dvergr) | SE_Stats | health_icon_walknut | 20 |  | False |  |
| SE_Dvergr_heal | Dvergr Heal | SE_Stats | - | 4 |  | False |  |
| SE_Greydwarf_shaman_frozen_heal | GD Heal | SE_Stats | - | 4 |  | False |  |
| SE_Greydwarf_shaman_heal | GD Heal | SE_Stats | - | 4 |  | False |  |
| SetEffect_AshlandsMediumArmor | $se_ashlandsmediumarmorseteffect_name (Endurance d'Ask) | SE_Stats | HelmetAshlandsMedium | 0 |  | False |  |
| SetEffect_BerserkerArmor | $se_berserkereffect_name (Berserker) | SE_Stats | HelmetBerserker | 0 |  | False |  |
| SetEffect_BerserkerUndeadArmor | $se_berserker_undead_effect_name (Colère de l'ours maudit) | SE_Stats | HelmetBerserkerUndead | 0 |  | False |  |
| SetEffect_DeepNorthMediumArmor | $se_deepnorthmediumarmorseteffect_name (Avant-garde) | SE_Stats | HelmetDNMedium | 0 |  | False |  |
| SetEffect_FenringArmor | $se_fenringseteffect_name (Bénédiction de Fenris) | SE_Stats | HelmetFenris | 0 |  | False |  |
| SetEffect_FishingHat | $item_helmet_fishinghat (Chapeau de pêcheur) | SE_Stats | HelmetFishingHat | 0 |  | False |  |
| SetEffect_HarvesterArmor | $se_harvesterseteffect_name (Moissonneur) | SE_Stats | HelmetStrawHat | 0 |  | False |  |
| SetEffect_LoxArmor | $se_loxseteffect_name (Don du lox) | SE_Stats | HelmetLox | 0 |  | False |  |
| SetEffect_MageArmor | $se_mageseteffect_name (Imprégnation d'Eitr) | SE_Stats | staminaupgrade | 0 |  | False |  |
| SetEffect_RootArmor | $se_rootseteffect_name (Armure de racine) | SE_Stats | HelmetRoot | 0 |  | False |  |
| SetEffect_TrollArmor | $se_trollseteffect_name (Discret) | SE_Stats | HelmetTrollLeather | 0 |  | False |  |
| SetEffect_WolfArmor | $se_frostres_name (Résistance au gel) | SE_Stats | ArmorWolfChest | 0 |  | False |  |
| Shelter | $se_shelter_name (Abri) | StatusEffect | Shelter | 0 |  | False |  |
| Slimed | $se_slimed_name (Engluement) | SE_Stats | Slimed | 1 |  | False |  |
| SlowFall | $se_slowfall_name (Chute de plumes) | SE_Stats | SlowFall | 0 |  | False |  |
| Smoked | $se_smoked_name (Fumé) | SE_Smoke | Smoked | 0 |  | False |  |
| SoftDeath | $se_softdeath_name (Pas de perte de compétence) | StatusEffect | SoftDeath | 600 |  | False |  |
| Spirit | $se_spirit_name (Esprit) | SE_Burning | Sun | 3 |  | False |  |
| Staff_FrostOrbs | $se_frostorbs (Sphère de vengeance) | SE_React | stafffrostorbs | 120 |  | False |  |
| Staff_shield | $se_shield (Barrière magique) | SE_Shield | Staff_Shield | 60 |  | False |  |
| Tared | $se_tared_name (Goudronné) | SE_Stats | Tared | 10 |  | False |  |
| TrinketBlackDamageHealth | $se_trinketblackdamagedealth (Peau de fer) | SE_Stats | TrinketBlackDamageHealth | 60 |  | False |  |
| TrinketBlackStamina | $se_trinketblackdtamina (Voile d'ombre) | SE_Stats | TrinketBlackStamina | 120 |  | False |  |
| TrinketBloodGoldHealth | $se_trinketbloodgoldhealth (Perce-cou) | SE_Stats | TrinketDNGold | 30 |  | False |  |
| TrinketBloodGoldStamina | $se_trinketbloodgoldstamina (Couronne de sorcière) | SE_Stats | TrinketDNNornThread | 30 |  | False |  |
| TrinketBronzeHealth | $se_trinketbronzehealth (Cœur de la forêt) | SE_Stats | TrinketBronzeHealth | 60 |  | False |  |
| TrinketBronzeStamina | $se_trinketbronzestamina (Pendentif en bronze) | SE_Stats | TrinketBronzeStamina | 60 |  | False |  |
| TrinketCarapaceEitr | $se_trinketcarapaceeitr (Boucles d'oreilles palpitantes) | SE_Stats | TrinketCarapaceEitr | 60 |  | False |  |
| TrinketChitinSwim | $se_trinketchitinswim (Aileron du destin) | SE_Stats | TrinketChitinSwim | 120 |  | False |  |
| TrinketFlametalEitr | $se_trinketflametaleitr (Jörmundling) | SE_Stats | TrinketFlametalEitr | 60 |  | False |  |
| TrinketFlametalStaminaHealth | $se_trinketflametalstaminahealth (Cœur ardent) | SE_Stats | TrinketFlametalStaminaHealth | 1 |  | False |  |
| TrinketIronHealth | $se_trinketironhealth (Broche en fer) | SE_Stats | TrinketIronHealth | 30 |  | False |  |
| TrinketIronStamina | $se_trinketironstamina (Bracelet de cheville) | SE_Stats | TrinketIronStamina | 30 |  | False |  |
| TrinketScaleStaminaDamage | $se_trinketscalestaminadamage (Fers retentissants) | SE_Stats | TrinketScaleStaminaDamage | 60 |  | False |  |
| TrinketSilverDamage | $se_trinketsilverdamage (Vision du loup) | SE_Stats | TrinketSilverDamage | 30 |  | False |  |
| TrinketSilverResist | $se_trinketsilverresist (Cœur de cristal) | SE_Stats | TrinketSilverResist | 50 |  | False |  |
| Warm | $se_warm_name (Chaud) | SE_Stats | Warm | 0 |  | False |  |
| Wet | $se_wet_name (Humide) | SE_Wet | Wet | 120 |  | False |  |
| WindRun | $se_windrun_name (Course du vent) | SE_Stats | capeasksvin | 0 |  | False |  |
| Wishbone | $se_wishbone_name (Bréchet) | SE_Finder | wishbone | 0 |  | False |  |


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
