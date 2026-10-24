# Référence vanilla (phase 0)

Référence des API et comportements de Valheim dont dépend MistWorms (spec §16 phase 0, §20.1). Les décisions qui en découlent sont consignées dans [`decisions.md`](decisions.md) ; les noms de prefabs vérifiés sont centralisés dans `src/MistWorms/VanillaNames.cs`.

- **Version analysée** : Valheim 1.0.20, build Steam 25909357 (client) / 25909431 (serveur dédié), Unity 6000.0.75f1. La partie A vient du décompilage de la 1.0.17 (non refait) ; la partie B vient du dump sur la 1.0.20.
- **Sources** : `reference/decompiled/assembly_valheim/<Type>.cs` (un fichier par type, décompilé avec ilspycmd 9.1.0.7988), plus `assembly_utils` et `assembly_guiutils`. Les numéros de ligne cités (`Character.cs:2241`) renvoient à ces fichiers.
- **Accès** : les assemblies sont publicisées à la compilation (BepInEx.AssemblyPublicizer), donc les membres `private` cités sont accessibles directement depuis le mod.
- **Deux parties** : A = ce que le code décompilé établit sans lancer le jeu ; B = les valeurs qui n'existent que dans les données des prefabs, remplies en jeu par le dump.

Légende des statuts : **confirmé** (l'hypothèse de la spec tient), **corrigé** (le code dit autre chose), **partiel** (en partie établi, le reste dépend des données), **exécution** (seul le dump ou un scénario peut répondre).

---

## A. Analyse statique du code décompilé

### A.1 Pipeline de dégâts d'un `Character` — confirmé, détaillé

Hypothèse §20.1 : « `Character.Damage` → `BlockAttack` → `ApplyArmor` → `ApplyDamage` dans cet ordre ». L'ordre tient, mais de nombreuses étapes s'intercalent.

Signatures :

- `public void Character.Damage(HitData hit)` (:2232) : calcule `hit.m_weakSpot = FindWeakSpotIndex(hit.m_hitCollider)` puis `m_nview.InvokeRPC("RPC_Damage", hit)`.
- `private void Character.RPC_Damage(long sender, HitData hit)` (:2241) : corps exécuté chez le **propriétaire** de la cible.
- `public void Character.ApplyDamage(HitData hit, bool showDamageText, bool triggerEffects, HitData.DamageModifier mod = HitData.DamageModifier.Normal)` (:2417).
- `protected virtual bool Character.BlockAttack(HitData hit, Character attacker)` (:2704), surchargé par `Humanoid.BlockAttack` (:1751).
- `protected virtual void Character.DamageArmorDurability(HitData hit)` (:2497), surchargé par `Player` (:7234).
- `protected virtual void Character.OnDamaged(HitData hit)` (:2709 ; Humanoid :470, Player :6823).
- `public virtual float Character.GetBodyArmor()` (:2699 ; Player :6862) ; `protected virtual void ApplyArmorDamageMods(ref HitData.DamageModifiers mods)` (:2695 ; Player :6842).
- `public HitData.DamageModifiers GetDamageModifiers(WeakSpot weakspot = null)` (:2409).

Ordre exact de `RPC_Damage` (Character.cs:2241-2402) :

1. Retour si `IsDebugFlying()` ; statistiques de coup du joueur local (sur tous les pairs).
2. Retour si non propriétaire.
3. `attacker = hit.GetAttacker()` ; si `hit.m_staggerMultiplier >= 100` → `Stagger(hit.m_dir)` **avant** le test d'esquive (:2261-2264).
4. Retour si PV ≤ 0, mort, téléportation, cinématique, ou `hit.m_dodgeable && IsDodgeInvincible()` (:2265) : les frames d'invincibilité annulent **tout** le coup (blocage, poussée, effet, armure compris).
5. Ajout d'eitr.
6. **Retour si `hit.HaveAttacker() && attacker == null`** (:2273), ou règle JcJ.
7. Si `attacker != null && !attacker.IsPlayer()` : `hit.ApplyModifier(Game.instance.GetDifficultyDamageScalePlayer(pos))` puis `hit.ApplyModifier(Game.m_enemyDamageRate)` (:2277-2282).
8. Comptabilité attaquant-joueur, modificateurs de mise à mort.
9. `m_seman.OnDamaged(hit, attacker)` (:2331, `SE_Shield` absorbe ici).
10. Aggravation ; dos ×`m_backstabBonus` ; ×2 si `IsStaggering() && !IsPlayer()` (:2342-2346).
11. `if (hit.m_blockable && IsBlocking()) BlockAttack(hit, attacker)` (:2347-2350).
12. `ApplyPushback(hit)` (:2355), après le blocage qui peut réduire `m_pushForce`.
13. Effet de statut `hit.m_statusEffectHash`.
14. `hit.ApplyResistance(GetDamageModifiers(weakSpot), out significantModifier)` (:2379).
15. Joueur : `hit.ApplyArmor(GetBodyArmor())` puis `DamageArmorDurability(hit)` ; non-joueur en NG+ (`Game.m_worldLevel > 0`) : `hit.ApplyArmor(worldLevel × m_worldLevelEnemyBaseAC)` (:2380-2389).
16. Poison, feu et esprit retirés du coup (:2390-2401).
17. `ApplyDamage(hit, true, true, significantModifier)`.
18. Les parts retirées deviennent des DoT (`AddFire` / `AddSpirit` / `AddPoison`) ; gel et foudre deviennent des effets de statut.

`ApplyDamage` (:2417-2491) : retour si vol de debug, mort, téléportation, cinématique ; `totalDamage = hit.GetTotalDamage()` (valeur **avant** mise à l'échelle, utilisée par le texte) ; cible non joueur : `ApplyModifier(GetDifficultyDamageScaleEnemy)` et `ApplyModifier(Game.m_playerDamageRate)`, **quel que soit l'attaquant** ; cible joueur : `ApplyModifier(Game.m_localDamgeTakenRate)` (:2432) ; retour si total ≤ 0,1 ; `DamageText.ShowText(...)` (:2446) ; `m_lastHit = hit` si PV > 0 (:2456) ; `SetHealth(health − total)` ; `AddStaggerDamage(...)` (:2469) ; `OnDamaged(hit)` (:2478) ; `m_onDamaged(total, hit.GetAttacker())` (:2485). Mode dieu / fantôme : les PV sont bornés à 1, pas d'annulation (:2449-2461).

La mort n'est **pas** traitée dans `ApplyDamage` : `private void CheckDeath()` (:2866) tourne plus tard dans `CustomFixedUpdate` (propriétaire, :826-845) et appelle `OnDeath()`.

Esquive : `Player.IsDodgeInvincible()` (:5837-5848) lit `m_dodgeInvincibleCached` chez le propriétaire, la variable ZDO `s_dodgeinv` sinon.

### A.2 `HitData` et types de dégâts — confirmé

Champs publics (HitData.cs:676-724) : `DamageTypes m_damage`, `bool m_dodgeable`, `bool m_blockable` (**tous deux `false` par défaut**, :678-680), `bool m_ranged`, `bool m_ignorePVP`, `short m_toolTier`, `float m_pushForce`, `float m_backstabBonus = 1`, `float m_staggerMultiplier = 1`, `Vector3 m_point`, `Vector3 m_dir`, `int m_statusEffectHash`, `ZDOID m_attacker = ZDOID.None`, `Skills.SkillType m_skill`, `float m_skillRaiseAmount = 1`, `float m_skillLevel`, `short m_itemLevel`, `byte m_itemWorldLevel`, `HitType m_hitType`, `float m_healthReturn`, `float m_eitrAdd`, `float m_radius`, `short m_weakSpot = -1`, `short m_variant`, `Collider m_hitCollider` (**non sérialisé**). Tout le reste est sérialisé (:740-903), `m_hitType` sous forme d'octet.

- `DamageTypes` : `m_damage, m_blunt, m_slash, m_pierce, m_chop, m_pickaxe, m_fire, m_frost, m_lightning, m_poison, m_spirit, m_nonPlayer`.
- `enum DamageModifier { Normal, Resistant, Weak, Immune, Ignore, VeryResistant, VeryWeak, SlightlyResistant, SlightlyWeak }` : multiplicateurs 1 ; 0,5 ; 1,5 ; 0 ; 0 ; 0,25 ; 2 ; 0,75 ; 1,25 (:67-78). **Neuf valeurs**, la spec §3.2 en liste sept.
- `struct DamageModifiers` : `m_blunt, m_slash, m_pierce, m_chop, m_pickaxe, m_fire, m_frost, m_lightning, m_poison, m_spirit, m_nonPlayer` (:118-140). `Character.m_damageModifiers` (Character.cs:285), `WearNTear.m_damages`, `Destructible.m_damages`.
- `enum HitType : byte { Undefined, EnemyHit, PlayerHit, Fall, Drowning, Burning, Freezing, Poisoned, Water, Smoke, EdgeOfWorld, Impact, Cart, Tree, Self, Structural, Turret, Boat, Stalagtite, Catapult, CinderFire, AshlandsOcean, AshlandsLava, Incinerator, DrawBridge }` (:80-107).

Méthodes :

- `HitData()`, `HitData(float damage)`, `HitData Clone()` (:735).
- `bool HaveAttacker()` (:1048), `Character GetAttacker()` (:1053), `void SetAttacker(Character)` (:1071).
- `void ApplyArmor(float ac)` (:1005) → `DamageTypes.ApplyArmor(float ac)` (:424) → `static DamageTypes.ApplyArmor(float dmg, float ac)` (:414). Les seuls appelants de `HitData.ApplyArmor` sont Character.cs:2383 et :2388 ; le blocage passe par `DamageTypes.ApplyArmor`.
- `void ApplyResistance(DamageModifiers, out DamageModifier significantModifier)` (:969).
- `void ApplyModifier(float)` (:1010) : **ne multiplie ni `m_damage.m_damage` ni `m_nonPlayer`**.
- `float GetTotalDamage()` (:915) : **ajoute `worldLevel × Game.instance.m_worldLevelEnemyBaseDamage` (85)** si l'attaquant résout vers un `Character` non joueur et que `Game.m_worldLevel > 0`. Utilisé par `ApplyDamage`, `WearNTear`, `Destructible`. `DamageTypes.GetTotalDamage()` (:323) n'ajoute rien.
- `GetTotalPhysicalDamage()` (:905), `GetTotalElementalDamage()` (:910), `GetTotalBlockableDamage()` (:1024), `void BlockDamage(float)` (:1029), `DamageTypes.GetTotalStaggerDamage()` (:308, contondant + tranchant + perçant + foudre), `DamageTypes.Modify(float)` (:349).

Pièges : le feu, le poison et l'esprit ne touchent jamais les PV directement ; ils deviennent des DoT `SE_Burning` / `SE_Poison` qui appellent `ApplyDamage` avec une `HitData` **sans attaquant** (`HitType.Burning`, SE_Burning.cs:28-71). Une mise à mort par brûlure n'est attribuée à personne.

Constantes de `Game` : statiques `m_playerDamageRate` (:186), `m_enemyDamageRate` (:188), `m_localDamgeTakenRate` (:192), `m_durabilityRate` (:202), `int m_worldLevel` (:220) ; d'instance `m_worldLevelEnemyBaseAC = 100` (:166), `m_worldLevelEnemyHPMultiplier = 2` (:168), `m_worldLevelEnemyBaseDamage = 85` (:170), `m_damageScalePerPlayer = 0.04` (:142), `m_healthScalePerPlayer = 0.3` (:144) ; `float GetDifficultyDamageScalePlayer(Vector3)` (:1155), `float GetDifficultyDamageScaleEnemy(Vector3)` (:1161).

### A.3 Blocage et parade — corrigé

`protected override bool Humanoid.BlockAttack(HitData hit, Character attacker)` (Humanoid.cs:1751-1876) :

- Échec (aucun blocage) si `Vector3.Dot(hit.m_dir, transform.forward) > 0` (:1753) : `m_dir` doit pointer de l'attaquant vers la cible.
- Parade parfaite : `blocker.m_shared.m_timedBlockBonus > 1 && m_blockTimer != -1 && m_blockTimer < 0.25f` (:1762) ; elle multiplie la puissance de blocage par `m_timedBlockBonus` (et `SEMan.ModifyTimedBlockBonus`).
- Bloqué = `blockable − DamageTypes.ApplyArmor(blockable, power)` (:1776-1781). Avec `ac ≥ dmg/2`, le reste vaut `clamp01(dmg/(4ac)) × dmg` : faible, **jamais nul**. Une parade parfaite **n'annule pas** le coup.
- Le blocage ne retire des dégâts que s'il reste de l'endurance et si l'étourdissement de garde ne s'est pas déclenché (sinon rien n'est bloqué).
- Endurance : parade parfaite → `m_perfectBlockStaminaDrain` au lieu de `m_blockStaminaDrain × ratio`, puis un second prélèvement ou `m_perfectBlockStaminaRegen` (:1782-1792).
- Bouclier : perd `m_useDurabilityDrain × (totalBlockable / blockPower) × Game.m_durabilityRate`, que le blocage réussisse ou non, sans plancher à 0 (:1803-1807).
- Bonus de parade parfaite (effet, adrénaline, étourdissement de l'attaquant si `attacker.m_staggerWhenBlocked`, effet de statut) : seulement si `(bool)attacker` (:1825-1860).
- Tout blocage réussi : `hit.m_statusEffectHash = 0`, `hit.m_pushForce *= ratio` ; si le coup n'est pas `m_ranged` et a un attaquant, une `HitData` de déviation (poussée seule, sans attaquant) est envoyée par `attacker.Damage()` (:1861-1874).
- La méthode renvoie `true` pour **tout** blocage, parfait ou non : le drapeau « parfait » doit être recalculé dans un préfixe.

Champs : `private float m_blockTimer` (:119), `public float m_blockStaminaDrain = 25` (:35), `m_perfectBlockStaminaDrain` (:37), `StatusEffect m_perfectBlockStatusEffect` (:39), `public override bool IsBlocking()` (:1878) ; côté objet : `m_blockPower` (ItemDrop.cs:212), `m_timedBlockBonus = 1.5` (:220), `m_perfectBlockStaminaRegen` (:222), `m_perfectBlockAdrenaline` (:244) ; `float ItemData.GetBlockPower(float skillFactor)` (:722), `GetDeflectionForce()` (:775).

### A.4 Étourdissement et recul — confirmé

- `public void Character.Stagger(Vector3 forceDirection)` (:2663) : le propriétaire appelle directement `RPC_Stagger(0L, dir)` ; un non-propriétaire envoie le RPC, qui arrive chez le propriétaire **sans passer par `Stagger`**.
- `private void RPC_Stagger(long sender, Vector3 forceDirection)` (:2673) ; `public bool IsStaggering()` (:3735).
- `protected bool AddStaggerDamage(float damage, Vector3 forceDirection, HitData hit)` (:2613) : sort immédiatement si `m_staggerDamageFactor <= 0`.
- Champs : `public bool m_staggerWhenBlocked = true` (:290, lu sur l'**attaquant** dans `BlockAttack`), `public float m_staggerDamageFactor` (:292), `public float m_enemyAdrenalineMultiplier = 1` (:294).
- Appelants de `Stagger` : `RPC_Damage` si `m_staggerMultiplier >= 100`, le seuil d'`AddStaggerDamage`, la parade parfaite (sur l'attaquant), Trap.cs:247.
- Recul : `private void ApplyPushback(HitData hit)` (:1904) → `public void ApplyPushback(Vector3 dir, float pushForce)` (:1909) : force = `pushForce × clamp01(1 + equipmentMovementModifier) / m_body.mass × 2.5`, horizontale, remplace `m_pushForce` (`protected Vector3`, :476) seulement si plus grande ; décroît de 100/s (`UpdatePushback`). La surcharge publique est aussi appelée par `Aoe` (Aoe.cs:718) et le recul d'attaque (Attack.cs:646).

### A.5 Santé, niveau, nom affiché — confirmé

- `public virtual string GetHoverName()` (:3868) : nom du `Tameable` s'il existe, sinon la chaîne ZDO `s_overrideHoverName` si non vide, sinon `Localization.instance.Localize(m_name)`. `EnemyHud` relocalise `GetHoverName()` à chaque mise à jour (EnemyHud.cs:154, :200) : un nom dynamique fonctionne.
- `public float GetHealth()` (:3003) ; `public void SetHealth(float)` (:3008) écrit ZDO `s_health` **seulement chez le propriétaire**, borne à 0.
- `public void SetMaxHealth(float)` (:3058) écrit `s_maxHealth` **sans test de propriétaire**, puis abaisse les PV si besoin (ne les augmente jamais).
- `public float GetMaxHealth()` (:3070, ZDO, défaut `m_health`) ; `public float GetMaxHealthBase()` (:3079) = `m_health`, × `worldLevel × m_worldLevelEnemyHPMultiplier` pour un non-joueur en NG+.
- `public void SetLevel(int level)` (:775) → `SetupMaxHealth()` (:747) = `SetMaxHealth(GetMaxHealthBase() × level)`, puis `m_onLevelSet` ; `public int GetLevel()` (:789).
- **`Awake` relance `SetupMaxHealth()`** pour un non-joueur propriétaire à pleine santé (:694-703) : un `s_maxHealth` personnalisé est écrasé à chaque réinstanciation.
- `public virtual void OnDeath()` (:2907-3001) : statistiques via `m_lastHit?.GetAttacker()` ; les non-propriétaires sortent tôt ; le propriétaire enregistre les morts, crée `m_deathEffects` (ragdoll), invoque `m_onDeath` (CharacterDrop, BaseAI, Tameable) puis `ZNetScene.instance.Destroy(gameObject)`. `Player.OnDeath` (:3314) ne rappelle pas la base et lit `m_lastHit.m_hitType` **sans test de nullité** (:3326).
- `public Action<float, Character> m_onDamaged` (:68), `public Action m_onDeath` (:70), `public string m_name` (:79), `public float m_health = 10` (:271).

### A.6 Attaquant d'une `HitData` (`GetAttacker`) — corrigé

`GetAttacker()` (HitData.cs:1053-1069) est sûr : `null` si `m_attacker.IsNone()`, si `ZNetScene.instance` est nul, si `FindInstance(m_attacker)` ne trouve pas d'objet **sur ce pair**, ou si l'objet n'a pas de `Character`.

Mais `Character.RPC_Damage` **jette le coup entier** quand `hit.HaveAttacker() && attacker == null` (Character.cs:2273). Un ZDOID de colonie sans `Character`, ou un amas non instancié chez le propriétaire de la cible, rend donc les morsures sans effet sur joueurs et créatures. `WearNTear.RPC_Damage` et `Destructible.RPC_Damage` ne jettent pas ces coups (ils sautent seulement la notification `PrivateArea`).

Déréférencement sans test : `MineRock.cs:153` et `:180` (`hit.GetAttacker().GetZDOID()` → `NullReferenceException`). Ailleurs, les accès sont protégés : Character.cs:2247, 2283, 2332, 2340/2345, 2368, 2433, 2449, 2485, 2632, OnDeath 2910 ; MonsterAI.OnDamaged → SetTarget (MonsterAI.cs:184-201) ; BaseAI.OnDamaged (BaseAI.cs:704-707) ; AnimalAI.cs:25-29 ; SE_React.cs:23 ; WearNTear.cs:1208-1223, 1317-1320 ; Destructible.cs:129-135, 185-196 ; MineRock5.cs:389 ; TreeBase.cs:135 ; TreeLog.cs:183 ; Humanoid.BlockAttack (:1821, 1825, 1864).

Messages de mort : `BaseAI.OnDeath` n'affiche qu'un `m_deathMessage` statique (BaseAI.cs:709-715) ; `Player.OnDeath` ne lit pas l'attaquant. Compétences : montées côté attaquant (Attack/Projectile/Aoe), jamais via `GetAttacker` chez la victime. `Game.RegisterKill` ne compte que les attaquants `Player` (ZDO `s_attackers`, :2283-2293, :2937-2952).

### A.7 Drops (créatures, structures) — corrigé

- `public List<KeyValuePair<GameObject,int>> CharacterDrop.GenerateDropList()` (CharacterDrop.cs:72, **méthode d'instance**) : niveau 2^(niveau−1) sur chance et quantité, tirage pseudo-aléatoire si chance ≤ 0,3, `Game.ScaleDrops`, `m_onePerPlayer`, plafond 100.
- `public static void CharacterDrop.DropItems(List<KeyValuePair<GameObject,int>> drops, Vector3 centerPos, float dropArea, bool cheated)` (:169) : instancie, pose `m_worldLevel` et `m_cheated`.
- `private void CharacterDrop.OnDeath()` (:62), abonné à `Character.m_onDeath` dans `Start` : `GenerateDropList` + `DropItems(drops, center, 0.5f, m_cheated)` si `m_dropsEnabled` ; `public void SetDropsEnabled(bool)` (:57).
- **Chemin ragdoll** : si l'effet de mort contient un `Ragdoll` avec `m_dropItems = true` (défaut, Ragdoll.cs:21), `Ragdoll.Setup(Vector3 velocity, float hue, float saturation, float value, CharacterDrop characterDrop, int level = 1, bool cheated = false)` (:112) → `private void SaveLootList(CharacterDrop)` (:137) appelle `GenerateDropList()` **pendant** `OnDeath` et stocke la liste dans la ZDO du ragdoll (`drop_hash`/`drop_amount`) ; `Character.OnDeath` désactive alors le `CharacterDrop` (:2959-2985). Le butin apparaît **plus tard**, hors `OnDeath`, par `Ragdoll.DestroyNow` → `private void SpawnLoot(Vector3)` (:155) → `CharacterDrop.DropItems`. Une liste vide n'enregistre rien.
- Structures : `public void WearNTear.Damage(HitData)` (:1176) → `private void RPC_Damage(long, HitData)` (:1193, propriétaire) : `ApplyResistance(m_damages)`, `GetTotalDamage`, texte, notification `PrivateArea` si l'attaquant résout, test de `m_minToolTier`, puis `public bool ApplyDamage(float damage, HitData hitData = null)` (:1248) → `private void Destroy(HitData hitData = null, bool blockDrop = false)` (:1288) → `m_piece.DropResources(hitData)` (:1302, synchrone sauf `blockDrop`), `m_onDestroyed`, effets, `ZNetScene.Destroy`. `public void Remove(bool blockDrop = false)` (:1272) → `RPC_Remove` → `Destroy(null, blockDrop)`. L'effondrement structurel appelle `ApplyDamage(damage)` **sans `HitData`** (:688).
- `public void Piece.DropResources(HitData hitData = null)` (Piece.cs:339) : rien en mode construction libre ; pour chaque `m_resources` avec `m_recover`, quantité `m_amount` ; `Game.CheckDropConversion(hitData, …)` ; ÷3 (min 1) si `!IsPlacedByPlayer()` (:457) ; `ItemDrop` ou conteneur `m_destroyedLootPrefab`. Autres appelants : `Character.ApplyDamage` si PV < 0 (:2481), déconstruction par le joueur (Player.cs:3008).
- `public int Game.ScaleDrops(GameObject drop, int randomMin, int randomMax)` (:1309) ; `public GameObject Game.CheckDropConversion(HitData, ItemDrop, GameObject dropPrefab, ref int dropCount)` (:1441).
- Champs `WearNTear` : `m_health` (:108), `m_damages` (:110), `m_triggerPrivateArea` (:118), `m_onDestroyed` (:43), `m_onDamaged` (:45).

### A.8 Pierre de garde et structures — corrigé

Une pierre de garde (`PrivateArea`) **n'empêche pas** les dégâts sur un `WearNTear` : `RPC_Damage` appelle seulement `static PrivateArea.OnObjectDamaged(Vector3 point, Character attacker, bool destroyed)` (PrivateArea.cs:412) quand l'attaquant résout (flash du bouclier, réaction de faction), puis applique les dégâts (WearNTear.cs:1199-1220). Les dégâts ne sont bloqués que par `m_damages` (Immune/Ignore), `m_minToolTier` (TooHard), PV ≤ 0 ou l'absence de propriété.

### A.9 Durabilité, casse, recettes — confirmé

- `SharedData` (ItemDrop.cs:293-306) : `bool m_useDurability`, `bool m_destroyBroken = true`, `bool m_canBeReparied = true` (sic), `float m_maxDurability = 100`, `float m_durabilityPerLevel = 50`, `float m_useDurabilityDrain = 1`, `float m_durabilityDrain` (par seconde équipé).
- `ItemData` : `float m_durability = 100` (:419, sérialisé en `int × 100`, précision 0,01), `int m_quality` (:421), `int m_worldLevel` (:425), `bool m_equipped` (:444, non sérialisé) ; `float GetMaxDurability()` (:688) = `GetMaxDurability(m_quality)` ; `float GetMaxDurability(int quality)` (:693) = `m_maxDurability + max(0, quality−1) × m_durabilityPerLevel` ; `GetDurabilityPercentage()` (:678).
- Casse : `private void Humanoid.UpdateEquipment(float dt)` (:377, propriétaire, joueurs uniquement, chaque `FixedUpdate`) appelle `private void DrainEquipedItemDurability(ItemData, float dt)` (:456) pour chaque emplacement équipé avec `m_useDurability`, même si `m_durabilityDrain` vaut 0. Si `!(m_durability > 0)` : `Message(TopLeft, Localize("$msg_broke", m_shared.m_name), 0, icon)`, `UnequipItem(item, false)`, puis `m_inventory.RemoveItem(item)` si `m_destroyBroken`. Un objet **équipé** mis à 0 casse donc tout seul au `FixedUpdate` suivant. Un objet **non équipé** à 0 reste simplement cassé : `EquipItem` le refuse (:1073), l'interface l'affiche en rouge.
- `public bool Humanoid.EquipItem(ItemData item, bool triggerEquipEffects = true)` (:1055) ; `public void UnequipItem(ItemData item, bool triggerEquipEffects = true)` (:1274).
- Usure d'armure vanilla : `Player.DamageArmorDurability` (:7234-7263) use une pièce d'armure au hasard de dégâts physiques + élémentaires, juste après `ApplyArmor`, bornée à 0. `GetArmor` ignore la durabilité.
- `Inventory.RemoveItem(ItemData)` (:344, bool), `private void Changed(bool success = false, bool cheatedStateChanged = false)` (:1107), `GetAllItems()` (:688), `GetEquippedItems()` (:653).
- `ObjectDB.GetRecipe(ItemData item)` (ObjectDB.cs:142, instance) : parcours linéaire de `m_recipes`, première recette dont `m_item.m_itemData.m_shared.m_name` correspond (ignore `m_enabled`), sinon `null`. `ObjectDB.GetItemPrefab(string)` (:82).
- `Recipe` (Recipe.cs:5-27) : `ItemDrop m_item`, `int m_amount = 1` (quantité produite), `bool m_enabled`, `bool m_requireOnlyOneIngredient`, `Piece.Requirement[] m_resources`.
- `Piece.Requirement` (Piece.cs:85-112) : `ItemDrop m_resItem`, `int m_amount = 1`, `int m_extraAmountOnlyOneIngredient`, `int m_amountPerLevel = 1`, `bool m_upgraderResource`, `bool m_recover = true`, `int GetAmount(int qualityLevel)` : `q ≤ 1` → `m_amount` ; sinon `floor(f(q) × m_amountPerLevel + (m_upgraderResource ? m_amount : 0))` avec `f = q−1` pour `q < 4` et `4 + (q−4)/2` pour `q ≥ 4`. C'est le coût **d'une étape** d'amélioration vers `q`, pas un cumul.

### A.10 Texte flottant (`DamageText`) — partiel

- `public static DamageText instance` (:49) ; `enum TextType { Normal, Resistant, Weak, Immune, Heal, TooHard, Blocked, Bonus }` (:8-18).
- `ShowText(HitData.DamageModifier type, Vector3 pos, float dmg, bool player = false)` (:146) ; `ShowText(TextType type, Vector3 pos, float dmg, bool player = false)` (:179, format `"0.#"`) ; `ShowText(TextType type, Vector3 pos, string text, bool player = false)` (:184).
- Tous envoient un RPC routé à tout le monde (`ZRoutedRpc.Everybody = 0`) ; le récepteur ignore au-delà de `m_maxTextDistance` (30 m) puis applique `Localization.instance.Localize(text)` : les jetons `$…` d'une chaîne sont traduits.
- Couleurs (:105-140) : texte sur soi (`player` et expéditeur local) et type ≤ Immune → rouge ; sinon Normal blanc, Resistant/Immune gris, Weak jaune, TooHard rosé, **Bonus orange (1 ; 0,63 ; 0,24), police ×1,5, 3 s**, Heal vert avec `+`, Blocked préfixé `$msg_blocked: `. Aucun paramètre de couleur.

### A.11 Factions et `IsEnemy` — corrigé

`enum Character.Faction` (Character.cs:8-24) : `Players=0, AnimalsVeg=1, ForestMonsters=2, Undead=3, Demon=4, MountainMonsters=5, SeaMonsters=6, PlainsMonsters=7, Boss=8, MistlandsMonsters=9, Dverger=10, PlayerSpawned=11, TrainingDummy=12, DeepNorth=13`. Défaut `m_faction = AnimalsVeg` (:83).

`public bool BaseAI.IsEnemy(Character other)` (BaseAI.cs:1188) délègue à `public static bool BaseAI.IsEnemy(Character a, Character b)` (:1193-1289). Logique, dans l'ordre :

1. `a == b` ou l'un nul → `false`.
2. `m_group` non vide et identique → `false`.
3. Apprivoisés : `false` seulement si les deux le sont, ou apprivoisé contre `Players`, ou contre `Dverger` non aggravé ; sinon `true` (un apprivoisé est ennemi de toute créature sauvage hors Players/Dverger).
4. Aggravé contre `Players` → `true`.
5. Même faction → `false`.
6. Selon la faction de `a` (relation **asymétrique**) : `AnimalsVeg`, `PlayerSpawned` → toujours `true` ; `Players` → `b != Dverger` ; `ForestMonsters` → ni AnimalsVeg ni Boss ; `Undead` → ni Demon ni Boss ; `Demon` → ni Undead ni Boss ; `Mountain/Sea/PlainsMonsters` → pas Boss ; `MistlandsMonsters`, `DeepNorth` → ni AnimalsVeg ni Boss ; `Dverger` → ni AnimalsVeg, ni Boss, ni Players ; **`Boss` → seulement `Players` ou `PlayerSpawned`** ; `TrainingDummy` → seulement `Players`.

`Boss` n'est donc **pas** l'ennemi de tous : aucune faction de monstres ne le considère comme ennemi. **`PlayerSpawned` est la seule faction hostile à toutes, dans les deux sens** (seule exception : TrainingDummy → PlayerSpawned). Deux `PlayerSpawned` sont amis. `PlayerSpawned` n'a pas d'autre effet dans le code ; `IsMonsterFaction` (:4206) l'exclut, ce qui n'affecte que LootSpawner.cs:80.

`IsEnemy` décide aussi **qui peut blesser qui** : Attack.cs:1152-1156 (un attaquant non joueur sans `m_hitFriendly` ignore les non-ennemis), Aoe.cs:488, Projectile.cs:504/548/638 ; et la couleur de barre d'`EnemyHud` (:216). La cible est lâchée dès que `!IsEnemy` (MonsterAI.cs:300).

### A.12 Peur du feu — partiel

- Champs : `public bool m_avoidFire` (BaseAI.cs:102), `public bool m_afraidOfFire` (:104), défaut `false`.
- Appel : `MonsterAI.UpdateAI` (:407) `if ((m_afraidOfFire || m_avoidFire) && AvoidFire(dt, m_targetCreature, m_afraidOfFire))` ; avec `m_afraidOfFire`, les cibles sont effacées et `UpdateAI` retourne (:407-415).
- `protected bool AvoidFire(float dt, Character moveToTarget, bool superAfraid)` (:584-620) : apprivoisé → `false`. `superAfraid` : `EffectArea.IsPointInsideArea(transform.position, EffectArea.Type.Fire, 3f)` ; un succès mémorise `m_nearFireTime` et `m_nearFireArea` ; tant que `Time.time − m_nearFireTime < 6f` → `SetAlerted(true)`, `Flee(dt, m_nearFireArea.transform.position)`, `true`. Évitement simple : tourne autour de la zone via `RandomMovementArroundPoint` à `(GetRadius()+3) × 1.5` (ou `GetRadius()+3+1` si la cible est elle-même dans le feu), sans fuite.
- `public static EffectArea EffectArea.IsPointInsideArea(Vector3 p, EffectArea.Type type, float radius = 0f)` (EffectArea.cs:197-213) : `Physics.OverlapSphereNonAlloc` sur la couche **`character_trigger`**, premier collider dont `(m_type & type) != 0`. Les 3 m codés en dur sont le rayon d'une sphère autour du pivot : la distance effective au centre du feu est **rayon du collider + 3 m**.
- `[Flags] enum EffectArea.Type : byte { None=0, Heat=1, Fire=2, PlayerBase=4, Burning=8, Teleport=0x10, NoMonsters=0x20, WarmCozyArea=0x40, PrivateProperty=0x80 }` ; `public float GetRadius()` (:159-171) ; `public static List<EffectArea> GetAllAreas()` (contient aussi les zones **inactives** : remplie en `Awake`, vidée en `OnDestroy`, :87-92).
- `protected bool Flee(float dt, Vector3 from)` (:556-582) : nouvelle cible toutes les `m_fleeInterval` (2 s), 10 directions dans ±`m_fleeAngle` (45°) à `m_fleeRange` (25 m), chacune avec `HavePath`.
- `IsValidRandomMovePoint` rejette les points dans une zone de feu si l'un des drapeaux est posé (:697).
- **Aucun critère « torche tenue »** : `BaseAI`/`MonsterAI` n'inspectent jamais l'équipement. Les seules requêtes `Type.Fire` sont BaseAI.cs:592/607/610/697 et MistEmitter.cs:56. Une torche ne fait peur que si le prefab attaché porte une `EffectArea` de type feu sur un collider `character_trigger` (donnée de prefab, partie B).
- `Fireplace` active/désactive `m_enabledObject` (Fireplace.cs:264-298) : un feu éteint n'est pas détecté par la physique.

### A.13 Perception — confirmé

- Champs (BaseAI.cs:40-48) : `m_viewRange = 50`, `m_viewAngle = 90` (**demi-angle**), `m_hearRange = 9999`, `m_mistVision = false`.
- `public static bool CanHearTarget(Transform me, float hearRange, Character target)` (:749-773) : joueurs en vol de debug ou fantôme → `false` ; **en intérieur (`Character.InInterior(me)`, y > 3000) `hearRange = min(12, hearRange)`** ; distance > portée → `false` ; distance < `target.GetNoiseRange()` → `true`.
- `public static bool CanSeeTarget(Transform me, Vector3 eyePoint, float viewRange, float viewAngle, bool alerted, bool mistVision, Character target)` (:780-821) : distance > `viewRange` ou > `viewRange × target.GetStealthFactor()` → `false` ; non alerté et `Vector3.Angle(target − me, me.forward) > viewAngle` → `false` (un alerté voit à 360°) ; raycast œil → œil (centre si la cible est accroupie) contre `m_viewBlockMask` = Default, static_solid, Default_small, piece, terrain, viewblock, vehicle (:222) ; brume si `!mistVision`.
- `public static bool CanSenseTarget(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision, Character target, bool passiveAggresive, bool isTamed)` et versions d'instance (:717-742) : `false` sous la clé globale `PassiveMobs` ; sinon ouïe OU vue.
- Bruit : `Character.m_noiseRange` décroît de 4/s ; `AddNoise(float)` garde le max (via `SEMan.ModifyNoise`) ; non-propriétaires : ZDO `s_noise` (:3797-3828). Sources : marche 15, course 30, accroupi rien (:1850-1858) ; nage 15 ; saut 30 ; roulade 5 ; construction 50 ; attaques ; coups sur `WearNTear`/`Destructible`.
- `Player.GetStealthFactor()` : 1 debout ; accroupi `lerp(0.5+0.5·lumière, 0.2+0.4·lumière, compétence)` (Player.cs:6978-7020).
- `protected Character FindEnemy()` (:1395-1427) : plus proche `Character` ennemi, vivant, non `m_aiSkipTarget`, non endormi, perçu ; sinon joueur le plus proche à 200 m si `HuntPlayer()`. `MonsterAI.UpdateTarget` l'appelle toutes les 2 s (joueur à moins de 50 m) ou 6 s, jamais en attaque (MonsterAI.cs:242-252). Oubli après 30 s sans perception, 60 s sans attaque, ou au-delà de `m_maxChaseDistance` (:330-344).

### A.14 IA, déplacement, pathfinding — confirmé

- `public interface IUpdateAI { bool UpdateAI(float deltaTime); }` ; `public virtual bool BaseAI.UpdateAI(float dt)` (:305-328) : `false` si vue invalide ou non propriétaire (synchronise alors `m_alerted` depuis `s_alert`) ; sinon `UpdateTakeoffLanding`, minuteurs de saut et d'errance, `UpdateRegeneration`, `m_timeSinceHurt += dt`.
- `public override bool MonsterAI.UpdateAI(float dt)` (:347-619) commence par `if (!base.UpdateAI(dt)) return false;`. `AnimalAI` est l'autre sous-classe.
- Ordonnancement : `MonoUpdaters.FixedUpdate` appelle chaque `IUpdateAI` de `BaseAI.Instances` à dt fixe 0,05 s (20 Hz) ; inscription dans `protected virtual void OnEnable()/OnDisable()` (:259-269).
- **`MonsterAI` exige un `Humanoid`** : `m_character as Humanoid` puis déréférencement dans `UpdateTarget` (`m_aiCannotTargetOthers`, :235) et `SelectBestAttack` (:723-736) ; `Start()` appelle `EquipBestWeapon` chez le propriétaire (:172-182).
- `public Pathfinding.AgentType m_pathAgentType = Humanoid` (:60).
- `protected bool MoveTo(float dt, Vector3 point, float dist, bool run)` (:969-1022) : renvoie `true` à l'arrivée, **mais aussi sans chemin ou avec un chemin vide** (indiscernable) ; seuil `max(dist, run ? 1 : 0.5)`.
- `protected bool FindPath(Vector3)` (:946-962, une requête par seconde, résultat réutilisé 5 s si la cible a bougé de moins de 1 m) ; `protected bool FoundPath()` (:964) ; `protected bool HavePath(Vector3)` (:937-944, chemin complet requis).
- `public void MoveTowards(Vector3 dir, bool run)` (:870-902), `protected bool MoveAndAvoid(float dt, Vector3 point, float dist, bool run)` (:1024), `public void StopMoving()` (:932), `protected void LookAt(Vector3)` (:904), `public void LookTowards(Vector3)` (:914), `public bool IsLookingAt(Vector3 point, float minAngle, bool inverted = false)` (:919), `protected void RandomMovement(float dt, Vector3 centerPoint, bool snapToGround = false)` (:496), `protected void RandomMovementArroundPoint(float dt, Vector3 point, float distance, bool run)` (:622), `protected void IdleMovement(float dt)` (:486), `protected void Follow(GameObject go, float dt)` (:425).
- `enum Pathfinding.AgentType` (Pathfinding.cs:27-42) : `Humanoid=1, TrollSize=2, HugeSize=3, HorseSize=4, HumanoidNoSwim=5, HumanoidAvoidWater=6, Fish=7, HumanoidBig=8, BigFish=9, GoblinBruteSize=10, HumanoidBigNoSwim=11, Abomination=12, SeekerQueen=13`.
- Gabarits (hauteur / montée / rayon, :188-259) : Humanoid 1,8 / 0,3 / 0,4 (HumanoidNoSwim et HumanoidAvoidWater en sont des copies) ; HumanoidBig et HumanoidBigNoSwim 2,5 / 0,3 / 0,5 ; TrollSize 7 / 0,6 / 1,0 ; Abomination 5 / 0,6 / 1,5 ; SeekerQueen 7 / 0,6 / 1,5 ; GoblinBruteSize 3,5 / 0,3 / 0,8 ; HugeSize 10 / 0,6 / 2,0 ; HorseSize 2,5 / 0,3 / 0,8 ; Fish et BigFish nage seule. **Le plus petit marcheur est `Humanoid`.** La liste est indexée par la valeur d'enum (:261-264) : pas de type personnalisé sans patch.
- `public bool Pathfinding.GetPath(Vector3 from, Vector3 to, List<Vector3> path, Pathfinding.AgentType agentType, bool requireFullPath = false, bool cleanup = true, bool havePath = false)` (:352-393) ; `public bool HavePath(Vector3 from, Vector3 to, AgentType)` (:321) ; `public bool FindValidPoint(out Vector3 point, Vector3 center, float range, AgentType)` (:326) ; `public static Pathfinding instance`.
- Navmesh construit paresseusement et de façon asynchrone : chaque `GetPath` « pique » les 3×3 tuiles de 32 m autour des deux extrémités, par type d'agent ; reconstruction si plus vieille que 5 s, une à la fois, toutes les 0,1 s ; tuiles de 32 × 6000 × 32 centrées à y = 2500 (y de −500 à 5500). Les premières requêtes d'une zone neuve échouent tant que la tuile n'est pas construite.

### A.15 Champs de `MonsterAI` / `BaseAI` utiles au clonage — corrigé

- `MonsterAI` (MonsterAI.cs:25-102) : `m_alertRange = 9999`, `m_crownFearRange = 9999`, `m_fleeIfHurtWhenTargetCantBeReached = true`, `m_fleeUnreachableSinceAttacking = 30`, `m_fleeUnreachableSinceHurt = 20`, `m_fleeIfNotAlerted`, `m_fleeIfLowHealth = 0`, `m_fleeTimeSinceHurt = 20`, `m_fleeInLava = true`, `m_fleePheromoneMin/Max = 3/8`, `m_circulateWhileCharging`, `m_circulateWhileChargingFlying`, `m_enableHuntPlayer`, `m_attackPlayerObjects = true`, `m_privateAreaTriggerTreshold = 4`, `m_interceptTimeMax/Min`, `m_maxChaseDistance`, `m_minAttackInterval`, `m_circleTargetInterval/Duration/Distance`, `m_sleeping`, `m_wakeupRange = 5`, `m_noiseWakeup`, `m_maxNoiseWakeupRange = 50`, `m_wakeupEffects`, `m_sleepEffects`, `m_wakeUpDelayMin/Max`, `m_fallAsleepDistance`, `m_avoidLand`, `m_consumeItems`, `m_consumeRange = 2`, `m_consumeSearchRange = 5`, `m_consumeSearchInterval = 10`, `Action<ItemDrop> m_onConsumedItem`. Privés : `m_targetCreature`, `m_targetStatic`, `m_lastKnownTargetPos`, `m_timeSinceAttacking`, `m_timeSinceSensedTargetCreature`, `m_updateTargetTimer`, `m_unableToAttackTargetTimer`…
- **`m_targetCreatures` / `m_targetPlayers` n'existent pas** sur `MonsterAI`/`BaseAI` (seul `Turret.m_targetPlayers`, Turret.cs:88).
- `BaseAI` (:40-131) : perception, `m_alertedEffects`, `m_idleSound`, `m_idleSoundInterval = 5`, `m_idleSoundChance = 0.5`, `m_pathAgentType`, `m_moveMinAngle = 10`, `m_smoothMovement = true`, `m_serpentMovement`, `m_jumpInterval`, `m_randomCircleInterval = 2`, `m_randomMoveInterval = 5`, `m_randomMoveRange = 4`, champs de vol (`m_randomFly`…), `m_avoidFire`, `m_afraidOfFire`, `m_avoidWater = true`, `m_avoidLava = true`, `m_skipLavaTargets`, `m_aggravatable`, `m_passiveAggresive`, `m_spawnMessage`, `m_deathMessage`, `m_alertedMessage`, `m_canBeAlerted = true`, `m_fleeRange = 25`, `m_fleeAngle = 45`, `m_fleeInterval = 2`.
- Alerte : `public bool IsAlerted()` (:1652) ; `protected virtual void SetAlerted(bool)` (:1554-1580 ; booléen d'animateur `alert`, ZDO `s_alert`, effets, message) ; override `MonsterAI.SetAlerted` (:919-926) ; `public void Alert()` (:1531).
- Divers : `SetHuntPlayer(bool)` / `HuntPlayer()` (:1483-1498), `IsSleeping()` (:1712), `GetTargetCreature()` (:1766 ; MonsterAI :807), `MonsterAI.GetStaticTarget()` (:812), `SetFollowTarget(GameObject)` (:950), `protected virtual void OnDamaged(float damage, Character attacker)` (MonsterAI :184-190 : réveil, alerte, `SetTarget(attacker)`), branché sur `Character.m_onDamaged` dans `BaseAI.Awake` (:226). `Character.Awake` résout `m_baseAI = GetComponent<BaseAI>()` (Character.cs:669).
- Code vanilla qui exige un `MonsterAI` : SpawnSystem.cs:319/560, Tameable.cs:80/690, Sadle.cs:69, NpcTalk.cs:85, CharacterAnimEvent.cs:127, PrivateArea.cs:533, Trap.cs:303, Character.cs:923/4156.

### A.16 Effets de statut — confirmé

- Chaque `Character` crée son `SEMan` en `Awake` (Character.cs:693) ; `public SEMan GetSEMan()` (:4356) ; mis à jour **chez le propriétaire** seulement (:812-830). Les modificateurs de vitesse s'appliquent à tous les personnages (`ApplyStatusEffectSpeedMods(ref float speed, Vector3 dir)`, :1624-1682, SEMan.cs:69-76).
- `StatusEffect : ScriptableObject` : `string m_name, m_category, m_tooltip`, `Sprite m_icon`, `bool m_hidden`, `float m_ttl` (0 = infini), `EffectList m_startEffects, m_stopEffects`, `m_startMessage/m_stopMessage`, `m_attributes` ; `public virtual void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)` (StatusEffect.cs:278) ; `Clone()`, `CanAdd`, `Setup`, `Stop`, `UpdateStatusEffect`, `IsDone`, `ResetTime`. **Identité = `NameHash()` = `base.name.GetStableHashCode()`** (nom d'objet Unity, pas `m_name`, :355-362).
- `SE_Stats` : `public float m_speedModifier` (:122 ; `speed += baseSpeed × m_speedModifier`, plancher 0, :436-458), `m_swimSpeedModifier`, `m_noiseModifier`, `m_stealthModifier`, `m_jumpModifier`.
- `public StatusEffect SEMan.AddStatusEffect(int nameHash, bool resetTime = false, int itemLevel = 0, float skillLevel = 0f, short variant = -1)` (:137-153) : non-propriétaire → RPC `RPC_AddStatusEffect`, renvoie `null` ; propriétaire → `ObjectDB.instance.GetStatusEffect(nameHash)` (**doit être enregistré dans l'ObjectDB**) ; déjà présent → `ResetTime()` si demandé.
- `public StatusEffect AddStatusEffect(StatusEffect se, bool resetTime = false, …)` (:184-212) : local, sans RPC, clone.
- `public bool RemoveStatusEffect(int nameHash, bool quiet = false)` / `(StatusEffect, bool)` (:214-241) : **pas de RPC** pour un non-propriétaire. `HaveStatusEffect(int)`, `GetStatusEffect(int)`, `GetStatusEffects()`.
- HUD : effets avec `m_icon` non nul et `!m_hidden` (:328-337). Les effets ne sont pas synchronisés en tant qu'objets ; seul `m_attributes` va en ZDO (`s_seAttrib`).
- Jötunn : `new CustomStatusEffect(StatusEffect, bool fixReference)` + `ItemManager.Instance.AddStatusEffect(CustomStatusEffect)` (Jotunn.xml:3570-3594, :5547).

### A.17 Couches physiques et systèmes de touche — corrigé

Il n'existe **pas** de champ `m_hitMask`. Les masques sont construits dans le code :

| Système | Masque | Couches | Requête et triggers |
| --- | --- | --- | --- |
| `Attack` mêlée | `protected static int m_attackMask` (Attack.cs:279, construit :370-376) | Default, static_solid, Default_small, piece, piece_nonsolid, character, character_net, character_ghost, hitbox, character_noenv, vehicle (+ terrain pour `m_attackMaskTerrain`) | `SphereCastNonAlloc`/`RaycastNonAlloc` avec **`QueryTriggerInteraction.Ignore`** (:1275-1287) : jamais de trigger |
| `Attack` caractères | `m_attackMaskCharacters` | character, character_net, character_ghost, hitbox, character_noenv, vehicle | idem |
| `Attack` de zone | `m_attackMask` | idem | `OverlapSphereNonAlloc(..., UseGlobal)` (:1063-1069) |
| `Aoe` | `private int m_rayMask` (Awake, Aoe.cs:211-223) | `m_hitCharacters` → character, character_net, character_ghost ; `m_hitProps` → Default, static_solid, Default_small, piece, hitbox, character_noenv, vehicle ; `m_hitTerrain` → terrain | `OverlapBox/OverlapSphereNonAlloc` sans QTI (UseGlobal, :454) ; `OnTrigger*`/`OnCollision*` si `m_useTriggers` |
| `Projectile` | `private static int s_rayMaskSolids` (:229, :236-239) | Default, static_solid, Default_small, piece, piece_nonsolid, terrain, character, character_net, character_ghost, hitbox, character_noenv, vehicle | `SphereCastAll/RaycastAll` sans QTI (:308), `DoAOE` avec UseGlobal (:466) |

- Défauts `Aoe` : `m_hitCharacters = true`, `m_hitProps = true`, `m_hitTerrain = false`, `m_hitParent = true` (Aoe.cs:117-121).
- `UseGlobal` suit `Physics.queriesHitTriggers`, que le code ne modifie jamais ; aucun appel `IgnoreLayerCollision` dans le code décompilé.
- En mêlée, les touches sont triées par distance et la boucle s'arrête à la première touche acceptée sauf `m_hitThroughWalls` (:1290-1341).
- `hitbox` et `character_noenv` sont touchées par les trois systèmes (par `Aoe` via le groupe `m_hitProps`) et absentes de `Player.m_interactMask` (survol, Player.cs:673), de `BaseAI.m_viewBlockMask`/`m_solidRayMask` et des masques de caméra (GameCamera.cs:562, 650).
- Couches rencontrées dans le code : Default, static_solid, Default_small, piece, piece_nonsolid, terrain, character, character_net, character_ghost, character_noenv, character_trigger, hitbox, vehicle, item, viewblock, blocker, smoke, effect, ghost, Water, WaterVolume, InstanceRenderer.

Routage des dégâts : `public interface IDestructible { void Damage(HitData hit); DestructibleType GetDestructibleType(); }` ; `enum DestructibleType { None=0, Default=1, Tree=2, Character=4, Everything=7 }`. Les trois systèmes passent par `public static GameObject Projectile.FindHitObject(Collider)` (Projectile.cs:883-895) : `GetComponentInParent<IDestructible>()`, sinon l'objet du `attachedRigidbody`, sinon celui du collider ; puis `GetComponent<IDestructible>().Damage(hitData)` avec `hitData.m_hitCollider` renseigné. Un `IDestructible` non-`Character` est blessé par tout attaquant en mêlée et en zone (sauf armes `m_tamedOnly`), toujours par les projectiles (`IsValidTarget`, :537-570), et par `Aoe` **seulement si `m_hitProps`** (Aoe.cs:685). Précédents vanilla : `Raven` (renvoie `DestructibleType.Character`), `HitArea`. `IHitProjectile.OnProjectileHit(...)` peut annuler une touche de projectile.

### A.18 Cibles statiques de l'IA — confirmé

- `MonsterAI.UpdateTarget` (:243-277) ne choisit que des `Character` (`FindEnemy`) et des `StaticTarget`.
- `class StaticTarget : MonoBehaviour { bool m_primaryTarget = false; bool m_randomTarget = true; virtual bool IsPriorityTarget(); virtual bool IsRandomTarget(); Vector3 GetCenter(); List<Collider> GetAllColliders(); Vector3 FindClosestPoint(Vector3); }` ; **`Piece : StaticTarget, IPlaced` en est la seule sous-classe** (Piece.cs:6 ; `m_targetNonPlayerBuilt`, :403-427).
- `protected StaticTarget FindClosestStaticPriorityTarget()` (BaseAI.cs:1316-1340, `OverlapSphere` de rayon `m_viewRange` sur `m_monsterTargetRayMask` = piece, piece_nonsolid, Default, static_solid, Default_small, vehicle) ; `protected StaticTarget FindRandomStaticTarget(float maxDistance)` (:1292-1314, seulement alerté et ciblant un joueur) ; `protected bool CanSeeTarget(StaticTarget)` (:823-854). Conditions : `m_attackPlayerObjects`, non apprivoisé, pas `PassiveMobs`, aggravable ou aggravé.
- `Destructible : MonoBehaviour, IDestructible` n'est **pas** un `StaticTarget` (Destructible.cs:5) : un `Destructible` sans `Piece`/`StaticTarget` n'est jamais choisi comme cible, mais peut subir des dégâts collatéraux (coups et zones visant autre chose). `StaticTarget.GetCenter` divise par le nombre de colliders non-trigger : un `StaticTarget` fait seulement de triggers donne NaN.

### A.19 Effets et audio — confirmé

- `[Serializable] class EffectList { EffectData[] m_effectPrefabs; GameObject[] Create(Vector3 basePos, Quaternion baseRot, Transform baseParent = null, float scale = 1f, int variant = -1, ZDOID gamepadEffectsExclusiveToPlayer = default); bool HasEffects(); }` (EffectList.cs:37) ; `EffectData { GameObject m_prefab; bool m_enabled = true; int m_variant = -1; bool m_attach, m_follow, m_inheritParentRotation, m_inheritParentScale, m_multiplyParentVisualScale, m_randomRotation, m_scale; string m_childTransform; }`. `Create` fait un simple `Object.Instantiate` local ; un prefab portant un `ZNetView` devient un objet réseau (ZDO créé dans `ZNetView.Awake`).
- `ZSFX : MonoBehaviour` exige un `AudioSource` sur le même objet (NRE sinon). Champs : `m_playOnAwake = true`, `AudioClip[] m_audioClips`, `int m_maxConcurrentSources`, `m_ignoreConcurrencyDistance`, `m_minPitch/m_maxPitch`, `m_minVol/m_maxVol`, fondus, `m_randomPan`, délais, `m_distanceReverb`, sous-titres, `[HideInInspector] int m_hash`. Méthodes : `Play()`, `Stop()`, `FadeOut()`, `IsPlaying()`, `SetVolumeModifier(float)`, `SetPitchModifier(float)`, `GenerateHash()`, `GetConcurrencyDistance()` = `max(1, audioSource.minDistance)`.
- `AudioMan.instance.RequestPlaySound(ZSFX)` (AudioMan.cs:738-765) : `m_maxConcurrentSources ≤ 0` → toujours joué ; sinon refus au-delà de N sons de même `m_hash` dans `m_concurrencyThreshold` (0,2 s) et dans la distance de concurrence (tampon circulaire de 512). Boucles : les N plus proches de la caméra restent audibles (0 → `m_forcedMaxConcurrentLoops = 5`, < 0 → illimité). `m_hash` est figé dans les prefabs vanilla ; aucun code n'appelle `GenerateHash` à l'exécution.
- `TimedDestruction { float m_timeout = 1; bool m_triggerOnAwake; void Trigger(); void Trigger(float); }`.

### A.20 Rendu et shaders — exécution

- Aucun nom `Custom/...` dans le code. Seuls `Shader.Find` : `Hidden/CameraHeatDistort`, `Hidden/ShieldDomePass`, `Hidden/Amplify Occlusion/{Occlusion,Blur,Apply}`.
- Propriétés référencées : `ShaderProps._MainTex, _Color, _EmissionColor, _Style, _RippleDistance, _ValueNoise` (assembly_utils/ShaderProps.cs) ; teinte de niveau `_Hue`, `_Saturation`, `_Value` (+ `_EmissionColor`) sur `sharedMaterials[0]` (LevelEffects.cs:72-78, Ragdoll.cs:42-47) ; `VisEquipment` : `_SkinColor, _MainTex, _SkinBumpMap, _ChestTex, _ChestBumpMap, _ChestMetal, _LegsTex, _LegsBumpMap, _LegsMetal, _SnowCover`. **`_BumpMap` n'apparaît nulle part dans le code.**
- Précédent d'instancing : `InstanceRenderer` appelle `Graphics.DrawMeshInstanced(mesh, 0, material, Matrix4x4[], count, null, shadowCasting, receiveShadows: true, LayerMask.NameToLayer("InstanceRenderer"))` avec son propre test de frustum (`Utils.InsideMainCamera`). Unity 6000.0.75 : `Graphics.RenderMeshInstanced` est disponible.
- `MaterialMan.instance.SetValue(GameObject, int propertyId, T value)` / `ResetValue` (blocs de propriétés par objet ; sans effet sur les tracés `Graphics.*`).
- Jötunn : `PrefabManager.Cache.GetPrefab<T>(string name)` trouve tout objet chargé, shaders compris (Jotunn.xml:6725-6736).

### A.21 HUD et listes de personnages — partiel

- `public static List<Character> Character.GetAllCharacters()` (:4025, liste vivante `s_characters`) ; `public static void GetCharactersInRange(Vector3 point, float radius, List<Character> characters)` (:4013) ; `public static List<Player> Player.GetAllPlayers()` (Player.cs:5562, liste vivante, joueurs distants compris, aussi sur un serveur dédié) ; `GetClosestPlayer(Vector3, float)`, `GetPlayersInRange(Vector3, float, List<Player>)`, `IsPlayerInRange(Vector3, float)`, `GetPlayersInRangeXZ`, `GetRandomPlayer()`.
- `EnemyHud` (:54-58) : `m_maxShowDistance = 10`, `m_maxShowDistanceBoss = 100`, `m_hoverShowDuration = 60` (valeurs de script). La barre d'une créature ordinaire n'apparaît **qu'après qu'elle a été survolée par le viseur** (`Player.GetHoverCreature()`), puis 60 s (:188). Étoiles : enfants `level_2` / `level_3`. Nom : `Localize(c.GetHoverName())` à chaque frame. Ancrage `GetTopPoint()` = centre du collider + moitié de sa hauteur (Character.cs:2168). `GetBaseAI().HaveTarget()/IsAlerted()` appelés sans test (:203-206) : un `BaseAI` est requis.
- Survol : `private void Player.FindHoverObject(out GameObject, out Character)` (:4261-4300), premier hit de `m_interactMask` seulement.

### A.22 Prérequis d'un clone de créature — partiel

Utilisés sans garde par `Character.Awake` et la suite (Character.cs:660-693) :

1. `CapsuleCollider` à la racine (`m_collider` ; `UpdateLayer` bascule character ↔ character_net si la couche de départ est l'une des deux, :855-868).
2. `Rigidbody` à la racine.
3. `ZNetView`.
4. `Animator` trouvé par `GetComponentInChildren<Animator>()` (ignore les objets inactifs) : **ne jamais désactiver l'objet `Visual` ni celui de l'Animator**, désactiver seulement les `Renderer`.
5. Enfant nommé exactement `Visual`.
6. `ZSyncAnimation` à la racine.
7. `CharacterAnimEvent` sur l'objet de l'Animator (utilisé par Attack.cs:452, :1733 ; root motion via `OnAnimatorMove`).
8. `Character.m_eye` (perception, `GetLookDir`).
9. Un `BaseAI`/`MonsterAI` (EnemyHud).

Optionnels ou gardés : `LODGroup` sur `Visual`, `VisEquipment`, `FootStep`, `m_head`. Les attaques ne partent que d'événements d'animation (`CharacterAnimEvent.Hit/OnAttackTrigger`) ; le jeu ne règle jamais `Animator.cullingMode` (valeur du prefab). À la mort, `m_deathEffects.Create(...)` instancie le ragdoll ; `Ragdoll.Setup` teinte `m_mainModel` (`public Renderer`).

### A.23 Donjons des mines — corrigé

- `enum Room.Theme` (Room.cs:7-27), **masque de bits** : `None=0, Crypt=1, SunkenCrypt=2, Cave=4, ForestCrypt=8, GoblinCamp=0x10, MeadowsVillage=0x20, MeadowsFarm=0x40, DvergerTown=0x80, DvergerBoss=0x100, ForestCryptHildir=0x200, CaveHildir=0x400, PlainsFortHildir=0x800, AshlandRuins=0x1000, FortressRuins=0x2000, Hole=0x4000, NorthVillage=0x10000, MorkHalla=0x20000`. Orthographe « Dverger » dans l'enum, « Dvergr » dans les noms de prefabs.
- `Room : MonoBehaviour` (:31-62) : `Vector3Int m_size = (8,4,8)`, `Theme m_theme`, `bool m_enabled, m_entrance, m_endCap, m_divider, m_faceCenter, m_perimeter`, `int m_endCapPrio, m_minPlaceOrder`, `float m_weight`, `[NonSerialized] int m_placeOrder, m_seed`, `private RoomConnection[] m_roomConnections` ; `int GetHash()` (`Utils.GetPrefabName(gameObject).GetStableHashCode()`), `GetConnections()`, `GetEntrance()`. Boîte = OBB centrée sur `transform.position`, tournée par `transform.rotation`, d'étendue `m_size` (DungeonGenerator.cs:967-1005, gizmo Room.cs:72-78).
- `RoomConnection` : `string m_type`, `bool m_entrance, m_allowDoor, m_doorOnlyIfOtherAlsoAllowsDoor`, `[NonSerialized] int m_placeOrder`, `bool TestContact(RoomConnection)`.
- `DungeonDB` : `static DungeonDB instance`, `static List<RoomData> GetRooms()`, `RoomData GetRoom(int hash)` ; `RoomData { SoftReference<GameObject> m_prefab; bool m_enabled; Room.Theme m_theme; Room RoomInPrefab { get; } (après m_prefab.Load()); int Hash; }`.
- `DungeonGenerator` : `public void Generate(ZoneSystem.SpawnMode mode)` (:172) → `public void Generate(int seed, ZoneSystem.SpawnMode mode)` (:200) ; `public int GetSeed()` ; `private void Awake()` (:135 : `m_nview = GetComponent<ZNetView>(); Load(); if (m_loadedRooms.Length != 0) LoadRoomPrefabsAsync();`) ; **pas de `Start`** ; `private void Load()` (:470), `private void Save()` (:438), `private void Spawn()` (:280), `private Room PlaceRoom(DungeonDB.RoomData, Vector3, Quaternion, RoomConnection, ZoneSystem.SpawnMode)` (:804, salles parentées à `base.transform`, :894). Champs : `Room.Theme m_themes`, `Algorithm m_algorithm`, `m_maxRooms`, `m_minRooms`, `Vector3 m_zoneCenter, m_zoneSize`, `[HideInInspector] Vector3 m_originalPosition`, `int m_generatedSeed`, `static int m_forceSeed`, `private ZNetView m_nview`, `private RoomPlacementData[] m_loadedRooms`.
- **`m_placedRooms` est une `private static List<Room>` de travail**, vidée au début et à la fin de `Generate` (:116, :221, :244) : vide dans un postfix. En mode Ghost les salles sont détruites avant la fin (:235-241). `Load()` ne la remplit jamais.
- Source durable : ZDO du générateur, clé `roomData` (`ZDOVars.s_roomData`) : `int32` nombre, puis par salle `int32` hash, 3 `float` position, 3 `float` angles d'Euler (:438-468 ; Utils.cs:1111-1136).
- Quand : `Generate` tourne **une seule fois** dans la vie du monde, sur le serveur/l'hôte, à la génération de la zone (Full si locale, **Ghost** autour des pairs distants, toujours le cas sur un serveur dédié). `Load` tourne depuis `Awake` à **chaque** instanciation sur **chaque** pair ; les salles sont ensuite créées en asynchrone en mode Client (:250-289) pendant que `ZoneSystem.SetLoadingInZone` bloque la zone. En mode Client, les enfants `ZNetView` des salles (spawners, coffres…) restent des copies **inactives** (:888-894) ; les vrais sont des ZDO racines créés à la génération (:832-869).
- La racine du donjon est l'objet portant le `DungeonGenerator`, avec son propre `ZNetView`/ZDO. `Location.m_generator` d'une `Location` vivante pointe vers une **copie inactive** (ZoneSystem.cs:2547-2552).

### A.24 Emplacements et téléportation — confirmé

- Noms d'emplacements (manifeste `C:\ValheimDev\valheim_Data\StreamingAssets\SoftRef\manifest`, absents du code) : `Mistlands_DvergrTownEntrance1`, `Mistlands_DvergrTownEntrance2`, `Mistlands_DvergrBossEntrance1`. Salles `dvergr_*` (mines) et `dvergr_bossroom_*` / `dvergr_new_bossroom_*` (citadelle).
- `public bool ZoneSystem.FindClosestLocation(string name, Vector3 point, out ZoneSystem.LocationInstance closest)` (:2943) ; `public bool FindLocations(string name, ref List<LocationInstance>)` (:2964) ; `struct LocationInstance { ZoneLocation m_location; Vector3 m_position; bool m_placed; }` ; `List<ZoneLocation> m_locations` ; `Dictionary<Vector2s, LocationInstance> m_locationInstances` (**serveur uniquement**) ; `enum SpawnMode { Full, Client, Ghost }` ; `static Vector2s GetZone(Vector3)`, `static Vector3 GetZonePos(Vector2s)` (les zones sont des **`Vector2s`**) ; `bool IsZoneLoaded(Vector3 | Vector2s)`, `bool IsActiveAreaLoaded()`, `bool FindFloor(Vector3, out float)`, `bool TestSpawnLocation(string, Vector3, bool disableSave = true)`.
- `LocationProxy` : ZDO `location` = hash du nom, `seed`. `Location` : `m_hasInterior`, `m_interiorTransform`, `m_generator`, `m_blockSpawnGroups`, `static Location GetZoneLocation(Vector3 | Vector2s)`, `static Location GetLocation(Vector3, bool checkDungeons = true)`.
- `Teleport : MonoBehaviour, Hoverable, Interactable { Teleport m_targetPoint; … }` ; `public virtual bool Character.TeleportTo(Vector3 pos, Quaternion rot, bool distantTeleport)` ; `Player.TeleportTo` (Player.cs:5919) : échec si non propriétaire (RPC), déjà en cours ou `m_teleportCooldown < 2 s` ; déplacement après 2 s, fin quand `ZNetScene.IsAreaReady` et `FindFloor` réussissent ; `distantTeleport = true` attend 8-15 s puis retombe sur la hauteur solide. `public static bool Character.InInterior(Vector3)` : **y > 3000**.

### A.25 Réseau, propriété, persistance — corrigé

- `ZNetView` : `bool m_persistent, m_distant, m_syncInitialScale`, `ZDO.ObjectType m_type`, `private bool m_ghost`, `static long Everybody = 0` ; `ZDO GetZDO()`, `bool IsValid()`, `bool IsOwner()`, `bool HasOwner()`, `void ClaimOwnership()`, `void Destroy()` ; `Register(string, Action<long>)` … `Register<T,U,V,B>` (jusqu'à 6 paramètres, `Dictionary.Add` : un doublon lève une exception), `Unregister(string)` ; `InvokeRPC(long targetID, string method, params object[])`, `InvokeRPC(string method, params object[])` (vers le **propriétaire**) ; `static StartGhostInit()/FinishGhostInit()`.
- `ZDO` : `Set(string|int, int/float/long/bool/string/Vector3/Quaternion/byte[])`, `Set(string, ZDOID)` (deux `long`, clés `_u`/`_i`), **pas de setter `byte`**, **aucun contrôle de propriété** ; `GetInt/GetFloat/GetLong/GetBool/GetString/GetVec3/GetQuaternion(name|hash, défaut)` et variantes `out` ; `byte[] GetByteArray(string|int, byte[] def = null)` ; `ZDOID GetZDOID(string)` ; `Remove*(int)` ; `GetOwner()`, `IsOwner()`, `SetOwner(long)`, `bool Persistent`, `ObjectType Type`, `GetPosition()`, `GetPrefab()`, `GetSector()`, `ZDOID m_uid`.
- `ZDOMan` : `static ZDOMan instance`, `static long GetSessionID()`, `ZDO CreateNewZDO(Vector3, int prefabHash)` (propriétaire = créateur), `GetZDO(ZDOID)`, `DestroyZDO(ZDO)`, `FindSectorObjects(Vector2s sector, SimulationDistance, List<ZDO>, List<ZDO> distant = null)`, `GetAllZDOsWithPrefabIterative(string, List<ZDO>, ref int)`, `ForceSendZDO(ZDOID)`.
- **Propriété** (ZDOMan.cs:938-993) : toutes les 2 s, le serveur seul parcourt les ZDO **persistants** proches de sa position puis de celle de chaque pair ; un ZDO hors de la zone active de son propriétaire est libéré ; un ZDO sans propriétaire (ou dont le propriétaire ne le couvre plus) et dans la zone active d'un pair lui revient. La propriété est **collante** (pas « le plus proche ») ; le test de zone est en 2D (ignore le décalage +5000 des intérieurs) ; le serveur passe en premier. `ClaimOwnership` force la propriété.
- **Persistance** : seuls les ZDO `Persistent` sont sauvegardés (ZDOMan.cs:1636-1641) ; un ZDO non persistant est détruit au déchargement par son propriétaire ou à la déconnexion de celui-ci, et sa propriété ne migre jamais. **Un serveur qui rencontre un hash de prefab inconnu prend la propriété du ZDO et le détruit** (« Destroyed invalid prefab ZDO », ZNetScene.cs:229-235, 268-274).
- `ZNetScene` : `GetPrefab(int|string)`, `HasPrefab(int)`, `FindInstance(ZDO)`, `FindInstance(ZDOID)`, `HaveInstance(ZDO)`, `Destroy(GameObject)`, `IsAreaReady(Vector3)`, `static InActiveArea(Vector3, Vector3|Vector2s)`.
- `ZRoutedRpc` : `static ZRoutedRpc instance` (recréé à chaque `ZNet.Awake`), `const long Everybody = 0`, `InvokeRoutedRPC(long targetPeerID, string method, params object[])`, `InvokeRoutedRPC(string method, params object[])` (vers le serveur), `InvokeRoutedRPC(long targetPeerID, ZDOID targetZDO, string method, params object[])` ; `Register…` (doublon → exception). Cible soi-même ou `Everybody` → traité aussi localement, de façon synchrone. Un RPC ciblant une ZDO est **abandonné en silence** si le pair n'a pas d'instance. Types sérialisables (`ZRpc.Serialize`) : int, uint, long, float, double, bool, string, ZPackage, List<string>, Vector3, Quaternion, ZDOID, HitData, ISerializableParameter. **`byte[]` n'est pas supporté** (ignoré en silence).

### A.26 Sauvegarde et serveur — confirmé

- `private void ZNet.SaveWorld(bool sync)` (:1737-1760) ; `public void Save(bool sync, bool saveOtherPlayerProfiles = false, bool waitForNextFrame = false)` (serveur avec monde seulement) ; `public bool IsSaving()` ; `public static Action WorldSaveStarted, WorldSaveFinished` (délégués statiques ; `WorldSaveStarted` est invoqué avant `ZDOMan.PrepareSave`, donc côté serveur).
- `public bool IsServer()` (vrai pour hôte et solo) ; `public static bool IsSinglePlayer` (= serveur non ouvert) ; **`IsDedicated()` renvoie `false` dans l'assembly client et `true` dans celle du serveur dédié** (vérifié sur `C:\ValheimDevServer\valheim_server_Data\Managed\assembly_valheim.dll`) ; `GetReferencePosition()`, `GetPeers()`, `GetPeer(long)`.

### A.27 Spawners — confirmé

- `CreatureSpawner` : objet réseau racine créé une seule fois à la génération (Full/Ghost), recréé ensuite depuis sa ZDO ; `private static List<CreatureSpawner> m_creatureSpawners` ; `GameObject m_creaturePrefab`, `m_respawnTimeMinuts`, `m_triggerDistance`, `m_spawnGroupID`, `m_spawnGroupRadius`, `m_spawnInterval` ; logique chez le propriétaire seulement. Les salles contiennent en plus des **copies inactives** aux bonnes positions.
- `SpawnArea` : `m_spawnRadius = 2`, `m_nearRadius = 10`, `m_farRadius`.
- `SpawnSystem` (biome) ne peut pas faire apparaître dans un intérieur : `IsSpawnPointGood` ramène le point au sol du terrain (SpawnSystem.cs:332-357).

### A.28 `Destructible` (base du nid) — confirmé

- Champs : `Action m_onDestroyed, m_onDamaged` (sans argument, chez le propriétaire), `DestructibleType m_destructibleType = Default`, `float m_health = 1`, `HitData.DamageModifiers m_damages`, `float m_minDamageTreshold`, `int m_minToolTier`, `m_hitNoise`, `m_destroyNoise`, `bool m_triggerPrivateArea`, `float m_ttl`, `GameObject m_spawnWhenDamaged, m_spawnWhenDestroyed`, `EffectList m_destroyedEffect, m_hitEffect, m_hitEffectBig, m_hitEffectBuildup`, `bool m_autoCreateFragments`.
- `Awake` enregistre `RPC_Damage` si une ZDO existe ; `public void Damage(HitData)` (:92-98) **ignore les coups de la première frame** (`m_firstFrame`, levé en `Start`) ; `private void RPC_Damage(long, HitData)` (:100-171, propriétaire) : PV lus en ZDO `health`, défaut `m_health × (1 + Game.m_worldLevel × Game.instance.m_worldLevelMineHPMultiplier)`, résistances, palier d'outil, `m_onDamaged()`, `Destroy(hit)` si PV ≤ 0 ; `public void Destroy(HitData hit = null)` : `m_destroyedEffect` (**propriétaire seulement**), `m_spawnWhenDestroyed`, `m_onDestroyed()`, `ZNetScene.Destroy` ; `DestroyNow()` (pour `m_ttl`).

### A.29 Harnais de dev : démarrage, console, joueur, temps — confirmé

**Chemin vanilla « Démarrer » (FejdStartup → Game)** : `OnStartGame()` (:925) → sélection du personnage → `OnCharacterStart()` (:2903) → `private void SelectCharacter(string fileName, FileHelpers.FileSource fileSource)` (:2897, `PlatformPrefs "profile"` + `Game.SetProfile`) → `ShowStartGame()` → `OnWorldStart()` (:1635-1723) : `SaveSystem.CanSaveToCloudStorage`, garde `m_world`/`m_startingWorld`, `m_dataError == None`, choix du backend (`PlayFab` si crossplay), puis `ZNet.m_onlineBackend = backend; ZSteamMatchmaking.instance.StopServerListing(); m_startingWorld = true; ZNet.SetServer(true, open, public, m_world.m_name, password, m_world); ZNet.ResetServerHost(); TransitionToMainScene();`. `private void TransitionToMainScene()` (:2928) → `Invoke("LoadMainSceneIfBackendSelected", m_instantStart ? 0 : 1.5f)` → `LoadMainScene()` (:2957 : `SystemResourceManager.FastLoadScene(m_mainScene)`). Scène principale : `Game.Awake` charge le profil ; `ZNet.Start` → `ServerLoadWorld()` ; `Game.FixedUpdate` → `RequestRespawn(0)` → `UpdateRespawn` → `SpawnPlayer` → `Player.SetLocalPlayer`.

- `FejdStartup` : `static FejdStartup instance`, `private void Awake()` (:307, arguments, Steam, `Game.Unpause()`, `Time.timeScale = 1`), `private void Start()` (:427, point d'accroche de QuickStart ; relancé après chaque déconnexion), `static string ServerPassword { get; private set; }` (:303), `private World m_world`, `private bool m_startingWorld`, `private bool m_instantStart`, `private ServerJoinData m_joinServer`, `private List<PlayerProfile> m_profiles`, `private int m_profileIndex`, `public SceneReference m_mainScene` (type de SoftReferenceableAssets, non référencé par le csproj), `private void SetSelectedProfile(string)` (:2763), `private World FindWorld(string)` (:1296), `public void JoinServer()` (:1841), `public void SetServerToJoin(ServerJoinData)` (:3074), `private IEnumerator TryPlayIntroCinematic()` (:470 ; la vidéo d'intro du premier démarrage appelle `Game.Pause()` via `CinematicsManager.Play`).
- `PlayerProfile(string filename = null, FileHelpers.FileSource fileSource = FileHelpers.FileSource.Auto)` (`Auto` peut devenir Cloud) ; `Load()`, `Save()` (→ `characters_local/<nom>.fch` en Local), `SetName(string)`, `public bool m_firstSpawn = true` (vrai → intro Valkyrie), `static bool HaveProfile(string)`, `static bool s_bypassCheatChecks`.
- `World(string name, string seed)` (seed vide → 0) ; `static World GetCreateWorld(string, FileSource)` crée avec un seed **aléatoire** ; `static bool HaveWorld(string)`, `static string GenerateSeed()`, `void SaveWorldFWLData(DateTime)`, `m_fileSource`, `m_needsDB`, `m_dataError`. Chemin vanilla de création : `new World(name, seed)`, `m_fileSource = Local`, `m_needsDB = false`, `SaveSystem.SetSaveNumber(0u)`, `SaveWorldFWLData(DateTime.Now)`. `SaveSystem.GetWorldList()`, `GetAllPlayerProfiles()`, `TryGetSaveByName(...)`, `InvalidateCache(...)`.
- `ZNet` : `static void SetServer(bool server, bool openServer, bool publicServer, string serverName, string password, World world)`, `static void ResetServerHost()`, `static bool HasServerHost()`, `SetServerHost(string host, int port, OnlineBackendType backend)`, `static OnlineBackendType m_onlineBackend` (Steamworks par défaut), `static Action s_onZNetStart`, `static ConnectionStatus GetConnectionStatus()`, `int GetPeerConnections()`, `void RemoteCommand(string)`.
- Rejoindre : `new ServerJoinData(new ServerJoinDataDedicated(string host, ushort port))` (ou `ServerJoinDataDedicated("host:port")`, port par défaut 2456) → `SetServerToJoin` → `JoinServer()` (`ZNet.SetServer(false, …)`, IP littérale résolue de façon synchrone, repli Steamworks, sondage de `HasServerHost()` jusqu'à 12,5 s). Mot de passe : `ZNet.RPC_ClientHandshake` soumet `FejdStartup.ServerPassword` s'il est non nul. `ServerJoinData` peut nécessiter `Splatform.dll` en référence (CS0012).
- Scène : chargée par `SceneReference`, jamais par nom (`SceneManager.LoadScene("main")` à proscrire) ; les noms `start`/`main` sont à confirmer à l'exécution.
- Console : `Console : Terminal`, `static Console.instance` ; `public void TryRunCommand(string text, bool silentFail = false, bool skipAllowedCheck = false)` (Terminal.cs:2786) ; toute sortie passe par `public void AddString(string text)` (:3069), qui alimente `protected List<string> m_chatBuffer` (plafond 300) ; `Chat.instance` est aussi un `Terminal`. Une commande de triche sans `Achievements.IsCheatedAtAll()` (vrai si `Game.isModded`) affiche `$achievements_confirm_cheat` et ne s'exécute pas.
- Triche : `public static bool Terminal.m_cheat` (`devcommands` le bascule et n'est pas idempotent) ; `public bool IsCheatsEnabled()` = `m_cheat && ZNet.instance.IsServer()` (**jamais vrai sur un client connecté à un serveur dédié**) ; `public static bool Player.m_debugMode` (remis à `false` dans `FejdStartup.Start`) ; `Player.SetGodMode(bool)`, `SetGhostMode(bool)`, `ToggleDebugFly()`, `IsDebugFlying()`. Dieu/fantôme bornent les PV à 1 ; **le vol de debug annule tout dégât** et rend le joueur invisible pour l'IA. Une créature touchée par un joueur en dieu/fantôme/vol voit ses drops marqués `m_cheated` (sauf clé `bypasscheatchecks`).
- Joueur : `static Player m_localPlayer`, `TeleportTo`, `IsTeleporting()`, `AddUniqueKeyValue(string, string)`, `GetStamina()`, `GetMaxStamina()`, `AddStamina(float)`, `UseStamina(float)`, `private float m_stamina`, `Humanoid.GetInventory()`, `StartAttack(Character target, bool secondaryAttack)`, `UnequipAllItems()`, `Player.IsEquipActionQueued(ItemData)`, `Inventory.AddItem(string name, int stack, int quality, int variant, long crafterID, string crafterName, bool cheated, bool pickedUp = false)` (durabilité mise au max : la régler ensuite), `Inventory.RemoveAll()`, `Character.Heal(float, bool showText = true)`, `SetLookDir(Vector3, float = 0)`.
- Prêt : `Game.instance`, `public static event Action Game.m_playerInitialSpawn`, `Game.WaitingForRespawn()`, `Game.InIntro(bool includeQueued = false)`, `Game.SkipIntro()`, `ZoneSystem.instance.IsActiveAreaLoaded()`, `ZNetScene.instance.IsAreaReady(Vector3)`.
- Caméra : `GameCamera.instance`, `ToggleFreeFly()`, `static InFreeFly()`, `SetFreeFlySmoothness(float)`, privés `m_freeFlyYaw/Pitch/Vel/Target/Lockon`, `m_distance` ; `UpdateFreeFly` réécrit la pose chaque frame. `static GameCamera.ScreenShot()` est asynchrone ; `ScreenCapture.CaptureScreenshotAsTexture()` après `WaitForEndOfFrame` est synchrone. HUD : `Hud.instance.m_userHidden`.
- Temps : **`Game.UpdatePause()` réécrit `Time.timeScale` à chaque frame** (`IsPaused() ? 0 : (peers > 0 ? 1 : m_timeScale)`, Game.cs:1186) ; le setter supporté est `public static void Game.FadeTimeScale(float timeScale = 0f, float transitionSec = 0f)` (borné 0-100, ignoré sauf si `timeScale == 1` ou `CanPause()`, c'est-à-dire solo sans pair) ; `Game.Pause()/Unpause()/IsPaused()` ; le menu Échap met en pause. `Time.fixedDeltaTime` n'est jamais modifié.

---

## B. Données d'exécution

Source : dump `mw_dump` du 2026-10-24 sur Valheim **1.0.20** (Unity 6000.0.75f1), sortie brute dans [`vanilla-reference-dump.md`](vanilla-reference-dump.md) ; `L..` = numéro de ligne de ce fichier. Régénération : `tools/run-game.ps1`, attendre `ready`, puis `python tools/vh.py dump docs/vanilla-reference-dump.md` (**toujours passer le chemin** : sans argument le pont écrirait dans ce fichier-ci). Le code est dans `src/MistWorms/Dev/VanillaDump.cs` (build Debug). Les décisions qui en découlent sont dans [`decisions.md`](decisions.md) et les noms dans `src/MistWorms/VanillaNames.cs`.

### B.1 Créatures de référence

`SeekerSoldier` n'existe pas (« missing from ZNetScene », L304) : le soldat fouineur est **`SeekerBrute`** (`$enemy_seekerbrute`).

| Prefab | Nom | PV | Faction | Vue | Demi-angle | Ouïe | Alerte | Agent de chemin | Idle (intervalle, chance) | Réf. |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Tick` | `$enemy_tick` | 50 | MistlandsMonsters | 30 | 90 | 9999 | 20 | `HumanoidNoSwim` | `sfx_tick_idle` (6 s, 0,5) | L16, L27 |
| `Seeker` | `$enemy_seeker` | 200 | MistlandsMonsters | 30 | 120 | 9999 | 30 | `Humanoid` | `sfx_seeker_idle`, `sfx_seeker_overlay` | L12, L23 |
| `SeekerBrood` | `$enemy_babyseeker` | 20 | MistlandsMonsters | 30 | 120 | 9999 | 30 | `Humanoid` | `sfx_baby_seeker_idle` | L13, L24 |
| `SeekerBrute` | `$enemy_seekerbrute` | 1500 | MistlandsMonsters | 30 | 90 | 9999 | 30 | `HumanoidBig` | `sfx_seeker_brute_idle` (10 s) | L14, L25 |

Toutes : `AfraidOfFire` = False, `AvoidFire` = False, `AttackPlayerObjects` = True, IA `MonsterAI`. Vitesse / marche / course de la tique : 1 / 5 / 8 (L16).

Résistances (L33-38) : `Tick` = blunt Normal, slash Normal, **pierce Weak**, chop / pickaxe Ignore, spirit Immune. `Seeker`, `SeekerBrood`, `SeekerBrute` = blunt / slash / pierce **Resistant**, chop / pickaxe Ignore, spirit Immune.

Taille et colliders (AABB de maillage en repère racine ; `Renderer.bounds` d'un prefab est vide, voir B.9) :

| Prefab | AABB (x, y, z) m | Collider racine | Réf. |
| --- | --- | --- | --- |
| `Tick` | 1,26 x 0,91 x 1,25 | capsule r 0,6, couche `character` | L51 |
| `SeekerBrood` | 1,20 x 1,20 x 2,07 | capsule r 0,6 h 1, couche `character` | L48 |
| `Seeker` | 3,2 x 3,2 x 5,5 | capsule r 0,8 h 1 (+ capsule r 0,6 h 2,45 sur `character_noenv`) | L47 |
| `SeekerBrute` | 4,40 x 2,67 x 4,91 | capsule r 1,2 h 2,2 (+ sphères et capsule `character_noenv`) | L49 |

Composants de `SeekerBrood` (L138) : `CapsuleCollider`, `Rigidbody`, `Humanoid`, `ZNetView`, `ZSyncTransform`, `ZSyncAnimation`, `MonsterAI`, `CharacterDrop`, `FootStep` : base de clone retenue. Drops : `Tick` = `GiantBloodSack` x1 (100 %) + `TrophyTick` (5 %) (L269-270) ; `SeekerBrood` = `RoyalJelly` (50 %). Attaque de la tique `tick_attack_attach` : pierce 50, portée 1, hit `fx_TickBloodHit` + `sfx_tick_attack_drain` (L263).

### B.2 Effets (sfx / vfx / fx)

791 `sfx_`, 487 `vfx_`, 281 `fx_` (L329-333). Effets utiles (confirmés par les tables d'effets des créatures) :

| Rôle | Prefab | Réf. |
| --- | --- | --- |
| Mort de la tique | `fx_tick_death` | L279 |
| Touche de la tique (sang) | `fx_TickBloodHit` | L263, L318 |
| Tique : idle / attaque (drain) / attaque (saut) / blessé / alerte | `sfx_tick_idle`, `sfx_tick_attack_drain`, `sfx_tick_attack_jump`, `sfx_tick_hurt`, `sfx_tick_alerted` | L263, L276, L280-281, L445-446 |
| Mort / blessé du jeune fouineur | `fx_babyseeker_death`, `fx_babyseeker_hurt` | L156, L159 |
| Idle / alerte du jeune fouineur | `sfx_baby_seeker_idle` (**à confirmer**, absent de la liste `sfx_*`, L514), `sfx_baby_seeker_alerted` | L160-161 |
| Destruction d'un œuf | `fx_egg_splash` | L355, L552 |

Les noms provisoires de `VanillaNames` `sfx_tick_attack`, `sfx_seekerbrood_idle`, `sfx_seekerbrood_attack`, `sfx_seekeregg_destroyed`, `vfx_tick_death`, `vfx_seekeregg_destroyed` ne sont **pas** dans ces tables : à remplacer par les noms ci-dessus.

### B.3 Candidats de nid (L534-553)

| Prefab | PV | Composants | `m_destroyedEffect` | Drop |
| --- | --- | --- | --- | --- |
| `SeekerEgg` | 1 | `Destructible`, `EggHatch`, `DropOnDestroyed`, `LODGroup` ; spirit Ignore | `fx_egg_splash` | `SeekerBrood` (50 %) |
| `SeekerEgg_alwayshatch` | 1 | idem | `fx_egg_splash` | `SeekerBrood` (100 %) |
| `CreepProp_egg_hanging01` | 1 | `Destructible`, `DropOnDestroyed`, tout Normal | `fx_egg_splash` | `SeekerBrood` (50 %), apparaît `CreepProp_egg_hanging02` |

`SeekerEgg` : AABB 0,53 x 1,16 x 0,53 m.

### B.4 Objets

1519 objets, 481 recettes, 356 avec durabilité (L558).

| Objet | Donnée | Réf. |
| --- | --- | --- |
| `Demister` (`$item_demister`) | `Utility`, **UseDurability False**, MaxDurability 100, DurabilityPerLevel 50 ; recette `piece_workbench` niv. 1 : Silver x1 + Wisp x1 | L562-565 |
| `Entrails` (`$item_entrails`) | `Material`, poids 0,5, pile 50 | L571 |
| `Bloodbag` (`$item_bloodbag`) | `Material`, poids 0,5, pile 50 | L572 |
| `FishingBait` | base, pile 100, pas de recette | L578 |
| `FishingBaitAshlands` / `Cave` / `DeepNorth` / `Forest` / `Mistlands` / `Ocean` / `Plains` / `Swamp` | `FishingBait` x20 + 1 trophée (`TrophyCharredMelee`, `Fenring`, `Hatchling`, `FrostTroll`, `Lox`, `Serpent`, `Goblin`, `Abomination`), `piece_preptable` niv. 1, sortie 20 | L579-586 |
| `ArmorPaddedCuirass`, `ArmorPaddedGreaves`, `HelmetPadded` | durabilité 1000 / 1200 / 1400 / 1600 (+200 par niveau), armure 26 ; forge niv. 2 (casque niv. 1) ; Iron + LinenThread + `Upgrader4Armor` | section 4 |
| `SwordBronze` | slash 35, durabilité 200-350 | section 4 |

### B.5 Salles et emplacements des mines

`Room.Theme` est un masque : `DvergerTown` = 128 (**48 salles**), `DvergerBoss` = 256 (**18 salles**) (L626-643). Emplacements (L675-677) :

| Emplacement | Générateur | Thème | Salles | Zone |
| --- | --- | --- | --- | --- |
| `Mistlands_DvergrTownEntrance1` | `Interior/DG_DvergrTown` | `DvergerTown` | 16-96 | 64 x 256 x 64, tuile 8 |
| `Mistlands_DvergrTownEntrance2` | `Interior/DG_DvergrTown` | `DvergerTown` | 16-96 | 64 x 256 x 64, tuile 8 |
| `Mistlands_DvergrBossEntrance1` | `Interior/DG_DvergrBoss` | `DvergerBoss` | 512-512 | 64 x 256 x 64, tuile 8 |

Rayon intérieur 32 pour les trois ; quantités 120 / 120 / 5.

### B.6 Shaders et rendu instancié (L689-888)

`SystemInfo.supportsInstancing` = True ; `Graphics.RenderMeshInstanced` disponible ; pipeline built-in, Direct3D11.

- `Custom/Creature` : **`Shader.Find` renvoie null**, mais le shader existe (761 prefabs, 1580 emplacements de matériau), 4 passes, mot-clé d'instancing, `enableInstancing` relu True, rendu de test sans erreur (L696, L883). 38 propriétés dont `_MainTex`, `_BumpMap`, `_Color`, `_EmissionMap`, `_EmissionColor`, `_Hue`, `_Saturation`, `_Value` (L325). Matériau de la tique : `Feasting_mat`, queue 2000 ; du jeune fouineur : `babyseeker`.
- Même constat pour `Custom/Piece`, `Standard`, `Custom/Vegetation`, `Custom/Player`. Seul `Legacy Shaders/Diffuse` est trouvé par `Shader.Find` (propriétés `_Color`, `_MainTex`).
- Transparents présents mais **non testés** : `Legacy Shaders/Particles/Alpha Blended` (1324 emplacements), `Custom/AlphaParticle`, `Custom/Distortion` (matériau `Aspect_mat`, queue 3000).

### B.7 Couches et touches (L765-814)

| Couche | Indice |
| --- | --- |
| `character` | 9 |
| `character_ghost` | 17 |
| `hitbox` | 18 |
| `character_net` | 26 |
| `character_noenv` | 27 |

`Physics.queriesHitTriggers` = True. La mêlée interroge avec `QueryTriggerInteraction.Ignore` (ne touche **pas** les triggers) ; `Aoe` et `Projectile` touchent les triggers. Masques reconstruits d'après le code, pas lus sur les composants.

### B.8 Versions (L863)

Jeu 1.0.20 (`1.0.20 e3ee4a778ed0`), Unity 6000.0.75f1, Jotunn 2.30.2, BepInEx 5.4.23.5.

### B.9 Encore à mesurer, et défauts du dump

À mesurer (aucune donnée dans le dump) :

- `StatusEffect` : `ObjectDB.m_StatusEffects` (icône d'Infesté, `m_attackStatusEffect` de la tique) : phase 5.
- `EffectArea` de feu (torche tenue, braseros) pour la peur du feu : phase 5.
- Effet de dôme / bouclier (shaders transparents à tester) : phase 2.
- Matrice de collision `Physics.GetIgnoreLayerCollision` pour le proxy de ver : phase 3.
- `Room.m_size` des salles de mine et `m_placedRooms` : phase 7.
- Agent de chemin dans les escaliers des mines (S08) : phase 4. `EnemyHud`, `StaticTarget` / `WearNTear` des objets dvergr, `CreatureSpawner` de donjon : non couverts.
- `sfx_baby_seeker_idle` : existence à confirmer.

Défauts du dump :

- L'énumération `sfx_*` est incomplète : elle ne contient pas des `sfx_` pourtant référencés par des créatures (ex. `sfx_baby_seeker_idle`).
- La colonne `Renderer.bounds` est égale à l'AABB du maillage (les prefabs non instanciés ont des bounds vides) ; seule l'AABB de maillage est fiable.
- Les masques de couches (Attack, Aoe, Projectile) sont reconstruits d'après le code, pas lus.
- Aucune donnée de `StatusEffect`, d'`EffectArea` de feu, de dôme, de matrice de collision ni de `Room.m_size`.
- Le dump ne couvre pas les animations ni `m_eye`, `Animator.cullingMode`, `ZSFX.m_maxConcurrentSources` / `m_hash`.
