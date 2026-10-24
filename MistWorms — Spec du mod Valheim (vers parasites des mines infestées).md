# MistWorms — Spec du mod Valheim (vers parasites des mines infestées)

Oct 24, 2026 · @Clément PLANÇON

## 0. Résumé et décisions clés

MistWorms ajoute aux mines infestées des Terres brumeuses un parasite : le **Ver des brumes**, insignifiant seul, redoutable en nombre. Les vers rampent, se rejoignent et forment un **Amas grouillant** qui se déplace comme un seul être, engloutit sa cible et dévore d'abord tout son équipement avant de s'en prendre à ses points de vie. Chaque ver tué rend une part de ce que l'amas a mangé.

Ce document est la spécification complète destinée à Claude Code (Opus) : il doit pouvoir réaliser le mod de bout en bout — code, assets, tests in-game, packaging — sans autre source. Les points marqués **\[À VÉRIFIER\]** sont des hypothèses sur les données internes de Valheim 1.0 à confirmer en phase 0 (§16) avant d'implémenter.

| Sujet | Décision retenue |
| --- | --- |
| Jeu cible | Valheim 1.0, branche publique (patch 1.0.17 du 6 oct. 2026 au moment de la rédaction) ; aucun autre mod sur le serveur |
| Stack | BepInExPack\_Valheim + Jötunn (≥ 2.30) + HarmonyX ; C# ciblant .NET Framework 4.7.2 ; Unity Editor non requis |
| Assets | Vrais modèles créés par Claude Code avec Blender en mode headless (scripts bpy versionnés), chargés à l'exécution au format glTF |
| Ver seul | ≈ 10 PV (PV d'une tique ÷ 10 **\[À VÉRIFIER\]**), une morsure = 1 point de durabilité, un coup d'arme bronze ou mieux le tue |
| Résistances | Feu : très faible ; tranchant : faible ; perçant : résistant ; poison et spirituel : immunisé. Un coup sur l'amas tue plusieurs vers (dégâts ÷ PV d'un ver) : les armes de zone sont la réponse |
| Feu | Les repousse (torche tenue, feu de camp), comme les grisâtres |
| Niveaux | Jamais d'étoiles |
| Amas | Se forme dès 3 vers proches, fusionne jusqu'à 200, se dissout sous 5 après une perte (§4.4) ; rayon ∝ n^0,36 (10 ≈ tique, 20 ≈ couvain, 200 ≈ un peu moins qu'un soldat ; calibré en jeu, §4.5) |
| Combat contre l'amas | Pas d'étourdissement ; recul décroissant avec la taille ; parade possible sans stagger |
| Passages étroits | L'amas « coule » : il s'étire visuellement, passe, se reforme (aucune scission d'entité) |
| Engloutissement | Joueur ralenti ×0,5 ; attaque et roulade autorisées ; la roulade ne libère pas ; on s'échappe en courant, avec du feu ou en tuant assez de vers |
| Morsures | 1 morsure par ver toutes les 0,5 s ; l'attaque cobra d'un ver seul suit la même règle (équipement d'abord) |
| Cibles | Tout : joueurs, apprivoisés, créatures, dvergar, structures — mais l'activation ne se fait qu'à la détection d'un joueur |
| Blocage / armure | Un bouclier levé réduit les morsures et perd sa durabilité comme en vanilla ; l'armure ne réduit rien |
| Équipement dévoré | Tout l'inventaire, pièce au hasard, Wisplight en dernier ; exploit « réparation gratuite » assumé |
| Drops | Pool au niveau de l'amas ; recette de base + matériaux d'amélioration selon la qualité ; objets sans recette = rien ; arrondi vers le bas ; drops natifs : appâts de pêche pondérés par rareté, entrailles, sac de sang ; pas de trophée ; assets vanilla réutilisés |
| Nids et spawn | Un petit nid par salle de mine + quelques vers isolés ; le nid pond ; max 50 vivants par nid, 200 par amas, 400 par mine ; mine propre une fois les nids détruits ; jamais hors mine |
| Performance | Priorité absolue : pas de 400 Characters, vers « virtuels » simulés en tableaux, rendu instancié, budgets chiffrés (§11) |
| Livraison | Tout d'un bloc, avec un harnais de test in-game (§15) pour que Claude Code vérifie lui-même chaque règle |
| Distribution | Thunderstore public ; serveur dédié HG Hosting ; configuration BepInEx imposée par le serveur |

Principes directeurs, dans l'ordre : performance (le jeu est très optimisé, le mod ne doit pas se voir dans le frametime), feeling vanilla (sons, effets, HUD et textes du jeu réutilisés, rien de custom à l'écran), tout configurable (chaque nombre de ce document est une clé de config, §14, sauf les constantes nommées qui y sont listées), tout testable (chaque règle a un scénario d'acceptation, §15). La spec est en français, le code, les identifiants, les clés de config et les commits en anglais ; les textes visibles par les joueurs existent en français et en anglais.

## 1. Contexte technique et environnement

Le développement et les tests se font sur le PC Windows où Valheim est installé via Steam : Claude Code y tourne, compile le mod, lance le jeu et exécute les scénarios de test. Le Mac mini n'intervient pas (il peut compiler, mais pas faire tourner le jeu avec BepInEx dans de bonnes conditions ; Valheim 1.0 a bien une version Mac, mais le modding y est marginal).

### 1.1 Versions cibles

| Composant | Version | Remarque |
| --- | --- | --- |
| Valheim | 1.0.x, branche publique (1.0.17 au 6 oct. 2026) | Sortie 1.0 le 9 sept. 2026 avec le Deep North ; pas d'autre mod sur le serveur ; la refonte du combat « Call to Arms » (début 2026 : adrénaline, roulade parfaite, blocage parfait sans endurance) est en jeu et influence §6 |
| BepInExPack\_Valheim (denikson, Thunderstore) | la plus récente au moment du build (une build « jour de la 1.0 » existe) | Fournit BepInEx 5.4.x + HarmonyX ; à épingler dans `manifest.json` |
| Jötunn (ValheimModding, Thunderstore / NuGet `JotunnLib`) | ≥ 2.30.0 (sortie avec la 1.0, alignée sur 1.0.7), la plus récente au build | Prefabs, créatures, localisation, config synchronisée, vérification de présence du mod côté client |
| .NET | SDK 8 pour `dotnet build` ; cible `net472` via `Microsoft.NETFramework.ReferenceAssemblies` | Un `.csproj` SDK-style, pas de Visual Studio requis |
| Assemblies publicisées | `BepInEx.AssemblyPublicizer.MSBuild` sur `assembly_valheim.dll` et `assembly_utils.dll` | Accès direct aux membres privés sans réflexion |
| Blender | 4.x (winget `BlenderFoundation.Blender`) | Uniquement en `--background --python` |
| Python | 3.12 (winget) | Scripts du harnais (`tools/vh.py`) |
| Unity Editor | **non requis** | Les assets sont chargés à l'exécution (§13) ; aucune AssetBundle |

Claude Code vérifie les versions réelles au début de la phase 0 (API Thunderstore `https://thunderstore.io/api/experimental/package/<ns>/<name>/`), les note dans `docs/versions.md` et les épingle dans le projet et le manifest.

### 1.2 Machines et rôles

| Machine | Rôle | Détails |
| --- | --- | --- |
| PC Windows (jeu) | Dev, build, tests client, serveur dédié local de test | Claude Code (CLI ou VS Code) installé dessus ; Steam et Valheim présents ; Unity absent |
| Serveur dédié HG Hosting | Serveur de jeu des potes | Vanilla aujourd'hui ; BepInExPack à installer via le panel de l'hébergeur ou SFTP (§17) ; OS du serveur à confirmer (Linux probable) |
| Clients des joueurs | Windows | Installation via r2modman / Thunderstore Mod Manager et un code de profil partagé (§17) |

Le jeu Steam du PC n'est pas modifié : le harnais crée une copie dédiée `C:\ValheimDev\` (robocopy depuis le dossier Steam) avec BepInEx, et un serveur dédié local `C:\ValheimDevServer\` (SteamCMD, app 896660). Le joueur garde ainsi un client vanilla pour rejoindre le serveur tant que celui-ci n'est pas moddé.

### 1.3 Outils à installer (script `tools/install-tools.ps1`)

- `winget install Git.Git Microsoft.DotNet.SDK.8 BlenderFoundation.Blender Python.Python.3.12 7zip.7zip`
- SteamCMD (zip officiel) dans `C:\SteamCMD\` pour le serveur dédié de test
- `ilspycmd` (`dotnet tool install -g ilspycmd`) pour décompiler `assembly_valheim.dll` dans `reference/decompiled/` (gitignoré) : c'est la source de vérité pour les signatures de méthodes, les noms de champs et les prefabs
- Jötunn et BepInExPack téléchargés depuis Thunderstore (zip), extraits dans la copie de jeu et le serveur local

### 1.4 Structure du dépôt (`mistworms/`)

```markdown
mistworms/
  MistWorms.sln
  src/MistWorms/            # plugin BepInEx (net472)
    MistWorms.csproj
    Plugin.cs               # BaseUnityPlugin, GUID, Harmony, Jötunn managers
    Config/                 # clés BepInEx, sync serveur (§14)
    Colony/                 # WormColony, VirtualWorm SoA, Nest, WormProxy (§10–11)
    Mass/                   # WritheMass (Character), WritheMassAI, engulf, pool (§4, §6, §7) ; créé en phase 4B
    Damage/                 # patches Harmony dégâts/durabilité/drops (§6–7)
    Spawn/                  # seeding des mines (§8)
    Render/                 # instancing, flipbook, matériaux (§12)
    Assets/                 # loader glTF, textures, catalogue (§13)
    Audio/                  # mapping sfx vanilla (§12)
    Localization/           # fr.json, en.json
    Dev/                    # QuickStart, DevBridge, scénarios (#if MW_DEV) (§15)
  src/MistWorms.Tests/      # xunit : parseur glTF, maths du pool, formation, config
  assets/
    blender/                # make_worm.py, make_mass.py, make_nest.py, render_icon.py, common.py
    build/                  # sortie : *.glb, *.png (versionnés, régénérables)
  tools/                    # install-tools.ps1, setup-game.ps1, setup-server.ps1, build.ps1, run-game.ps1, run-server.ps1, vh.py, package.ps1, decompile.ps1
  thunderstore/             # manifest.json, README.md, CHANGELOG.md, icon.png (généré)
  docs/                     # versions.md, vanilla-reference.md (dump phase 0), decisions.md, test-report.md
  reference/                # decompiled/ (gitignoré), game-dlls/ (gitignoré)
```

Le build (`tools/build.ps1 -Configuration Debug|Release`) compile, copie `MistWorms.dll` + `assets/build/` dans `C:\ValheimDev\BepInEx\plugins\MistWorms\` (et dans le serveur local), et la build Release exclut tout le dossier `Dev/` par la constante de compilation `MW_DEV`.

## 2. Noms, identifiants et localisation

Le mod s'appelle **MistWorms** ; ses trois entités visibles sont le ver, l'amas et le nid. Tous les noms affichés passent par la localisation Jötunn (français + anglais), jamais en dur dans le code.

| Élément | Identifiant | Français | Anglais |
| --- | --- | --- | --- |
| Plugin GUID | `dev.pointvirgule.mistworms` | — | — |
| Nom du plugin / package Thunderstore | `MistWorms` (namespace Thunderstore = compte de l'utilisateur, à renseigner dans `manifest.json`) | — | — |
| Namespace C# | `MistWorms` | — | — |
| Fichier de config | `BepInEx/config/dev.pointvirgule.mistworms.cfg` | — | — |
| Ver (entité virtuelle, pas de prefab) | token `$mw_worm` | Ver des brumes | Mist Worm |
| Amas (prefab créature) | `MW_WritheMass`, token `$mw_mass` | Amas grouillant | Writhing Mass |
| Nom au survol de l'amas | `$mw_mass_count` = « {0} ({1}) » | Amas grouillant (40) | Writhing Mass (40) |
| Nid (prefab destructible) | `MW_WormNest`, token `$mw_nest` | Nid de vers | Worm Nest |
| Colonie (prefab invisible, un par mine) | `MW_WormColony` | — | — |
| Effet de statut | `MW_SE_Infested`, tokens `$mw_se_infested_name` / `$mw_se_infested_desc` | Infesté / « Des vers vous recouvrent. Vitesse réduite, votre équipement est dévoré. » | Infested / "Worms cover you. Speed reduced, your equipment is being eaten." |
| Texte flottant de morsure | `$mw_text_eq` = « Équipement −{0} » / "Gear −{0}" | Équipement −12 | Gear −12 |
| Message de mort (vanilla, via le nom de l'attaquant) | nom de l'amas | « Tué par Amas grouillant » | "Killed by Writhing Mass" |
| Commandes console (build Debug uniquement, sauf `mw_version`) | préfixe `mw_` : `mw_version`, `mw_spawn <n> [rayon]`, `mw_mass <n>`, `mw_nest`, `mw_kill`, `mw_state`, `mw_tpmine`, `mw_wake`, `mw_calm`, `mw_perf`, `mw_scenario <nom>` | — | — |

Règles : les fichiers `Localization/fr.json` et `Localization/en.json` contiennent tous les tokens ; l'anglais est la langue de repli. Les noms de prefabs vanilla utilisés (base de clonage de l'amas, items de drop, sons, effets) sont listés en annexe (§20) avec leur statut de vérification ; ils sont centralisés dans une classe `VanillaNames` et jamais répétés en chaîne littérale ailleurs.

## 3. Design — le ver seul

Un ver seul est une menace dérisoire : 10 PV, un coup de n'importe quelle arme en bronze ou mieux le tue, et sa seule attaque, le coup de cobra, grignote 2 points de durabilité. Sa vraie fonction est de rejoindre les autres : dès qu'un ver repère un joueur, il cherche d'abord un groupe à rejoindre et n'attaque seul que s'il n'y en a aucun à portée.

### 3.1 Caractéristiques

| Caractéristique | Valeur par défaut | Clé de config | Justification |
| --- | --- | --- | --- |
| Points de vie | 10 | `Worm.Health` | PV d'une tique ÷ 5 (la tique a 50 PV en 1.0.20, dump du 24 oct. 2026 ; 10 PV conservés par décision) ; un couteau en silex (≈ 12) ou une massue (≈ 12) tue en un coup, une flèche en bois (≈ 22 × 0,5 perçant = 11) aussi, un poing non |
| Longueur / largeur | 0,40 m / 0,06 m | — | 10 vers ≈ une tique, échelle visuelle de §4.5 |
| Vitesse | 2,4 m/s (fuite : × 1,35) | `Worm.MoveSpeed`, `Worm.FleeSpeedMultiplier` | Un peu moins vite qu'un joueur qui court normalement ; un sprint sème n'importe quel amas |
| Vitesse de rotation | 360 °/s | `Worm.TurnSpeed` | Rampe, ne pivote pas sur place |
| Morsure (en amas) | 1 point de durabilité par morsure, toutes les 0,5 s | `Worm.DurabilityPerBite`, `Worm.BiteInterval` | 40 vers ≈ 80 points/s |
| Coup de cobra (seul) | 2 points de durabilité par coup ; vs créature : 2 dégâts perçants | `Worm.CobraDurability`, `Worm.CobraCreatureDamage` | Même règle « équipement d'abord » (§6) |
| Dégâts vs créatures (en amas) | 1 dégât perçant par ver par morsure | `Worm.CreatureDamagePerBite` | Agrégé par tick (§6.5) |
| Dégâts vs structures (en amas) | 0,5 par ver par morsure | `Worm.StructureDamagePerBite` | Type contondant |
| Dégâts PV vs joueur (quand plus rien à dévorer) | 1 par ver par morsure, type perçant | `Worm.HealthDamagePerBite` | Agrégé par tick ; l'armure est à 0 à ce stade |
| Niveau | toujours 1 (aucune étoile) | — | Décision Q&A 14 |
| Portée de vue / angle (actif) | 20 m / 360° | `Worm.ViewRange`, `Worm.ViewAngle` | Un ver actif perçoit tout autour de lui (pas d'yeux, sensible aux vibrations) ; plus court que la tique (30 m, demi-angle 90°), décision du 24 oct. 2026 ; c'est aussi la portée de ciblage d'un ver seul (§5.3) |
| Portée d'ouïe (actif) | 15 m | `Worm.HearRange` | Mêmes règles de bruit que `BaseAI.CanHearTarget` (le bruit de la cible compte) |
| Vue / ouïe (dormant) | 4 m / 6 m | `Worm.DormantViewRange`, `Worm.DormantHearRange` | Un ver endormi, à demi enfoui, ne réagit qu'au joueur tout proche ou bruyant |
| Portée du coup de cobra | 1,2 m ; préparation 0,6 s ; frappe 0,25 s ; récupération 1,5 s | `Worm.CobraRange`, `Worm.CobraWindup`, `Worm.CobraCooldown` | Lisible, esquivable |
| Rayon de regroupement | 20 m | `Mass.GatherRadius` | « Priorité au groupe » : s'il existe un amas ou ≥ 2 autres vers actifs dans ce rayon, le ver va les rejoindre au lieu d'attaquer |
| Durée de fuite | 4 s | `Worm.FleeDuration` | Un ver fuit 4 s (§3.3) ; un amas suit `Fire.FearDuration` (§9.1) |

### 3.2 Résistances (ver, amas et nid partagent la même table)

| Type de dégât | Modificateur | Effet |
| --- | --- | --- |
| Feu | VeryWeak (× 2) | Le feu est la contre-mesure principale |
| Tranchant | Weak (× 1,5) | Les lames tranchent les corps mous : épées et haches de combat sont une bonne réponse, derrière le feu |
| Perçant | Resistant (× 0,5) | Les flèches et lances ne sont pas la réponse |
| Contondant, gel, foudre | Normal | Frostner, Démolisseur et les armes de zone brillent par leur zone, pas par un bonus |
| Poison, spirituel | Immune | Parasites, pas des morts-vivants |
| Hache (chop), pioche | Ignore | Comme les créatures vanilla |

Chaque ligne est une clé `Resist.<Type>` acceptant les valeurs `HitData.DamageModifier` (Normal, Resistant, Weak, Immune, Ignore, VeryResistant, VeryWeak).

### 3.3 États d'un ver

1. **Sleeping** — dormant autour de son nid (§5), à demi enfoui, animation de pulsation lente. Se réveille : nid alerté, joueur dans la vue/ouïe dormante, ou dégâts reçus (par lui, son nid ou un ver à moins de 5 m).
2. **Idle** — actif sans cible : erre dans un rayon de 6 m autour de son nid (ou de sa position de réveil), pauses aléatoires de 1 à 3 s.
3. **Gathering** — a détecté un joueur et il existe un amas ou un groupe (≥ 2 autres vers actifs) à moins de 20 m : se dirige vers l'amas le plus proche, sinon vers le centroïde du groupe. À moins de `Mass.FormRadius` du centroïde avec ≥ 3 vers → formation d'un amas (§4.1). À moins de (rayon de l'amas + 0,5 m) d'un amas → passe en Absorbing (§4.1). Alignement avec les voisins de même état (voir « Comportement de groupe » ci-dessous).
4. **Hunting** — a détecté un joueur et rien à rejoindre : se dirige vers la cible la plus proche (règles de ciblage §5) ; à ≤ 1,2 m passe en Striking. Les vers qui chassent la même cible prennent chacun une place d'approche autour d'elle, et s'alignent sur leurs voisins en Hunting (voir « Comportement de groupe » ci-dessous).
5. **Striking** — préparation (se dresse), frappe (sphère de 0,5 m devant la tête), récupération ; puis Hunting. Réévalue « quelqu'un à rejoindre ? » à chaque récupération.
6. **Fleeing** — après la dissolution d'un amas (§4.4) ou si un feu est à moins de `Fire.FearRadius` : s'éloigne pendant `Worm.FleeDuration` (4 s) à vitesse × 1,35, puis Gathering si un groupe existe à moins de `Mass.RegroupRadius` (30 m), sinon Idle. La durée de fuite d'un amas est différente : il suit `Fire.FearDuration` (3 s, §9.1, phase 5).
7. **Absorbing** — absorption en cours (0,5 s, §4.1) : le ver grimpe sur le dôme jusqu'à une place de la spirale, puis s'enfonce (il descend sous la surface et son échelle tombe à 0,6). `n` augmente à la fin de l'animation. Pendant l'animation, le ver reste un ver à part entière : simulé, frappable, proxy actif. S'il meurt, il n'est pas absorbé (`n` n'augmente pas) et passe en Dead.
8. **Absorbed** — fait partie d'un amas : plus simulé ni rendu individuellement, seul son compteur et son origine (nid) sont conservés dans l'amas.
9. **Dead** — effet visuel de mort (§12.4), drops (§7), slot libéré.

**Comportement de groupe des vers libres.** Les poids et rayons sont des constantes nommées documentées, pas des clés de config (§14). La séparation, la cohésion et l'alignement sont détaillés en §11.1.

- **File indienne** : si un ver qui va dans la même direction (écart < 45°) est devant à moins de 0,6 m, le suiveur cale sa vitesse sur la sienne au lieu de le doubler en le traversant. Les vers qui visent le même but réutilisent le même chemin (cache existant, §11.1), donc les couloirs produisent des files.
- **Places d'approche** : les vers qui chassent la même cible se répartissent autour d'elle. Chaque ver reçoit un angle d'approche, dérivé de son indice, réparti sur 360° parmi les chasseurs de cette cible. Il vise le point à `Worm.CobraRange` de la cible dans cette direction s'il est atteignable (sol, pas de mur), sinon le point direct. Ils encerclent au lieu de s'empiler sur un point.
- **Cohésion légère** en errance (Idle) : poids 0,2 vers le centre des voisins à moins de 3 m ; les vers errent en grappes lâches.
- **Alignement** en Gathering et en Hunting : poids 0,3 vers le cap moyen des voisins de même état.
- Gathering inchangé : convergence vers l'amas le plus proche, sinon vers le centroïde du groupe.

Un ver ne saute pas, ne nage pas, ne grimpe pas aux murs ; il franchit une marche ≤ 0,4 m et une pente ≤ 60°. Il ne despawn jamais tant que la mine est chargée : son état est persisté (§11.4).

## 4. Design — l'amas

L'amas est un seul être aux yeux du jeu : une créature (`Character`) dont les points de vie, les dégâts et la taille sont la somme de ses vers, et dont le nom au survol affiche ce compte. Il n'y a jamais 40 entités : il y en a une, qui porte un compteur `n` et un pool de butin.

### 4.1 Formation et absorption

- Un amas naît quand ≥ `Mass.FormThreshold` (3) vers actifs en état Gathering sont à moins de `Mass.FormRadius` (2,5 m) de leur centroïde : la colonie crée un `MW_WritheMass` au centroïde avec `n` = nombre de vers, et retire ces vers de la simulation (état Absorbed). Rendu seulement, la logique reste celle de la spec : les vers rassemblés convergent vers le centroïde en 0,4 s, puis le dôme pousse de r = 0 à r(n) en 0,5 s (ease-out) pendant qu'ils s'y enfoncent.
- Un ver actif à moins de (rayon + 0,5 m) d'un amas passe en Absorbing (0,5 s, §3.3) ; à la fin de l'animation il est absorbé : `n` += 1, son pool (§7) fusionne, son nid d'origine est compté dans `nestCounts` de l'amas.
- Deux amas dont les sphères se touchent fusionnent si `n1 + n2 ≤ Mass.MaxCount` (200) : le plus gros absorbe le plus petit (compteur, pool, nestCounts). Sinon, le plus petit transfère exactement `200 − n1` vers au plus gros et continue seul avec le reste ; un amas plein (200) n'absorbe plus rien. Le pool et `nestCounts` sont transférés au prorata des vers transférés ; le `partial` (§4.2) du plus petit est perdu dans une fusion complète, et conservé par le reste dans une fusion partielle. Un reste qui passe sous `Mass.DissolveThreshold` à la suite d'une fusion partielle (sans perte par dégâts) ne se dissout pas : seules les pertes déclenchent la dissolution (§4.4). Rendu : le plus petit amas glisse vers le plus gros ; au contact, son dôme se résorbe en 0,5 s, ses vers de surface glissent sur le gros dôme, et le gros grossit avec le lissage du rayon (§4.5).
- Les vers libres qui ne peuvent rejoindre un amas plein forment entre eux un nouvel amas selon la règle de formation.
- Un amas formé à 3 ou 4 vers (sous `Mass.DissolveThreshold`) vit jusqu'à sa première perte : la dissolution ne s'applique qu'après une perte (§4.4).

### 4.2 Points de vie et dégâts reçus

- `maxHealth = n × Worm.Health` ; `health` suit `n` (mise à jour de `Character.SetMaxHealth` / `SetHealth` à chaque changement de `n`). Les PV sont exactement `n × Worm.Health`, sans multiplicateur NG+ ni modificateur de monde ; ils sont réimposés après `Character.Awake` (qui recalcule les PV max d'un non-joueur, décision 1.3), et `SetHealth` est appelé explicitement quand `n` augmente (absorption, fusion).
- Chaque coup reçu de dégâts effectifs `D` (après résistances §3.2 et blocage vanilla) fait : `partial += D` ; `kills = floor(partial / Worm.Health)` ; `partial −= kills × Worm.Health` ; `kills = min(kills, n)` ; `n −= kills`. Un marteau à 55 contondant sur un amas de 40 tue donc 5 vers, et le reste (5) est mémorisé pour le coup suivant. Les armes de zone (`Aoe`) touchent l'amas une fois par instance d'`Aoe`, comme toute créature.
- Chaque ver tué rend une part du pool (§7.3). Rendu : un ver de surface se détache, glisse et tombe au sol, puis l'effet de mort vert (§12.4) se joue (plafond de 10 par seconde inchangé).
- `n = 0` → l'amas meurt (vanilla `Character.OnDeath`), le reliquat du pool est droppé (§7.3). Aucun cadavre, aucun ragdoll.

### 4.3 Nom, HUD, niveau

- Nom au survol : « Amas grouillant (n) », recalculé à chaque changement de `n` (patch `Character.GetHoverName`). L'`EnemyHud` vanilla affiche la barre de PV comme pour toute créature ; aucune étoile (`level = 1`).
- L'amas apparaît dans le message de mort vanilla et dans les trophées de combat comme une créature normale.

### 4.4 Dissolution et fuite

- Si `n` tombe sous `Mass.DissolveThreshold` (5) **par suite de dégâts** (une perte, §4.2), l'amas se dissout : `n` vers libres sont recréés autour de sa position en état Fleeing (§3.3), le pool est réparti à parts égales entre eux (fractions autorisées), l'entité amas est détruite. La dissolution ne s'applique qu'après une perte : un amas formé à 3 ou 4 vers (§4.1) vit jusqu'à sa première perte. Elle est distincte du retour à la dormance (§5.2). Rendu : le dôme se résorbe en 0,4 s, et les `n` vers jaillissent radialement en un petit bond (hauteur 0,3 à 0,6 m, distance 0,5 à 1,5 m, sol vérifié par `WormPhysics`) avant de passer en Fleeing.
- Les fuyards fuient `Worm.FleeDuration` (4 s), puis cherchent un groupe à moins de `Mass.RegroupRadius` (30 m) ; trois fuyards qui se retrouvent reforment un amas.

### 4.5 Taille et forme

```latex
r(n) = R_{10} \left( \frac{n}{10} \right)^{e} \quad R_{10} = 0{,}83\ \text{m},\ e = 0{,}36
```

| n | r(n) | Diamètre | Hauteur | Repère visuel |
| --- | --- | --- | --- | --- |
| 3 | 0,54 m | 1,08 m | 0,43 m | un gros tas de vers |
| 10 | 0,83 m | 1,66 m | 0,66 m | ≈ une tique |
| 20 | 1,07 m | 2,13 m | 0,85 m | ≈ un couvain de fouineur |
| 50 | 1,48 m | 2,96 m | 1,19 m | à hauteur de hanche |
| 100 | 1,90 m | 3,80 m | 1,52 m | — |
| 200 | 2,44 m | 4,88 m | 1,95 m | un peu moins qu'un soldat fouineur ; recouvre un joueur debout |

`R_{10}` = `Mass.BaseRadius`, `e` = `Mass.RadiusExponent`. Hauteur du dôme = 0,8 × r.

**Calibrage (24 oct. 2026, mesures en jeu 1.0.20, dump §1b, maillages en pose de référence ; le joueur témoin mesure 1,905 m).** Une créature et le dôme sont comparés par leur taille caractéristique `L = √(D × H)`, où `D = √(largeur × longueur)` est l'emprise au sol et `H` la hauteur ; pour le dôme, `L = √(2r × 0,8r) = 1,265 r`. C'est le compromis des moindres carrés en logarithme entre largeur et hauteur : le dôme plus plat qu'une créature est compensé par une emprise plus large. Mesures : tique `D` 1,23 m, `H` 0,89 m, `L` 1,04 ; couvain `L` 1,32 ; soldat (`SeekerBrute`) `D` 4,61 m, `H` 2,51 m, `L` 3,40 ; fouineur (`Seeker`) `L` 3,53. Ancres imposées : r(10) = L_tique / 1,265 = 0,825 m et r(200) = 0,9 × L_soldat / 1,265 = 2,42 m, d'où `e = ln(r200 / r10) / ln 20 = 0,359`, arrondis à 0,83 et 0,36. En 1.0.20 le fouineur ordinaire est aussi gros que le soldat (équivalent ≈ 290 vers contre ≈ 260), si bien que l'ancien repère « 50 ≈ fouineur » est incompatible avec « 200 un peu moins qu'un soldat » : il est remplacé par « 20 ≈ couvain ». La **sphère physique** (collider de la créature, utilisée pour le déplacement et pour recevoir les coups) est bornée à `Mass.MaxColliderRadius` (0,6 m) quel que soit `n` : la forme visuelle peut dépasser le collider, c'est voulu, cela permet le passage des couloirs (§4.7) et évite qu'un amas de 200 bloque physiquement les joueurs. La hauteur du centre suit le sol (le collider repose au sol, le dôme visuel part du sol).

**Rayon affiché.** Rendu seulement : le rayon affiché d'un amas suit `r(n)` avec un lissage de 0,3 s, jamais de saut quand `n` change.

**Zone de coup.** Le collider physique reste borné à 0,6 m. Les coups passent par des sphères **sur la couche `hitbox`** (pas de collision physique, décision 47), routées vers l'amas : en dôme, une sphère de rayon 0,9 × r(n), centrée à 0,4 × r(n) de hauteur ; en Stream (§4.7), une chaîne de sphères le long du ruban. Un `Aoe` touche l'amas une fois (§4.2).

### 4.6 Déplacement

- Vitesse `Mass.MoveSpeed` = 2,4 m/s, égale à celle du ver seul ; pas de saut, pas de nage ; pathfinding vanilla avec le plus petit type d'agent qui passe les escaliers des mines **\[À VÉRIFIER : `Pathfinding.AgentType` disponible, ex. `Humanoid`\]**.
- Rotation : l'amas ne « se tourne » pas, il glisse ; les vers de surface rampent dans le sens du mouvement (§12).
- Hors combat (actif sans cible), l'amas erre à 6 m de son dernier point d'intérêt et retourne vers son nid d'origine majoritaire quand la colonie se calme (§5.2).

### 4.7 Passages étroits — la « coulée »

L'amas se scinde et se reforme visuellement, sans jamais se scinder en entités. Chaque 0,25 s, l'amas sonde la largeur libre devant lui (sphère de rayon `r(n)` projetée sur 2 m le long du chemin) : si elle est inférieure à `2 r(n) + 0,2 m`, il passe en mode **Stream** (une forme, pas un état, §4.10) : le dôme s'aplatit (hauteur × `Mass.StreamHeightFactor`, 0,4), s'allonge le long du chemin (longueur = min(n × `Mass.StreamLengthPerWorm` (0,08 m), `Mass.StreamMaxLength` (6 m)), largeur = largeur libre − 0,1 m) et les vers de surface se répartissent le long du ruban ; la zone de coup suit la coulée par une chaîne de sphères sur la couche `hitbox` le long du ruban (§4.5), pour rester frappable. Dès que 3 m de chemin sont libres, retour à la forme dôme. La transition dôme ↔ ruban dure 0,5 s dans les deux sens. La vitesse ne change pas. Les autres paramètres de la coulée (période de sondage 0,25 s, projection 2 m, marges 0,2 m et 0,1 m, 3 m libres) sont des constantes nommées, pas des clés de config.

### 4.8 Recul, étourdissement, parade

- Aucun étourdissement : `m_staggerWhenBlocked = false`, patch qui annule `Character.Stagger` et `AddStaggerDamage` pour `MW_WritheMass`.
- Recul : le facteur `clamp(10 / n, Mass.KnockbackMinFactor, 1)` s'applique à `HitData.m_pushForce` avant le recul vanilla (le point exact du patch sera choisi au dump) — un amas de 10 recule comme une tique, un amas de 200 bouge à peine.
- Parade : le blocage et la parade vanilla fonctionnent contre les morsures (§6.4) ; une parade parfaite (vanilla) ne stagger pas l'amas et, par règle du mod, annule ce tick de morsure et retarde le suivant de 0,5 s. C'est une logique du mod, indépendante du fait que le vanilla n'annule pas le coup (décision 1.3). Phase 5 (S03b).

### 4.9 Engloutissement (résumé, détails §6)

Phase 5 (en phase 4B l'amas s'arrête au contact sans engloutir, §4.10).

À moins de `r(n) + Mass.EngulfRange` (0,6 m) de sa cible, l'amas s'y colle et la recouvre : il suit la position de la cible avec un retard de 0,15 s, les collisions physiques entre l'amas et la cible sont ignorées (`Physics.IgnoreCollision`), la cible reçoit l'effet **Infesté**, et le cycle de morsures commence. Il se détache si la cible s'éloigne de plus de `r(n) + Mass.DetachDistance` (1,5 m) pendant `Mass.DetachDelay` (0,75 s), si un feu le fait fuir, si la cible meurt ou si `n` tombe sous le seuil de dissolution.

### 4.10 États de l'amas

| État | Rôle | Phase |
| --- | --- | --- |
| Idle | errance, retour vers le nid (§4.6) | 4B |
| Hunt | se dirige vers sa cible (§5.3) ; au contact (à `r(n) + Mass.EngulfRange`) il s'arrête sans engloutir | 4B |
| Engulf | collé à la cible, tick de morsure (§4.9, §6) | 5 |
| Dormant | immobile, ne cible rien et n'est pas ciblé (§5.1) ; stub en 4B | 7 |

Stream (§4.7) est une forme, pas un état : il se combine à n'importe quel état de déplacement et se synchronise par un bit séparé (§11.3).

## 5. Design — activation, dormance et cibles

La colonie ne s'éveille que pour un joueur ; une fois éveillée, elle dévore tout ce qui bouge ou qui tient debout, joueurs, apprivoisés, fouineurs, dvergar et constructions, puis se rendort quand plus aucun joueur n'est là.

### 5.1 Réveil

- Un **groupe de nid** (un nid + ses vers + les amas qu'ils forment) est Dormant ou Actif. Au réveil d'un nid, tous ses vers endormis passent Idle puis Gathering/Hunting, et l'alerte se propage à tout ver ou nid à moins de `Colony.AlertRadius` (25 m), nid après nid (une alerte ne traverse pas plus de 2 sauts par seconde, pour que l'éveil d'une grande mine se fasse en vague, pas instantanément).
- Déclencheurs de réveil, uniquement par un **joueur** (`Targets.WakeOnlyByPlayers = true`) : joueur dans la vue/ouïe dormante d'un ver (§3.1), joueur à moins de `Nest.WakeRadius` (10 m) du nid en ligne de vue, dégâts infligés par un joueur à un ver, un nid ou un amas (quel que soit l'attaquant réel : un fouineur qui tue un ver dormant ne réveille rien, un joueur si). Les créatures vanilla ne peuvent pas cibler un ver dormant : il n'existe pas pour leur IA (§10).
- Un amas dormant (colonie rendormie avec des amas existants, §5.2) reste une entité mais ne cible rien et n'est pas ciblé par les créatures ; il s'éveille comme un nid.

### 5.2 Retour au calme

- Le groupe reste Actif tant qu'un joueur est à moins de `Targets.ActiveRadius` (45 m) d'un de ses membres. Après `Targets.CalmDelay` (60 s) sans joueur, il se rendort : les amas rentrent vers leur nid majoritaire (ou le nid le plus proche, ou restent sur place sans nid), puis se dissolvent en vers Sleeping disposés autour (compte, pool et origines conservés, §11.4) ; les vers libres retournent dormir.
- Un joueur qui revient réveille tout de nouveau ; le butin dévoré n'est jamais perdu par la dormance.

### 5.3 Cibles et priorités (groupe Actif)

| Catégorie | Attaquée ? | Clé | Poids de distance |
| --- | --- | --- | --- |
| Joueurs | oui | `Targets.AttackPlayers` | 0,6 (`Targets.PlayerWeight`) |
| Créatures apprivoisées | oui | `Targets.AttackTamed` | 0,8 |
| Créatures sauvages (fouineurs, tiques, couvains, soldats, dvergar…) | oui | `Targets.AttackCreatures` | 1,0 |
| Structures (`WearNTear` : pièces construites par les joueurs, portes et objets dvergr destructibles de la mine) | oui | `Targets.AttackStructures` | 1,5 |
| Nids, vers, amas, pierres tombales, portails, objets non destructibles | jamais | — | — |

Choix de cible, réévalué toutes les 0,5 s : parmi les cibles visibles à moins de `Mass.TargetRange` (20 m), la plus petite valeur `distance × poids` gagne ; une cible engloutie est conservée jusqu'au détachement (§4.9). Un ver seul suit les mêmes règles avec `Worm.ViewRange` (20 m) pour portée de ciblage (§3.1). Les prefabs listés dans `Targets.ExcludedPrefabs` (défaut `SW_DivineWhale`) ne sont jamais ciblés, ni par les vers ni par les amas (§10.4 ; phase 6, plus la sélection de cibles de l'amas en 4B). Les joueurs sont donc préférés à distance égale, mais un fouineur collé à l'amas sera mangé avant un joueur à 15 m.

### 5.4 Factions et guerre dans la mine

L'amas doit être ennemi de toutes les factions, y compris `MistlandsMonsters` et `Dverger`, pour que fouineurs et dvergar ripostent et pour que la mine devienne un champ de bataille à trois camps. Implémentation : faction `PlayerSpawned`, la seule hostile à toutes les autres dans les deux sens (`Faction.Boss` ne convient pas, décision 1.1 ligne 28), sans postfix sur `BaseAI.IsEnemy` pour l'amas. Le harnais (§15) imprime la matrice `IsEnemy` pour vérifier.

Quand l'amas tue une créature ou détruit une structure, ce qu'elles auraient laissé tomber va dans son pool au lieu du sol (§7.4) : « ça drop tout ce que ça a bouffé ».

## 6. Design — engloutissement, morsures et équipement

Les morsures d'un amas ou d'un ver ne touchent jamais les points de vie d'un joueur tant qu'il lui reste un objet à dévorer : elles consomment la durabilité de son inventaire, pièce au hasard, Wisplight en dernier, et ne passent aux PV qu'une fois tout détruit.

### 6.1 Le tick de morsure

Toutes les `Worm.BiteInterval` (0,5 s), l'amas englouti autour d'une cible produit **un seul coup agrégé** (une `HitData`) : `P = n × Worm.DurabilityPerBite` points pour un joueur, `n × Worm.CreatureDamagePerBite` dégâts perçants pour une créature, `n × Worm.StructureDamagePerBite` contondants pour une structure. Agréger par tick (plutôt que 40 micro-coups) garde le réseau et le blocage vanilla sains. Le coup est émis par le propriétaire de l'amas avec `m_attacker = ZDOID de l'amas` ; il voyage via le RPC `Damage` vanilla jusqu'au client propriétaire de la cible, où les règles ci-dessous s'appliquent.

### 6.2 Effet « Infesté » sur la cible

- Appliqué à l'engloutissement, retiré au détachement (`SEMan` vanilla, `StatusEffect` custom `MW_SE_Infested`, icône vanilla réutilisée **\[À VÉRIFIER : sprite d'un effet existant, ex. celui de la morsure de tique\]**).
- Effets : vitesse de déplacement × `Engulf.SlowFactor` (0,5) y compris sprint ; attaques, blocage, parade, roulade, consommables et interactions autorisés ; aucun dégât périodique propre.
- La roulade ne libère pas (l'amas suit) mais ses frames d'invincibilité vanilla annulent le tick de morsure qui tombe pendant la roulade ; la roulade parfaite de Call to Arms est traitée par le jeu comme d'habitude.
- Se libérer : sprinter hors de portée (le joueur ralenti sprinte à ≈ 3,5 m/s, l'amas suit à 2,4 m/s : on s'en sort, lentement), brandir du feu (§9.1), ou réduire l'amas sous le seuil de dissolution.

### 6.3 Ordre de destruction de l'inventaire (joueur)

1. **Éligibles** : tous les objets de l'inventaire du joueur (équipés ou non, toutes les cases, y compris les emplacements rapides) tels que `m_shared.m_useDurability` et `m_durability > 0`, **sauf** le Wisplight (`Demister` **\[À VÉRIFIER\]**). Les objets sans durabilité (matériaux, flèches, nourriture, trophées, Megingjord…) ne sont jamais touchés.
2. **Nombre de pièces mordues par tick** : `k = clamp(ceil(n / 10 × Engulf.ItemsPerTenWorms), 1, nombre d'éligibles)` — un gros amas ronge plusieurs pièces à la fois.
3. `k` objets tirés au hasard sans remise parmi les éligibles ; chacun perd `P / k` points, plafonné à sa durabilité restante ; l'excédent est reporté sur un autre éligible tiré au hasard, jusqu'à épuisement de `P` ou des éligibles.
4. **Wisplight en dernier** (`Engulf.WisplightLast = true`) : il ne devient éligible que lorsqu'aucun autre éligible ne subsiste. Sa destruction ramène la brume : c'est le coup de grâce voulu. \[À VÉRIFIER : le Wisplight a-t-il une durabilité ?\] S'il n'en a pas (m\_useDurability = false), le mod lui en donne une au démarrage en modifiant ses données partagées dans l'ObjectDB (m\_useDurability = true, m\_maxDurability = Engulf.WisplightVirtualDurability, défaut 100, m\_durabilityPerLevel = 0) : il ne s'use alors que sous les morsures, se casse à 0 comme une pièce d'armure et se répare gratuitement à l'établi, comme le reste.
5. **Phase PV** : s'il ne reste aucun éligible (Wisplight inclus), le reste de `P` est appliqué aux PV via le chemin vanilla, avec `n × Worm.HealthDamagePerBite` dégâts perçants par tick (l'armure est nulle à ce stade, toutes les pièces étant cassées ; un bouclier levé mais cassé ne bloque plus non plus).
6. Un objet à 0 suit le comportement vanilla : cassé, déséquipé automatiquement, réparable gratuitement à l'établi (exploit assumé, §0) ; un objet vanilla `m_destroyBroken` disparaît comme en vanilla.

### 6.4 Blocage et armure

- **Blocage** : le coup agrégé passe par `Humanoid.BlockAttack` vanilla sur le client de la cible : bouclier levé et amas dans l'angle de blocage → réduction selon la puissance de blocage, perte de durabilité du bouclier et consommation d'endurance comme pour n'importe quel coup ; blocage parfait (Call to Arms) : le vanilla n'annule pas le coup (il multiplie seulement la puissance de blocage, décision 1.3), mais la règle du mod (§4.8) annule ce tick de morsure et retarde le suivant de 0,5 s (phase 5). La valeur retenue pour `P` est la valeur **après blocage**.
- **Armure** : jamais appliquée aux morsures. Patch : préfixe sur `HitData.ApplyArmor` qui saute l'appel quand `m_attacker` est un amas ou un ver.
- **Esquive** : frames d'invincibilité de la roulade respectées (vanilla).
- **Mode dieu / fantôme** (devcommands) : aucun coup appliqué (vanilla) ; le harnais désactive le mode dieu pendant les scénarios de morsure.

### 6.5 Créatures et structures

- Créature engloutie (apprivoisée ou sauvage) : coup agrégé perçant, armure et résistances vanilla de la créature appliquées, riposte vanilla (la créature attaque l'amas). Pas d'effet Infesté sur les créatures (mais ralentissement : `m_speed` de la créature × 0,5 via un `StatusEffect` sans icône si la créature a un `SEMan`).
- Structure : coup agrégé contondant sur `WearNTear` ; une porte fermée est une cible valide (l'amas la mange plutôt que de la contourner) ; les pièces à durabilité infinie ou protégées par une pierre de garde suivent la règle vanilla (pas de dégâts).
- Ce que la cible aurait laissé tomber va dans le pool (§7.4).

### 6.6 Texte et retour visuel

- Chaque tick sur un joueur affiche un texte flottant vanilla (`DamageText`) orange au lieu du rouge des dégâts : « Équipement −12 » (valeur `P` effective), ou le nombre rouge habituel en phase PV.
- Quand une pièce casse, le message vanilla « X est cassé » suffit ; aucun message custom.
- Le dôme de l'amas entoure la cible ; pour ne pas aveugler la caméra, l'échelle du dôme est ramenée à 0,6 quand la caméra est à moins de `r + 0,7 m` du centre (phase 4B). Le dither (matériau translucide) n'est fait que si un shader vanilla le permet (à vérifier, sinon abandonné).

## 7. Design — drops

Tout ce que l'amas dévore entre dans un **pool** commun porté par l'amas ; chaque ver tué en rend sa part entière, le dernier rend le reliquat, et rien n'est jamais arrondi vers le haut. Un ver qui n'a rien mangé peut encore lâcher un appât de pêche, des entrailles ou un sac de sang.

### 7.1 Le pool

Le pool est un dictionnaire `prefab → quantité (float)`. Il existe sur chaque amas et sur chaque ver libre (vide pour un ver qui n'a rien mordu). Absorption d'un ver → son pool est additionné à celui de l'amas. Fusion d'amas → addition. Dissolution → division à parts égales entre les vers libérés (fractions conservées). Dormance → persisté avec l'amas ou le ver (§11.4). Chaque pool porte aussi un reste fractionnaire par ressource, `carry[r]` (§7.3), additionné, divisé et persisté comme le pool.

### 7.2 Ce qu'une morsure d'équipement ajoute au pool

Pour un objet de qualité `q` ayant perdu `d` points de durabilité (après blocage) sur une durabilité maximale `Dmax(q)` (`ItemData.GetMaxDurability(q)`) :

```latex
\text{pool}[r] \mathrel{+}= \frac{d}{D_{max}(q)} \times \left( a_r + (q-1) \times u_r \right) \quad \text{pour chaque ressource } r \text{ de la recette}
```

- `a_r` = `Recipe.m_resources[r].m_amount` (coût de fabrication), `u_r` = `m_amountPerLevel` (coût par niveau d'amélioration) ; `Drops.IncludeUpgradeMaterials = true` ajoute le second terme.
- Recette = `ObjectDB.GetRecipe(item)` ; objet sans recette, ou recette sans ressources (objets de marchand, de quête, de drop) → rien.
- `Drops.EquipmentDropRatio` (défaut 1,0) multiplie le tout ; il existe pour le jour où l'exploit « réparer gratis » lasserait.
- Le calcul se fait sur le client de la cible (seul à connaître son inventaire) et le résultat est envoyé à l'amas par RPC `RPC_Consumed(list<prefab, float>)` (§11).

Exemple : une cuirasse rembourrée q1 (10 fer, 20 fil de lin, 1000 de durabilité) qui perd 437 points → +4,37 fer et +8,74 fil de lin dans le pool.

### 7.3 Ce qui tombe quand un ver meurt

- Le calcul est indépendant du chemin (phase 6). Chaque ressource `r` garde un reste fractionnaire `carry[r]`. Pour `k` vers tués d'un coup sur un amas de `n` (avant les morts) : `share = pool[r] × k / n` ; `pool[r] −= share` ; `carry[r] += share` ; on droppe `floor(carry[r])` objets au point d'impact (`ItemDrop` vanilla, empilés par `m_maxStackSize`) et on les retire de `carry[r]`. Le total droppé est ainsi le `floor` de la part cumulée, quel que soit le découpage des coups (un coup de zone ou ver par ver). Exemple : 40 vers qui ont rongé 4,37 fer ; chaque ver tué en rend 4,37 / 40 = 0,109, donc rien pour les premiers (le `carry` monte), puis 1 fer dès le 10e ver tué. Les gros coups de zone rendent plus régulièrement.
- Mort du dernier ver (`n` → 0) : on droppe `floor(pool[r] + carry[r])` pour chaque entrée ; le reste fractionnaire est perdu (« arrondi à l'intérieur »).
- Ver libre tué : `floor` de son propre pool (reste `carry` compris), reste perdu.
- Les drops apparaissent avec la physique vanilla d'`ItemDrop` (petite impulsion aléatoire), et sont limités à 60 objets par événement pour éviter une pluie d'items (le surplus reste dans le pool pour le ver suivant, ou est droppé en piles maximales au dernier ver).

### 7.4 Créatures et structures dévorées

- Créature tuée par un amas ou un ver (dernier coup `m_lastHit.m_attacker` = nôtre) : la liste de drop vanilla (`CharacterDrop.GenerateDropList`, étoiles et modificateurs de monde compris) est calculée normalement puis **capturée** dans le pool au lieu d'être créée au sol (patch sur `CharacterDrop.GenerateDropList`, méthode d'instance, et gestion du chemin ragdoll ; l'effet de mort de l'amas n'a pas de `Ragdoll` ; §10.3, phase 6). Les trophées et objets à chance en font partie tels qu'obtenus.
- Structure détruite par un amas ou un ver : les ressources que `Piece.DropResources` aurait rendues sont capturées de la même façon ; les dégâts partiels ne rendent rien.
- Ces quantités sont entières ; elles suivent ensuite la règle 7.3 comme le reste.

### 7.5 Drops natifs (indépendants du pool)

| Drop | Probabilité par ver tué | Clé | Item vanilla |
| --- | --- | --- | --- |
| Appât de pêche | 8 % | `Drops.BaitChance` | un prefab de la famille `FishingBait*`, tiré selon `Drops.BaitWeights` |
| Entrailles | 8 % | `Drops.EntrailsChance` | `Entrails` **\[À VÉRIFIER\]** |
| Sac de sang | 4 % | `Drops.BloodbagChance` | `Bloodbag` **\[À VÉRIFIER\]** |

Les trois tirages sont indépendants ; aucun trophée, aucun item nouveau, aucune icône à créer. **Pondération des appâts par rareté** : au premier lancement, le mod énumère les items dont le prefab commence par `FishingBait` **\[À VÉRIFIER : noms exacts après le Deep North\]** et écrit dans la config `Drops.BaitWeights` une liste `prefab:poids` avec : appât de base (acheté chez Haldor) = 40, appât des Terres brumeuses = 25, tout autre appât = `10 / (1 + T)` arrondi au minimum 2, où `T` = nombre d'ingrédients de la recette qui sont des trophées (les appâts fabriqués avec un trophée de boss ou de mini-boss sont les plus rares). L'utilisateur peut ensuite éditer la liste ; un prefab inconnu dans la config est ignoré avec un avertissement dans le log.

### 7.6 Ce que ne font pas les drops

Pas de drop à l'absorption, à la fusion ni à la dissolution ; pas de drop quand un nid est détruit (il libère ses derniers vers, §8) ; pas de drop pour un ver qui despawn par limite (§8.4) — il n'y a pas de despawn.

## 8. Design — nids, reproduction, plafonds et spawn

Chaque salle d'une mine infestée reçoit un petit nid et quelques vers isolés ; le nid pond tant qu'il vit, jusqu'à 50 vers vivants par nid et 400 par mine, et une mine dont tous les nids sont détruits ne produit plus jamais de ver. Rien de tout cela n'existe hors des mines.

### 8.1 Le nid

| Caractéristique | Valeur | Clé |
| --- | --- | --- |
| Points de vie | 150 | `Nest.Health` |
| Résistances | table §3.2 (le feu nettoie un nid en deux coups de torche) | `Resist.*` |
| Couvée initiale | 8 vers Sleeping à 1–3 m du nid | `Nest.InitialWorms` |
| Vers isolés par salle | 3 (± 1), Sleeping, dispersés dans la salle à ≥ 4 m du nid | `Nest.FreeWormsPerRoom` |
| Ponte | 1 ver Sleeping toutes les 20 s à 1–2 m du nid | `Nest.SpawnInterval` |
| Plafond par nid | 50 vers vivants issus de ce nid (libres ou absorbés dans un amas) | `Nest.MaxAlivePerNest` |
| Destruction | effet et son vanilla réutilisés **\[À VÉRIFIER : vfx/sfx de destruction d'un sac d'œufs de fouineur\]**, libère 5 vers actifs, aucun drop | `Nest.ReleaseOnDestroy` |
| Taille visuelle | monticule ≈ 0,9 m de large, 0,5 m de haut, pulsation lente | — |
| Réveil | §5.1 ; rayon de réveil 10 m | `Nest.WakeRadius` |

- La ponte n'a lieu que lorsque la mine est chargée et qu'un joueur est dans la mine (zone chargée côté propriétaire) : pas de rattrapage du temps écoulé hors ligne. « Si on laisse faire » = rester ou revenir souvent : une mine de 8 nids explorée 25 minutes gagne 100 vers.
- Un nid ne pond pas pendant qu'il est attaqué (3 s après le dernier coup) ni pendant la dormance de son groupe.
- Un nid n'est jamais une cible pour les vers ni pour les créatures vanilla (pas de `Character`, un `Destructible` dans une couche que l'IA vanilla n'attaque pas spontanément **\[À VÉRIFIER : les fouineurs attaquent-ils les `Destructible` non-pièces ? a priori non\]**).

### 8.2 Plafonds

| Plafond | Valeur | Clé | Comportement au plafond |
| --- | --- | --- | --- |
| Vers vivants par nid | 50 | `Nest.MaxAlivePerNest` | le nid arrête de pondre |
| Vers par amas | 200 | `Mass.MaxCount` | l'excédent forme ou garde un amas séparé (§4.1) |
| Vers vivants par mine (libres + absorbés) | 400 | `Colony.MaxAlivePerMine` | aucune ponte, aucune libération ; le harnais peut forcer au-delà uniquement en Debug pour les tests de perf |
| Nids par mine | 24 | `Nest.MaxNestsPerMine` | les salles suivantes n'ont que des vers isolés |

Le compte par nid est tenu via l'origine de chaque ver (`nestIndex`, §11) y compris dans les amas (`nestCounts`). Deux gros amas de 200 sont donc possibles dans une grande mine : c'est l'intention.

### 8.3 Spawn dans les mines infestées

- **Où** : uniquement dans les donjons des mines infestées des Terres brumeuses, identifiés par le thème de leurs salles `Room.Theme.DvergerTown` **\[À VÉRIFIER : nom exact de l'énumération, et emplacements `Mistlands_DvergrTownEntrance1/2`\]**. Jamais dans la citadelle de la Reine (`DvergerBoss`), jamais à l'extérieur, jamais dans un autre biome ou un autre donjon.
- **Quand** : au premier chargement d'une mine, qu'elle ait été générée avant ou après l'installation du mod. Hook : postfix sur `DungeonGenerator.Generate` (génération) et sur `DungeonGenerator.Load` (mine existante rechargée) ; une mine déjà ensemencée porte le drapeau ZDO `mw_seeded = 1` sur la racine du donjon et n'est jamais ensemencée deux fois. Seul le pair propriétaire de la zone ensemence ; les objets créés sont des ZDO persistants, donc visibles par tous et sauvegardés.
- **Comment** : pour chaque salle placée (`m_placedRooms`) dont la surface au sol ≥ `Nest.MinRoomArea` (25 m²) et qui n'est ni l'entrée ni une pièce « de passage » (couloir, escalier : surface < seuil), choisir un point de sol par raycasts vers le bas depuis la boîte de la salle (12 essais max, point à ≥ 1,5 m des murs, ≥ 2 m des `CreatureSpawner`/objets existants, pente < 20°) ; y poser un `MW_WormNest`. Puis 3 ± 1 vers isolés par salle, nid ou pas, par la même méthode. Si aucun point valide : pas de nid dans cette salle.
- Un seul `MW_WormColony` par mine, posé à la racine du donjon, qui porte la simulation de tous les vers de la mine (§10). Les nids s'y enregistrent avec un index stable (ordre de création).
- Les spawns vanilla de la mine (fouineurs, tiques, couvains, soldats) sont **inchangés** : les vers s'ajoutent.

### 8.4 Fin de vie et nettoyage

- Mine propre = tous les nids détruits ; les vers restants vivent jusqu'à leur mort, aucun nouveau n'apparaît, la colonie reste pour les persister puis se supprime quand elle est vide.
- Aucun despawn par distance ou par temps ; aucun événement de raid ; aucune migration hors de la mine (les vers sont clampés à la boîte englobante du donjon + 2 m, et un ver qui tombe hors de la boîte est détruit sans drop).

## 9. Design — feu, résistances et interactions diverses

Le feu est la contre-mesure voulue : il fait fuir les vers et les brûle deux fois plus, si bien qu'une torche en main suffit à traverser une salle, au prix d'un bouclier qu'on ne tient pas.

### 9.1 Feu

- **Peur** (`Fire.AfraidOfFire = true`) : un ver ou un amas actif fuit toute source de feu à moins de `Fire.FearRadius` (4 m) : zone d'effet `EffectArea` de type feu (feu de camp, brasero, torches murales allumées) ou arme/objet de feu tenu par un `Humanoid` (torche, exactement selon la logique vanilla des grisâtres, `BaseAI.m_afraidOfFire` / `AvoidFire` réutilisée pour l'amas, reproduite pour les vers virtuels) **\[À VÉRIFIER : méthode et critère exacts dans `BaseAI` 1.0\]**.
- Un amas englouti qui subit la peur se détache et recule hors du rayon ; un amas qui reçoit des dégâts de feu fuit pendant `Fire.FearDuration` (3 s, phase 5) même sans source persistante ; un ver, lui, fuit `Worm.FleeDuration` (4 s, §3.3).
- Fuir n'empêche pas de brûler : la zone de feu continue d'infliger ses dégâts vanilla, multipliés par la faiblesse (× 2).
- Le Wisplight n'est pas une source de feu. Les lampes dvergr des mines non plus.

### 9.2 Interactions

| Situation | Comportement |
| --- | --- |
| Furtivité | Un joueur accroupi non détecté passe : vue et ouïe vanilla (§3.1), bruit du joueur pris en compte ; les vers dormants ne voient qu'à 4 m |
| Lumière | Aucun effet (ni le Wisplight ni une torche n'attirent) |
| Apprivoisés | Cibles valides ; un loup englouti prend des dégâts PV perçants, et riposte (vanilla) |
| Autres joueurs | Un joueur peut frapper l'amas collé à un coéquipier ; les dégâts de zone du joueur suivent la règle JcJ vanilla |
| Mort du joueur | Mort vanilla ; la pierre tombale n'est jamais ciblée ; l'amas choisit une nouvelle cible |
| Déconnexion / changement de propriétaire | La colonie et les amas changent de propriétaire selon les règles ZDO vanilla ; l'état est repris du dernier instantané (§11) |
| Modificateurs de monde | Les coups agrégés passent par `HitData` vanilla : les curseurs de difficulté de combat s'appliquent aux PV ; `Engulf.ScaleWithWorldModifiers` (défaut `true`) les applique aussi aux points de durabilité |
| Sommeil, repos, abri | Aucune interaction spécifique |
| Portails, bateaux, charrettes | Sans objet dans une mine |
| Sauvegarde / chargement | Tout l'état (vers, amas, pools, nids, compteurs) survit à une sauvegarde, un redémarrage de serveur et un changement de propriétaire (§11.4) |
| Mods tiers (CLLC, EpicLoot…) | Non supportés en v1 ; le mod ne doit pas planter s'ils sont présents : l'amas est créé au niveau 1 à chaque spawn, les drops de pool sont des `ItemDrop` vanilla |
| Console `killall` / `removedrops` | `killall` tue les amas (créatures) ; la commande Debug `mw_kill` tue aussi les vers virtuels et les nids |

### 9.3 Garde-fous

- Un ver ou un amas ne quitte jamais la boîte du donjon (§8.4).
- Un amas dont la cible devient invalide (téléportée, déconnectée, morte) relâche sa cible dans la seconde.
- Toute erreur dans la simulation d'une colonie est journalisée et désactive cette colonie (vers figés, aucun drop) plutôt que de faire planter le client ; le harnais traite tout `Exception` du mod comme un échec de scénario.

## 10. Architecture technique

Le principe qui porte toute l'architecture : **un ver n'est pas une créature**. Une mine entière tient dans un seul objet réseau, la colonie, qui simule ses 400 vers en tableaux ; seuls les amas sont de vraies créatures vanilla, et seuls les nids sont de vrais destructibles. Les vers ne possèdent qu'un collider de proxy, sans `ZNetView` ni `Character`, pour être frappés.

&#91;embedded content: architecture · 1 colonie par mine, amas = créatures, vers = tableaux + proxies\]

Le pair propriétaire simule la colonie et ses amas ; chaque morsure voyage comme une `HitData` vanilla jusqu'au client de la cible, qui renvoie ce qui a été dévoré ; tous les clients ne font que dessiner, jouer les sons et tenir les colliders des vers.

### 10.1 Entités et composants

| Entité | Prefab / objet | Composants | Rôle |
| --- | --- | --- | --- |
| Colonie | `MW_WormColony` (un par mine, à la racine du donjon, ZDO persistant) | `ZNetView`, `WormColony` | Tableaux SoA des vers virtuels ; simulation côté propriétaire ; registre des nids ; état de réveil par groupe ; plafonds ; instantanés et RPC ; fournit les instances à rendre |
| Ver virtuel | aucun GameObject | entrée `i` des tableaux `pos[]`, `heading[]`, `state[]`, `timer[]`, `hp[]`, `nestIndex[]`, `target[]`, `poolIndex[]` | Toute la logique de §3 ; capacité fixe 400 + marge Debug (réservée au démarrage, aucune allocation en jeu) |
| Proxy de ver | GameObject poolé (pool de 420), sans `ZNetView` | `SphereCollider` trigger r = 0,18 m sur une couche touchée par les attaques **\[À VÉRIFIER : `character` est touché par `Attack`, `Aoe` et les projectiles vanilla, et un trigger n'entrave pas le joueur\]**, `WormProxy : IDestructible` | Reçoit `Damage(HitData)` → route vers la colonie (local si propriétaire, sinon RPC) ; attaché aux vers rendus à moins de 40 m d'un joueur, suit leur position ; les sphères de coup de l'amas (dôme et coulée, §4.5, §4.7) suivent le même principe de routage |
| Amas | `MW_WritheMass` cloné d'un rampant vanilla **\[À VÉRIFIER : `SeekerBrood`\]** via Jötunn `PrefabManager.CreateClonedPrefab` + `CreatureManager` | `ZNetView`, `ZSyncTransform`, `Character`, `Humanoid` (requis par `MonsterAI`), `WritheMassAI : MonsterAI`, `WritheMassState`, `WritheMassVisual`, `Rigidbody`, `CapsuleCollider` (r ≤ 0,6), `Animator` + `ZSyncAnimation` vanilla conservés mais renderers désactivés (évite les `NullReferenceException` de `Character`) | Créature vanilla à part entière : visée par les IA, barre de PV, mort, messages ; logique de §4–§6 ; zone de coup = sphères sur la couche `hitbox` routées vers l'amas (§4.5) |
| Nid | `MW_WormNest` cloné d'un destructible vanilla **\[À VÉRIFIER : sac d'œufs de fouineur ou autre `Destructible` simple\]** | `ZNetView` (persistant), `Destructible`, `WormNest`, visuel custom | Ponte, couvée, compteur par nid, alerte, libération à la destruction |
| Effet Infesté | `MW_SE_Infested` (Jötunn `CustomStatusEffect`) | `StatusEffect` avec `ModifySpeed` | §6.2 |
| Rendu | `WormRenderer` (singleton client) | `Graphics.RenderMeshInstanced` / repli (§12) | Dessine tous les vers, dômes, coulées et vers de surface visibles, toutes colonies confondues |
| Assets | `AssetCatalog`, `GltfLoader`, `TextureLoader` | — | Chargement des `.glb` et `.png` du dossier du plugin au démarrage (§13) |
| Patches | `DamagePatches`, `DropPatches`, `SpawnPatches`, `HudPatches`, `AiPatches`, `SavePatches` | HarmonyX | §10.3 |
| Dev | `QuickStart`, `DevBridge`, `Scenarios`, `PerfProbe` (`#if MW_DEV`) | — | §15 |

### 10.2 Qui décide quoi

- **Propriétaire de la colonie** (pair qui possède le ZDO, règle vanilla du plus proche) : simule les vers, crée et détruit les amas et les nids, applique les morts de vers, gère les pools des vers libres, émet les instantanés.
- **Propriétaire d'un amas** (son ZDO) : IA, morsures (émet les `HitData`), compteur `n`, pool de l'amas, fusion. En pratique le même pair que la colonie la plupart du temps ; le code ne le suppose jamais (toute écriture passe par « si propriétaire, écrire, sinon RPC au propriétaire »).
- **Propriétaire de la cible** (le client du joueur mordu) : blocage, choix des pièces, durabilité, calcul de ce qui est entré dans le pool → `RPC_Consumed` vers l'amas.
- **Tous les clients** : rendu, sons, effets, interpolation, `Physics.IgnoreCollision` entre amas et cible engloutie.
- **Serveur dédié** : n'est propriétaire de rien de tout cela (comme en vanilla pour les créatures) ; il impose la config (§14) et refuse les clients sans le mod (Jötunn `NetworkCompatibility`, `CompatibilityLevel.EveryoneMustHaveMod`, `VersionStrictness.Minor`).

### 10.3 Patches Harmony

| Cible | Type | Rôle |
| --- | --- | --- |
| `DungeonGenerator.Generate` et `DungeonGenerator.Load` | postfix | Ensemencement des mines (§8.3), une fois, drapeau `mw_seeded` |
| `HitData.ApplyArmor` | prefix (skip) | L'armure n'atténue jamais une morsure (§6.4) |
| `Character.ApplyDamage` | prefix | Si l'attaquant est un amas ou un ver et la cible un joueur avec des objets éligibles : convertir les dégâts restants en points de durabilité, afficher le texte « Équipement −P », notifier l'amas, et ne pas appliquer aux PV |
| `Character.Stagger`, `Character.AddStaggerDamage` | prefix (skip) pour `MW_WritheMass` | Jamais d'étourdissement (§4.8) |
| `HitData.m_pushForce` (point exact choisi au dump) | prefix | Recul de l'amas × `clamp(10/n, KnockbackMinFactor, 1)` (§4.8) |
| `Character.GetHoverName` | postfix pour `MW_WritheMass` | Nom avec compteur (§4.3) |
| `CharacterDrop.GenerateDropList` (instance) + chemin ragdoll | prefix/postfix | Capture des drops de créatures tuées par l'amas (§7.4, phase 6), via un drapeau thread-static posé quand `m_lastHit.m_attacker` est nôtre ; l'effet de mort de l'amas n'a pas de `Ragdoll` |
| `WearNTear.Damage` + `WearNTear.Destroy` + `Piece.DropResources` | prefix/postfix | Mémoriser le dernier attaquant par pièce ; capturer les ressources d'une pièce détruite par l'amas (§7.4) |
| `BaseAI.IsEnemy` | aucun pour l'amas (faction `PlayerSpawned`, §5.4) | Le harnais imprime la matrice pour vérifier |
| `ZNet.SaveWorld` | prefix | Forcer un instantané des colonies avant la sauvegarde (§11.4) |
| `Game.Start` / `FejdStartup` | postfix (`#if MW_DEV`) | QuickStart (§15) |

Aucun transpiler : des préfixes et postfixes uniquement, chacun protégé par un `try/catch` qui journalise et laisse passer le comportement vanilla en cas d'erreur. Chaque patch a un test dans le harnais qui vérifie qu'il s'applique (Harmony `GetPatchedMethods`) et qu'il produit l'effet attendu.

### 10.4 Compatibilité avec SkyWhale

L'utilisateur demande que MistWorms et SkyWhale tournent ensemble sur le même serveur. Deux règles suffisent, aucune `Priority` ni `Before`/`After` Harmony n'est nécessaire :

1. un patch ne modifie le résultat que si une créature ou un objet du mod est en jeu ;
2. un prefix ne renvoie jamais `false` sur un objet qui n'est pas au mod.

- **Patchs communs** : `Character.ApplyDamage` et `BaseAI.IsEnemy`. SkyWhale patche la surcharge statique `BaseAI.IsEnemy(Character a, Character b)` : `false` si `a` est la baleine, ou si `b` est la baleine et `a` n'est pas un `Player`. Nos vers ne sont pas des `Player` ; l'exclusion par prefab (ci-dessous) tient.
- **Noms** : préfixes de prefab `MW_` pour nous, `SW_` pour SkyWhale ; clés ZDO `mw_*` pour nous, `sw_*` pour SkyWhale.
- **Couches** : la coque, les nageoires et la capsule racine de la baleine sont sur `hitbox` (18), partagée avec nos proxys ; son pont est sur `vehicle` (28), ses triggers sur `character_trigger`. Nos requêtes physiques n'utilisent pas `hitbox` (`SolidMask` et `ViewBlockMask` sont les masques vanilla de `BaseAI`) ; toute requête future sur `hitbox` doit filtrer sur `WormProxy`. Le pont `vehicle` est dans `SolidMask` et `ViewBlockMask`, comme un bateau pour les créatures vanilla : accepté. De son côté, SkyWhale filtre aussi sur ses propres composants ses requêtes sur `hitbox`.
- **Exclusion de cible** : `Targets.ExcludedPrefabs` (défaut `SW_DivineWhale`, §14) ; ces prefabs ne sont jamais ciblés par les vers ni par les amas (§5.3). Phase 6, plus la sélection de cibles de l'amas en 4B.
- **Crash de la baleine** : il passe par le `Damage()` vanilla et touche donc nos proxys normalement.
- **Copies de dev** : `C:\ValheimDev\` et le serveur local sont partagés entre les deux projets ; on les prend chacun son tour.

## 11. Simulation, réseau et persistance

Budget sur le pair propriétaire, mine entière à 400 vers actifs : **≤ 1,0 ms par frame** de simulation en moyenne, 3 ms au pire, zéro allocation GC en régime stable ; sur les autres clients ≤ 0,3 ms ; ≤ 6 Ko/s de réseau par colonie active, ≈ 0 en dormance. Ces chiffres sont mesurés par le harnais (scénario S11, §15) et font partie des critères d'acceptation.

### 11.1 Simulation des vers (propriétaire)

- Tableaux SoA préalloués (capacité `Colony.MaxAlivePerMine` + 50 en Debug) ; liste libre des slots ; aucun `new` dans la boucle, aucune LINQ, aucune `string` ; `Physics.RaycastNonAlloc` / `OverlapSphereNonAlloc` avec buffers statiques.
- **Cadence par niveau de détail** (`Performance.SimRateNear` 20 Hz à moins de 30 m d'un joueur, `Performance.SimRateFar` 5 Hz au-delà, 2 Hz pour les dormants) ; la boucle est découpée en tranches par frame (`i % slices == frame % slices`) pour lisser le coût. Ce sont les cadences du **propriétaire** de la colonie qui s'appliquent, quelle que soit la configuration locale des autres pairs.
- **Déplacement** : direction désirée = somme pondérée de (objectif, évitement d'obstacles par 3 raycasts horizontaux avant/±35° réévalués tous les 0,25 s, séparation, cohésion et alignement avec les voisins (grille spatiale 2 m, détail ci-dessous), places d'approche et file indienne (§3.3), bruit d'errance) ; projection au sol par raycast vers le bas tous les 0,1–0,25 s ; pas de `Rigidbody`, pas de `CharacterController`.
- **Séparation, cohésion, alignement** (constantes nommées, pas des clés de config) : la séparation prend pour point de référence le milieu du corps (0,15 m derrière la tête), rayon 0,3 m, 6 voisins, poids 1,0 (cible : plus de chevauchement visible, S18) ; cohésion légère en errance, poids 0,2 vers le centre des voisins à moins de 3 m ; alignement en Gathering et en Hunting, poids 0,3 vers le cap moyen des voisins de même état.
- **Pathfinding** : pas de chemin par défaut (les salles sont ouvertes). Un ver qui n'a pas progressé de 0,5 m en 3 s demande un chemin vanilla (`Pathfinding.instance.GetPath`, agent le plus petit) ; budget global 8 requêtes/s par colonie, cache 10 s, suivi par points de passage. Les amas utilisent le pathfinding vanilla de `MonsterAI` en permanence.
- **Perception** : un test de visibilité par ver et par 0,5 s au plus (raycast vers le joueur le plus proche), cible choisie selon §5.3 ; les joueurs sont lus une fois par tick depuis `Player.GetAllPlayers()`, les créatures depuis `Character.GetAllCharacters()` filtrées par distance via la grille.
- **Formation/absorption** : la grille spatiale sert aussi à trouver les groupes (§4.1) ; une passe toutes les 0,25 s.
- **Coup de cobra** : sphère de 0,5 m devant la tête, cible = `Character` ou `WearNTear` le plus proche ; émet une `HitData` vanilla (attaquant = la colonie, avec un marqueur « ver » ; le `m_attacker` est le ZDOID de la colonie, dont le `Character` est absent : les patches identifient un attaquant « nôtre » par ZDOID enregistré, pas par composant **\[À VÉRIFIER : `HitData.GetAttacker()` tolère un ZDOID sans `Character`, sinon créer un `Character` fantôme désactivé sur la colonie\]**).

### 11.2 IA de l'amas

`WritheMassAI : MonsterAI`, `UpdateAI` surchargé : (1) si dormant → immobile ; (2) peur du feu via la logique vanilla ; (3) cible selon §5.3 (réutilise `FindEnemy`, `FindClosestStaticPriority`-équivalents et `CanSeeTarget`) ; (4) si cible à portée d'engloutissement → état Engulf (phase 5 ; en phase 4B l'amas s'arrête au contact, état Hunt, sans engloutir, §4.10), sinon `MoveTo(cible)` via pathfinding ; (5) en Engulf, position = cible + retard, tick de morsure (§6.1), conditions de détachement (§4.9) ; (6) hors cible, errance / retour au nid (§4.6) ; (7) sondage de largeur pour la coulée (§4.7). Jamais `DoAttack`, jamais d'`Attack` vanilla, jamais d'animation vanilla.

### 11.3 Réseau

| Canal | Contenu | Cadence |
| --- | --- | --- |
| ZDO colonie `mw_snap` (byte\[\]) | instantané complet : par ver `pos` (3 × int16, 2 cm, relatif à l'origine du donjon), `heading` (byte), `state` (byte), `hp` (byte = `hp / Worm.Health × 255`, soit la fraction de vie quelle que soit la config), `nestIndex` (byte) = 9 octets ; + nids (index, vivants) + groupes (actif/dormant) + pools des vers libres (liste compacte) | toutes les 5 s si modifié, avant sauvegarde, au changement de propriétaire |
| RPC colonie `RPC_Delta` (ZRoutedRpc vers les pairs à moins de 120 m) | vers modifiés depuis le dernier envoi : index + pos + heading + state ; morts (index) ; naissances (index + nestIndex) | 5 Hz (`Network.SyncRate`) tant qu'un ver actif existe ; rien en dormance |
| RPC colonie `RPC_WormHit(index, dégâts, attaquant, point)` | coup reçu par un proxy sur un client non propriétaire | à l'événement |
| ZDO amas | `mw_n` (int), `mw_partial` (float), `mw_pool` (byte\[\] : liste prefab-hash + float), `mw_nests` (byte\[\]), `mw_state` (byte : Idle/Hunt/Engulf/Dormant, §4.10) + un bit `Stream` séparé (forme, pas état), `mw_target` (ZDOID) + `ZSyncTransform` vanilla | à la modification (ZDO vanilla) |
| RPC amas `RPC_Consumed(list)` | depuis le client de la cible mordue : ce qui est entré dans le pool | à chaque tick de morsure ayant mangé quelque chose |
| RPC amas `RPC_Absorb(index)` / colonie `RPC_Dissolve(n, pool)` | transferts ver ↔ amas ; le pool de `RPC_Dissolve` voyage dans un `ZPackage` (`ZRoutedRpc` ne sait pas sérialiser `byte[]`) | à l'événement |

- Sur les clients non propriétaires, les vers continuent à cap et vitesse constants entre deux deltas (dead reckoning) puis sont recalés par interpolation sur 0,2 s ; les animations (phase du flipbook) sont locales et jamais synchronisées.
- Un pair qui devient propriétaire repart de `mw_snap` ; les deltas sont idempotents (état absolu, pas de différentiel).
- Taille : 400 vers actifs × 9 o × 5 Hz = 18 Ko/s au pire théorique ; en pratique seuls les vers qui bougent sont envoyés (les absorbés et dormants ne le sont pas), objectif ≤ 6 Ko/s ; le harnais mesure.

### 11.4 Persistance

- Tout vit dans des ZDO persistants : colonie (`mw_snap`, `mw_seeded` sur la racine du donjon), amas, nids. Une sauvegarde serveur ou un redémarrage restitue le même nombre de vers, les mêmes amas (n, pool, origines), les mêmes nids (vivants, timer de ponte).
- `ZNet.SaveWorld` (prefix) force l'écriture de `mw_snap` par le propriétaire avant la sauvegarde ; les non-propriétaires n'écrivent jamais.
- Désinstallation du mod : les ZDO orphelins (`MW_*`) sont ignorés par le jeu vanilla comme ceux de tout mod retiré (prefab inconnu → objet non instancié) ; aucune corruption.
- Changement de version du format `mw_snap` : un octet de version en tête ; une version inconnue → colonie réinitialisée (vers dormants autour des nids, pools vidés) avec avertissement dans le log.

## 12. Rendu, matériaux, audio et effets

Tous les vers d'une mine sont dessinés en quelques appels instanciés à partir de maillages pré-animés (flipbook), sans `Animator`, sans `SkinnedMeshRenderer`, sans GameObject par ver ; l'amas est un dôme animé couvert de vers instanciés. Budget client : ≤ 0,5 ms CPU par frame et ≤ 24 appels de rendu pour les vers, ≤ 3 par amas.

### 12.1 Flipbook instancié

- Chaque animation est une série de maillages statiques exportés par Blender (§13) : `worm_crawl_00..15` (boucle de reptation, 16 images à 12 i/s), `worm_idle_00..07` (au repos, 8 images à 6 i/s, boucle), `worm_rear_00..05` (se dresser), `worm_strike_00..03` (frappe), `worm_sleep_00..03` (pulsation lente, 4 i/s), `mass_boil_00..07` (ébullition du dôme, 8 i/s), `nest_idle` (1 image + pulsation d'échelle en code).
- Reptation : le cycle `crawl` avance avec la distance parcourue : 12 images par `Worm.MoveSpeed` mètre. L'ondulation est serpentine et péristaltique, toutes deux bien marquées (valeurs en §13.1).
- Repos : un ver immobile depuis plus de 0,3 s joue `worm_idle` (la tête balaie ±15°, le corps se tortille faiblement) ; plus de pose figée au repos.
- Par frame : les vers visibles sont regroupés par (animation, image) et chaque groupe est dessiné par un `Graphics.RenderMeshInstanced` (ou `DrawMeshInstanced` selon l'API disponible dans la version d'Unity du jeu **\[À VÉRIFIER : version Unity de Valheim 1.0, « engine upgrade » de février 2026\]**) avec une matrice TRS par instance (position, cap, roulis ±8°, échelle 1 ± 0,1). Pas de teinte par instance : `RenderMeshInstanced` n'a pas de `MaterialPropertyBlock` par instance (décision 30).
- Culling : frustum de la caméra (boîte englobante par ver) et distance `Performance.RenderDistance` (60 m) ; les vers dormants hors champ ne coûtent rien.
- Ombres : projetées (`ShadowCastingMode.On`) jusqu'à 25 m, désactivées au-delà ; reçues toujours.
- L'amas : dôme `mass_boil_*` à l'échelle `r(n)` lissé sur 0,3 s, §4.5 (hauteur × 0,8, ou aplati en mode coulée §4.7) + `min(n, Performance.MaxVisibleSurfaceWorms = 40)` vers de surface instanciés, positionnés sur une spirale de Fibonacci autour du dôme et glissant lentement le long de celui-ci (déphasage par instance, orientation tangente au mouvement de l'amas) ; en mode coulée, répartis le long du segment de coulée.
- Interpolation : toutes les positions rendues sont lissées entre deux ticks de simulation (propriétaire) ou deux deltas (clients).

### 12.2 Matériaux et shaders

- Les matériaux sont construits en code avec le shader `Custom/Creature` **récupéré en premier sur le matériau d'un prefab vanilla** (`Tick`, puis `SeekerBrood`, décisions 20, 28) ; `Shader.Find` et le scan de `Performance.ShaderCandidates` ne sont que des replis de configuration, journalisés ; les propriétés (`_MainTex`, `_BumpMap`, lustre) sont lues sur ce matériau au dump, avec nos textures (§13). Densité de texels, filtrage et lustre du ver : §13.4.
- Décision à l'exécution, journalisée : (1) si le shader supporte l'instancing (`material.enableInstancing = true` accepté et un rendu de test sans erreur) → instancing ; (2) sinon, essai de la liste `Performance.ShaderCandidates` (`Custom/Creature`, `Custom/Piece`, `Standard`, `Legacy Shaders/Diffuse`) ; (3) sinon **repli** : fusion CPU des instances par groupe (`Mesh.CombineMeshes` dans des maillages dynamiques réutilisés, ≤ 8 appels) — plus coûteux mais fonctionnel, et le harnais vérifie que le budget tient.
- Dôme : même shader, texture dédiée, rendu double face. Quand la caméra est à moins de `r + 0,7 m` du centre, l'échelle du dôme est ramenée à 0,6 (§6.6, phase 4B). Le dither d'alpha n'est fait que si un shader vanilla le permet (à vérifier ; sinon abandonné, §18).
- Aucun shader custom embarqué (pas d'AssetBundle) ; aucune texture au-delà de 1024², format PNG chargé via `ImageConversion.LoadImage`, mipmaps activées, compression à l'exécution (`Texture2D.Compress`).

### 12.3 Audio (sons vanilla uniquement)

| Événement | Son | Limite |
| --- | --- | --- |
| Reptation / idle d'un ver proche | son d'idle d'un rampant des mines (couvain ou tique) à faible volume, pitch aléatoire 0,8–1,2 | 4 sources simultanées max (`Performance.MaxSoundSources`), vers à moins de 12 m |
| Coup de cobra | son d'attaque de la tique | 1 par coup |
| Coup reçu par un ver | son de touche d'un petit rampant (tique), choisi au dump | 1 par coup |
| Mort d'un ver | son d'écrasement / mort d'un petit rampant | 1 par 0,1 s max |
| Amas (boucle) | idle de couvain en boucle, 3 couches déphasées, volume ∝ log(n) | 1 boucle par amas, 3 amas audibles max |
| Engloutissement (début) | son de morsure de tique + son humide | 1 |
| Nid (idle, destruction) | pulsation sourde ; destruction = son du sac d'œufs de fouineur | 1 |

Phasage : phase 4A pour les sons du ver seul (idle/reptation, cobra, coup reçu, mort) ; phase 4B pour la boucle de l'amas ; phase 5 pour l'engloutissement ; phase 7 pour le nid.

Les noms exacts des prefabs `sfx_*` sont pris au dump (§20) et centralisés dans `VanillaNames`. Les sons passent par `ZSFX` vanilla instanciés depuis les prefabs du jeu (jamais d'`AudioClip` embarqué), avec le mixer et les réglages de volume vanilla.

### 12.4 Effets

- Coup sur un ver (phase 4A) : petit jet de sang vert-jaune acide, jet court d'environ 0,3 m, 6 à 12 particules, durée ≤ 0,5 s (c'est un petit mob, l'effet reste petit). Joué chez le pair qui porte le coup (là où le proxy est frappé), immédiatement ; les autres clients le reçoivent en phase 8 (via `RPC_Delta`).
- Mort d'un ver : le même sang vert en effet de mort, plafonné à 10 instances par seconde par colonie (plafond commun aux coups et aux morts, inchangé).
- Source des effets verts : d'abord un effet vanilla déjà vert, à chercher au dump (fouineur, couvain, blob, ooze…) ; sinon un clone unique, créé au chargement, de `fx_TickBloodHit` / `fx_tick_death`, recoloré (`ParticleSystem.main.startColor`, couleur du matériau des particules), sans `ZNetView` (effet local, purement visuel). Le template est mis en cache : jamais de recoloration par coup.
- Transitions de l'amas (rendu seulement, la logique reste celle de §4) :
  - rayon lissé : le rayon affiché suit `r(n)` avec un lissage de 0,3 s, jamais de saut (§4.5) ;
  - formation : les vers convergent vers le centroïde (0,4 s), puis le dôme pousse de r = 0 à r(n) en 0,5 s (ease-out) pendant qu'ils s'y enfoncent (§4.1) ;
  - absorption : état Absorbing, 0,5 s (§3.3, §4.1) ;
  - fusion : le plus petit amas glisse vers le plus gros, son dôme se résorbe en 0,5 s au contact, ses vers de surface glissent sur le gros dôme (§4.1) ;
  - dissolution : le dôme se résorbe en 0,4 s, les `n` vers jaillissent en un petit bond avant Fleeing (§4.4) ;
  - mort d'un ver dans l'amas : un ver de surface se détache, glisse et tombe, puis l'effet de mort vert se joue (§4.2) ;
  - coulée : transition dôme ↔ ruban en 0,5 s, vers de surface répartis le long du ruban (§4.7).
- Engloutissement : aucun effet écran ; l'icône de statut et le dôme suffisent.
- Nid détruit : effet de destruction vanilla réutilisé + 5 vers qui sortent.

## 13. Pipeline d'assets (Blender headless → glTF → runtime)

Claude Code fabrique tous les assets lui-même avec des scripts Blender versionnés, lancés en mode `--background` ; le jeu les charge à l'exécution avec un petit loader glTF maison. Pas d'Unity Editor, pas d'AssetBundle, pas de fichier binaire fait à la main : `tools/build-assets.ps1` doit pouvoir tout régénérer depuis zéro.

### 13.1 Scripts (`assets/blender/`)

| Script | Produit | Détails |
| --- | --- | --- |
| `common.py` | utilitaires | export glTF binaire (`.glb`, triangles, normales, UV, sans squelette, sans animation), bake de textures procédurales en PNG, seed fixe pour un résultat déterministe |
| `make_worm.py` | `worm_crawl_00..15.glb`, `worm_idle_00..07.glb`, `worm_rear_00..05.glb`, `worm_strike_00..03.glb`, `worm_sleep_00..03.glb`, `worm_albedo.png`, `worm_normal.png` (128 × 64 ou 128², filtrage `Point`, §13.4 ; remplace 512²), `worm_gloss.png` (carte de lustre) | Corps en 14 segments le long de +Z, profil de rayon (tête 0,035 m, corps 0,03 m, queue 0,012 m), 600–900 triangles, longueur 0,40 m, origine au centre de masse au sol. Reptation (16 images) : ondulation latérale serpentine (amplitude 0,02 m à la tête, 0,045 m à la queue, longueur d'onde 0,35 m : un S complet visible sur le corps) + onde péristaltique (rayon ± 25 %, longueur d'onde 0,13 m) avec étirement longitudinal ± 6 % en phase. Repos (8 images) : la tête balaie ± 15°, le corps se tortille faiblement. « Se dresser » : les 6 segments avant montent le long d'un arc jusqu'à 0,28 m de haut ; frappe : fente avant de 0,15 m ; sommeil : pulsation ± 5 %. Texture « pâle de caverne » (§13.4), aucune référence à un asset existant |
| `make_mass.py` | `mass_boil_00..07.glb`, `mass_albedo.png` 1024², `mass_normal.png` 1024² | Demi-sphère subdivisée (≈ 1 500–2 500 triangles) de rayon 1 et de hauteur 1, origine au centre de la base, jupe jusqu'à y = −0,05 (décision 25 ; échelle `(r, 0,8 r, r)` en jeu), déplacement par bruit multicouche + crêtes vermiformes ; 8 images à offset de bruit croissant pour l'ébullition, bouclables. Texture plus sombre, veinée de rouge, très spéculaire |
| `make_nest.py` | `nest_idle.glb`, `nest_albedo.png` 1024², `nest_normal.png` | Monticule de mucus 0,9 × 0,9 × 0,5 m avec grappes d'œufs translucides, ≈ 2 000 triangles |
| `render_icon.py` | `thunderstore/icon.png` 256², `thunderstore/banner.png` 1024×256 | Rendu Eevee du dôme et de quelques vers sur fond sombre brumeux |
| `validate_assets.py` | `assets/build/manifest.json` | Vérifie dimensions, nombre de triangles, présence des images attendues ; écrit le manifeste lu par `AssetCatalog` |

Lancement : `& "$env:ProgramFiles\Blender Foundation\Blender 4.x\blender.exe" --background --python assets/blender/make_worm.py` (le script d'orchestration `tools/build-assets.ps1` enchaîne les cinq). Blender n'est jamais piloté par MCP pour le build final ; un MCP Blender peut servir à l'exploration, mais tout ce qui compte est dans les scripts.

### 13.2 Conventions d'échelle et d'axes

- Unités Blender = mètres ; export glTF avec `+Y up` (par défaut) ; le loader convertit vers Unity (main gauche) : `x → −x` et inversion de l'ordre des sommets de chaque triangle.
- Avant du ver = `+Z` glTF (devient `+Z` Unity, « forward ») ; origine au sol pour le ver et le nid, au centre de la base pour le dôme (demi-sphère, jupe jusqu'à y = −0,05, décision 25).
- Les images d'un flipbook ont exactement la même topologie (même nombre de sommets et d'indices) : cela permet au loader de ne charger les indices qu'une fois et d'interpoler deux images adjacentes côté CPU si le budget le permet (option `Performance.FlipbookBlend`, défaut `false`).

### 13.3 Loader runtime (`src/MistWorms/Assets/`)

- `GltfLoader` : lit un `.glb` (en-tête 12 octets, chunk JSON, chunk BIN), supporte uniquement : un ou plusieurs `meshes[].primitives[]` en mode triangles, accesseurs `POSITION` (float3), `NORMAL` (float3), `TEXCOORD_0` (float2), indices `UNSIGNED_SHORT` ou `UNSIGNED_INT`, `byteStride` optionnel ; tout le reste (skins, animations, matériaux glTF, Draco, textures intégrées) est ignoré ou refusé avec un message clair. ≈ 300 lignes, pur C# + `UnityEngine.Mesh` à la fin ; la partie parsing est testée hors du jeu (xunit, §15.6) avec les `.glb` du dépôt.
- `TextureLoader` : PNG → `Texture2D` (sRGB pour l'albédo, linéaire pour les normales, `linear = true`), mipmaps, `Compress(false)`.
- `AssetCatalog` : charge tout au démarrage du plugin (`Awake`, avant Jötunn `OnVanillaPrefabsAvailable`) depuis le dossier du plugin (`Path.GetDirectoryName(Info.Location)/assets`), construit les `Mesh` (`MarkDynamic` non, `UploadMeshData(true)` pour libérer la copie CPU sauf en mode blend), expose `Mesh[] WormCrawl`, etc., et loggue le temps de chargement (objectif < 300 ms, mesuré à 371 ms en phase 2) ; les nouvelles images (crawl 16, idle 8) ne doivent pas l'aggraver, à remesurer en phase 4A.
- Un asset manquant ou invalide désactive le mod proprement (log d'erreur, pas de spawn, pas de plantage).

### 13.4 Direction artistique (en une ligne par asset)

- **Ver** (« pâle de caverne ») : larve-asticot musculeuse plutôt que ver de terre. Peau blanc-jaunâtre un peu translucide (simulée dans l'albédo), veines vert acide visibles sous la peau, plis des anneaux plus sombres (gris-olive), tête et mandibules ambre-brun. Très luisant et gluant : le corps renvoie franchement la lumière des torches et des lampes dvergr. Style Valheim : silhouette low-poly lisible (anneaux nets), texture d'aspect peint à la main, grandes formes plutôt que bruit fin. **Même densité de texels et même mode de filtrage que les créatures vanilla** (dump du 24 oct. 2026) : `_MainTex` en 64² pour `SeekerBrood` (`babyseeker_d`) et `Tick` (`Feasting_d`), en 128² pour `Leech` (`swampfish_d`) et le blob, en 256² seulement pour `Seeker` et `SeekerBrute` ; toutes en filtrage `Point`, avec mipmaps, compressées DXT5 / BC7. Le ver prend donc une texture de 128 × 64 ou 128², `filterMode = Point`, mipmaps, comme `Leech` et le blob ; pas de bruit fin. Lustre : texture séparée `worm_gloss.png` (`_MetallicGlossMap` : RGB 0 = non métallique, A = lustre) avec `_UseGlossmap` 1 et le mot-clé `_USEGLOSSMAP_ON`, comme `Tick` (`Feasting_m`, alpha moyen 150/255, max 238) et `SeekerBrood`. Valeurs de départ : lustre ≈ 0,9 sur le corps, ≈ 0,5 dans les plis, `_Glossiness` 0,85 (repères vanilla : 0,54 `Leech`, 0,55 `Seeker`, 0,76 `SeekerBrood`, 0,75 à 0,93 pour les blobs sous `Custom/Blob`), à ajuster d'après V07 ; si la carte n'a aucun effet en jeu, lustre constant 0,85 et reflets peints dans l'albédo (plus discrets qu'aujourd'hui). Captures Blender (preview) : rugosité cohérente avec le lustre réel en jeu.
- **Amas** : une masse qui semble faite de milliers de vers (crêtes, boucles, pointes de têtes qui affleurent), humide, veinée de sombre, jamais une « boule » lisse.
- **Nid** : mucus brunâtre, grappes d'œufs ambrés translucides, quelque chose entre un sac d'œufs et une flaque de glu.
- Le harnais (§15) prend des captures de chaque asset à l'échelle du joueur ; Claude Code les regarde et itère sur les scripts jusqu'à ce que l'échelle (ver 0,4 m, amas de 10 ≈ tique, de 200 ≈ un peu moins qu'un soldat) et la lisibilité soient bonnes.

## 14. Configuration BepInEx

Chaque nombre de ce document est une clé du fichier `BepInEx/config/dev.pointvirgule.mistworms.cfg`, sauf les constantes nommées documentées (voir la fin de cette section). Toutes les clés de jeu sont synchronisées depuis le serveur et verrouillées pour les clients (Jötunn `ConfigurationManagerAttributes { IsAdminOnly = true }`) ; seules les sections `Performance` (rendu, sons) et `Dev` restent locales. Chaque clé porte une description en anglais et une plage (`AcceptableValueRange`) ; une valeur hors plage est ramenée dans la plage avec un avertissement.

| Section | Clé | Défaut | Plage / type | Référence |
| --- | --- | --- | --- | --- |
| General | `Enabled` | true | bool | désactive tout spawn ; les entités existantes restent inertes |
| Worm | `Health` | 10 | 1–200 | §3.1 |
| Worm | `MoveSpeed` / `FleeSpeedMultiplier` / `TurnSpeed` | 2.4 / 1.35 / 360 | 0.5–8 / 1–3 / 90–720 | §3.1 |
| Worm | `FleeDuration` | 4 | 0.5–30 s | §3.3 |
| Worm | `DurabilityPerBite` / `BiteInterval` | 1.0 / 0.5 | 0.1–20 / 0.1–5 s | §6.1 |
| Worm | `HealthDamagePerBite` / `CreatureDamagePerBite` / `StructureDamagePerBite` | 1.0 / 1.0 / 0.5 | 0–50 | §3.1 |
| Worm | `CobraDurability` / `CobraCreatureDamage` / `CobraRange` / `CobraWindup` / `CobraCooldown` | 2 / 2 / 1.2 / 0.6 / 1.5 | — | §3.1 |
| Worm | `ViewRange` / `ViewAngle` / `HearRange` / `DormantViewRange` / `DormantHearRange` | 20 / 360 / 15 / 4 / 6 | m, ° | §3.1 |
| Resist | `Fire` / `Pierce` / `Blunt` / `Slash` / `Frost` / `Lightning` / `Poison` / `Spirit` / `Chop` / `Pickaxe` | VeryWeak / Resistant / Normal / Weak / Normal / Normal / Immune / Immune / Ignore / Ignore | enum `HitData.DamageModifier` | §3.2 |
| Mass | `FormThreshold` / `FormRadius` / `GatherRadius` | 3 / 2.5 / 20 | 2–50 / 1–10 / 5–60 | §4.1 |
| Mass | `MaxCount` / `DissolveThreshold` | 200 / 5 | 10–1000 / 1–50 | §4.1, §4.4 |
| Mass | `BaseRadius` / `RadiusExponent` / `MaxColliderRadius` | 0.83 / 0.36 / 0.6 | 0.1–2 / 0.2–1 / 0.2–2 | §4.5 |
| Mass | `MoveSpeed` / `TargetRange` / `EngulfRange` / `DetachDistance` / `DetachDelay` | 2.4 / 20 / 0.6 / 1.5 / 0.75 | — | §4.6, §4.9 |
| Mass | `KnockbackMinFactor` | 0.1 | 0–1 | §4.8 |
| Mass | `StreamHeightFactor` / `StreamLengthPerWorm` / `StreamMaxLength` / `RegroupRadius` | 0.4 / 0.08 / 6 / 30 | 0.1–1 / 0.01–0.5 m / 1–20 m / 5–100 m | §4.7, §4.4 |
| Engulf | `SlowFactor` / `WisplightLast` / `IncludeUnequippedInventory` / `ItemsPerTenWorms` | 0.5 / true / true / 1 | 0.1–1 / bool / bool / 1–10 | §6.2, §6.3 |
| Engulf | `BlockReducesBites` / `ArmorReducesBites` / `ScaleWithWorldModifiers` / `ShowDurabilityText` | true / false / true / true | bool | §6.4, §9.2 |
| Engulf | `WisplightVirtualDurability` | 100 | 1–10000 | §6.3 |
| Engulf | `WisplightPrefab` | `Demister` **\[À VÉRIFIER\]** | string | §6.3 |
| Targets | `AttackPlayers` / `AttackTamed` / `AttackCreatures` / `AttackStructures` | true ×4 | bool | §5.3 |
| Targets | `PlayerWeight` / `TamedWeight` / `CreatureWeight` / `StructureWeight` | 0.6 / 0.8 / 1.0 / 1.5 | 0.1–5 | §5.3 |
| Targets | `ExcludedPrefabs` | `SW_DivineWhale` | liste de prefabs séparés par des virgules | §5.3, §10.4 |
| Targets | `WakeOnlyByPlayers` / `ActiveRadius` / `CalmDelay` | true / 45 / 60 | bool / 10–200 m / 5–600 s | §5.1, §5.2 |
| Fire | `AfraidOfFire` / `FearRadius` / `FearDuration` | true / 4.0 / 3.0 | bool / 0–15 / 0–30 | §9.1 |
| Drops | `EquipmentDropRatio` / `IncludeUpgradeMaterials` | 1.0 / true | 0–1 / bool | §7.2 |
| Drops | `BaitChance` / `EntrailsChance` / `BloodbagChance` | 0.08 / 0.08 / 0.04 | 0–1 | §7.5 |
| Drops | `BaitWeights` | généré au premier lancement | `prefab:poids,…` | §7.5 |
| Drops | `MaxItemsPerDropEvent` | 60 | 1–500 | §7.3 |
| Nest | `Health` / `InitialWorms` / `FreeWormsPerRoom` / `SpawnInterval` / `MaxAlivePerNest` / `ReleaseOnDestroy` | 150 / 8 / 3 / 20 / 50 / 5 | — | §8.1 |
| Nest | `WakeRadius` / `MinRoomArea` / `MaxNestsPerMine` | 10 / 25 / 24 | m / m² / 1–64 | §8.1, §8.3 |
| Colony | `MaxAlivePerMine` / `AlertRadius` | 400 / 25 | 10–2000 / 5–100 | §8.2, §5.1 |
| Network | `SyncRate` / `SnapshotInterval` | 5 / 5 | 1–20 Hz / 1–60 s | §11.3 |
| Performance (local) | `RenderDistance` / `MaxVisibleSurfaceWorms` / `MaxSoundSources` / `ShadowDistance` | 60 / 40 / 4 / 25 | — | §12 |
| Performance (local) | `SimRateNear` / `SimRateFar` / `FlipbookBlend` / `ShaderCandidates` | 20 / 5 / false / `Custom/Creature,Custom/Piece,Standard,Legacy Shaders/Diffuse` | — | §11.1, §12.2 |
| Dev (Debug, local) | `BridgePort` / `AutoLoadWorld` / `AutoLoadCharacter` / `WorldSeed` / `AutoDevCommands` | 27781 / `MWTest` / `MWTester` / `MistWorms` / true | — | §15 |

Règles d'implémentation : une classe `MWConfig` statique expose chaque clé en `ConfigEntry<T>` typé, lue à chaud (pas de cache) sauf pour les capacités des tableaux (`Colony.MaxAlivePerMine`, lues au chargement de la colonie) ; un changement de config synchronisé en cours de partie est appliqué au tick suivant. Le fichier généré au premier lancement est commité dans `thunderstore/` comme référence (`default.cfg`).

Cadences de simulation (`Performance.SimRateNear` / `SimRateFar`) : bien que locales, ce sont celles du **propriétaire** de la colonie qui s'appliquent (§11.1).

Constantes nommées (documentées dans la spec, pas des clés de config) : poids et rayons du comportement de groupe des vers libres (§3.3, §11.1) ; paramètres de coulée autres que ceux du tableau (§4.7) ; paramètres d'ondulation, de variation par instance et de transitions visuelles (§12.1, §12.4, §13.1) ; zone de coup de l'amas (§4.5).

## 15. Harnais de dev et auto-tests in-game

Claude Code doit pouvoir, seul et sans écran, lancer Valheim moddé, entrer dans une mine, créer des vers, mesurer, prendre des captures, exécuter des scénarios à assertions et lire les résultats. Tout passe par un pont HTTP local embarqué dans la build Debug du mod et un outil en ligne de commande `tools/vh.py`.

### 15.1 Mise en place (`tools/setup-game.ps1`, `tools/setup-server.ps1`)

1. Localiser Valheim via le registre Steam et `libraryfolders.vdf` ; `robocopy` vers `C:\ValheimDev\` (une fois, puis `/MIR` pour suivre les mises à jour Steam).
2. Télécharger BepInExPack\_Valheim et Jötunn depuis Thunderstore, les extraire dans `C:\ValheimDev\` ; vérifier `BepInEx/LogOutput.log` au premier lancement (`Chainloader startup complete`).
3. SteamCMD : `+login anonymous +app_update 896660 validate` dans `C:\ValheimDevServer\` + BepInEx + Jötunn + le mod ; script `tools/run-server.ps1` (monde `MWServer`, mot de passe, port 2456, `-crossplay` off).
4. Copier `assembly_valheim.dll` & co dans `reference/game-dlls/` et décompiler avec `ilspycmd` dans `reference/decompiled/` (phase 0).

### 15.2 QuickStart (`Dev/QuickStart.cs`, `#if MW_DEV`)

- Arguments de ligne de commande lus par le mod : `-mwdev autoload=<monde>/<perso> bridge=<port> seed=<seed>` ; `tools/run-game.ps1` lance `C:\ValheimDev\valheim.exe -console -window-mode windowed -screen-width 1280 -screen-height 720 -mwdev autoload=MWTest/MWTester bridge=27781` (Steam doit tourner).
- Au `FejdStartup`, le mod crée s'ils manquent le profil `MWTester` (`PlayerProfile`) et le monde `MWTest` (`World` avec le seed `Dev.WorldSeed`), puis démarre une partie solo locale en reproduisant le chemin vanilla du bouton « Démarrer » (pas de serveur, pas de mot de passe) **\[À VÉRIFIER : enchaînement exact `FejdStartup` → `Game` en 1.0 ; s'inspirer des mods open-source de démarrage rapide\]**. Alternative `-mwdev join=127.0.0.1:2456` pour rejoindre le serveur local.
- `Dev.AutoDevCommands = true` active `devcommands` (`Terminal.m_cheat`), `debugmode` et `fly` à la demande ; le mode dieu est piloté par les scénarios.

### 15.3 DevBridge (`Dev/DevBridge.cs`) — HTTP sur `127.0.0.1:27781`, JSON

| Méthode / route | Rôle |
| --- | --- |
| `GET /status` | scène, monde, position du joueur, propriétaire ?, nombre de colonies, FPS, temps de jeu |
| `POST /console` `{cmd}` | exécute une commande console vanilla ou `mw_*` ; renvoie les lignes de sortie |
| `POST /tp` `{x,y,z}` et `POST /tp-mine` | téléporte ; `/tp-mine` trouve l'entrée de mine infestée la plus proche (`ZoneSystem.FindClosestLocation` sur les emplacements du §8.3), s'y téléporte, attend le chargement de la zone, utilise l'entrée (téléporteur vanilla) et attend que la colonie existe ; délai max 90 s |
| `POST /spawn` `{n, radius, state, nestIndex}` / `POST /mass` `{n}` / `POST /nest` | crée des vers virtuels, un amas, un nid devant le joueur (Debug uniquement, ignore les plafonds si `force:true`) |
| `POST /player` `{god, ghost, hp, stamina, inventory:[{prefab, quality, durability}], equip:[…], clear:true}` | prépare l'état du joueur d'un scénario |
| `POST /hit` `{targetZdoid, damage:{blunt,…}, point}` | injecte une `HitData` vanilla sur une entité (reproduit un coup sans dépendre des animations) |
| `POST /attack` `{weapon}` | équipe l'arme et déclenche une vraie attaque de mêlée vanilla vers l'avant (`Humanoid.StartAttack`) |
| `GET /state` | JSON complet : par colonie (vers par état, nids, groupes), par amas (n, hp, partial, pool, cible, état), joueur (hp, effets, durabilité de chaque objet), matrice `IsEnemy` à la demande |
| `GET /perf` | moyennes et max sur les 5 dernières secondes : ms de simulation, ms de rendu, appels de rendu, octets réseau/s, allocations GC |
| `POST /camera` \`{mode: free | follow, pos, lookAt}`+`GET /screenshot\` |
| `POST /scenario` `{name, params}` → `GET /scenario/{id}` | lance un scénario (coroutine) ; résultat : `{status, assertions:[{name, ok, expected, actual}], shots:[…], log:[…]}` |
| `POST /time` `{scale}` / `POST /wait` `{seconds}` | accélération (`Time.timeScale` ≤ 4) et attente |
| `POST /quit` | quitte proprement |

Tout est exécuté sur le thread principal via une file ; les requêtes sont séquentielles ; le pont n'écoute jamais ailleurs que sur l'interface locale et n'existe pas en Release.

### 15.4 Outil en ligne de commande (`tools/vh.py`)

`vh launch | wait-ready | status | cmd "…" | tp-mine | spawn N | mass N | state | perf | shot [nom] | scenario NOM | scenario-all | log-tail | server start|stop | quit`. Chaque commande imprime du JSON ; `scenario-all` écrit `docs/test-report.md` (tableau scénario → OK/KO, chiffres de perf, liens vers les captures) et retourne un code de sortie non nul si un scénario échoue.

### 15.5 Scénarios (`Dev/Scenarios/*.cs`, chacun une classe `IDevScenario` avec `IEnumerator Run(ScenarioContext)`)

| Id | Scénario | Assertions principales |
| --- | --- | --- |
| S00 | S00_Smoke : démarrage, pont de dev, scénario trivial (outillage de la phase 1) | plugin chargé (GUID, version), config liée au plugin (`Worm.Health`), jeton `$mw_mass` traduit, joueur local présent en jeu |
| S01 | Ver seul, une épée en bronze | mort en 1 coup ; aucun drop de pool ; drop natif selon les probabilités sur 200 répétitions (± 3 pts) |
| S02 | 40 vers spawnés, joueur visible | un seul amas de 40 en ≤ 10 s ; `hp = 40 × Worm.Health` ; nom « Amas grouillant (40) » |
| S03 | Coup injecté de 55 contondant sur un amas de 40 | n = 35, partial = 5 ; 22 perçant → n = 34 (11 effectifs, partial 6) ; 30 feu → ×2 = 60 → n = 28, partial 6 |
| S02b | 240 vers spawnés, joueur visible (variante de S02) | fusion jusqu'à 200, jamais au-delà : un amas de 200 et un de 40 |
| S03b | Parade sans stagger (variante de S03, phase 5 : elle demande le blocage des morsures) | parade parfaite : tick de morsure annulé, suivant retardé de 0,5 s, aucun stagger |
| S04 | Engloutissement, set rembourré complet q1 + Wisplight + 10 objets divers, mode dieu off | effet Infesté actif ; PV inchangés tant qu'un éligible subsiste ; perte de durabilité totale ≈ 80/s ± 25 % ; Wisplight intact tant qu'il reste autre chose ; puis Wisplight ; puis PV baissent |
| S05 | Pool : inventaire = cuirasse rembourrée seule ; stop à 50 % puis amas tué par coups injectés | fer droppé = floor(10 × d / 1000) et fil de lin = floor(20 × d / 1000), où d = durabilité réellement perdue par la cuirasse (lue avant les coups) ; même résultat que l'amas meure d'un seul coup ou ver par ver ; aucun reste positif après le dernier ver |
| S06 | Torche en main | aucun engloutissement pendant 10 s, amas à ≥ 4 m ; épée équipée → engloutissement en ≤ 5 s |
| S07 | Amas de 8 ; 4 tués | amas dissous, 4 vers libres en fuite ; regroupement en ≤ 15 s si 3+ proches |
| S08 | Coulée : amas de 200 dans un couloir de mine de 2 m | progression ≥ 10 m en ≤ 20 s ; mode Stream observé ; proxies frappables pendant la coulée |
| S09 | Nid, `SpawnInterval` forcé à 2 s | +3 vers en 6 s ; arrêt à `MaxAlivePerNest` ; aucune ponte pendant 3 s après un coup |
| S10 | 450 vers demandés | vivants ≤ 400 (sauf `force`) |
| S11 | Perf : 400 vers actifs + 2 amas de 200, 30 s, caméra sur la mêlée | sim moyenne ≤ 1,0 ms, max ≤ 3 ms ; rendu ≤ 0,5 ms ; appels ≤ 24 + 6 ; GC ≤ 0,5 Mo sur 30 s ; réseau ≤ 6 Ko/s (en multijoueur S17) |
| S12 | Fouineur spawné près d'un amas actif | le fouineur prend des dégâts et riposte ; à sa mort, aucun item au sol, pool augmenté de sa liste de drop |
| S13 | Porte en bois construite près d'un amas | détruite ; ressources dans le pool, rien au sol |
| S14 | Dormance (`CalmDelay` forcé à 5 s), joueur à 60 m | amas rentrés et dissous en dormants, n et pool conservés ; retour du joueur → réveil, même n |
| S15 | Sauvegarde / rechargement du monde | mêmes compteurs, pools, nids, timers (± 1 ver) |
| S16 | Ensemencement d'une vraie mine (`/tp-mine`) | ≥ 1 nid par salle éligible, ≤ 24 nids, vers isolés présents, aucun objet hors de la boîte du donjon, mine vanilla intacte ; mine déjà visitée avant le mod → ensemencée au premier chargement, une seule fois |
| S17 | Multijoueur local : serveur dédié + 1 client (2 si une seconde instance peut démarrer) | config synchronisée, client sans mod refusé, amas visible et frappable depuis un non-propriétaire, bande passante mesurée |
| S18 | S18_GroupCoherence : a) 30 vers chassent un joueur immobile en terrain ouvert ; b) 20 vers en mine, spawnés en passes autour du joueur (comme P3_Wander) avec ligne de vue (phase 4A) | a) en régime établi, ≥ 95 % des paires de voisins ont leurs milieux de corps à plus de 0,2 m, et les chasseurs arrivés couvrent ≥ 240° autour de la cible (secteurs de 30° occupés) ; b) aucun ver sans progrès (moins de 0,5 m) pendant plus de 6 s alors que son but est atteignable (compatible avec le chemin demandé à 3 s, §11.1 ; un ver à moins de 2,5 m du joueur ou en Striking n'est pas compté comme bloqué), et aucune violation mur/sol (mêmes contrôles que P3_Wander). Le couloir de 2,5 m n'est pas une précondition : la largeur du passage le plus étroit et le taux de chemins de chasse sont informatifs |
| V01 | Visuel : le ver de près, à l'échelle du joueur (refait en phase 4A : look « pâle de caverne », crawl et idle) | captures relues par Claude Code : rien d'invisible ou de noir, échelle 0,4 m, ombres présentes |
| V02–V06 | Visuels : V02 amas de 10, V03 amas de 200, V04 amas de 50 (comme le code), V05 engloutissement, V06 nid, à côté du joueur ; V02 à V04 refaits en phase 4B avec un vrai `MW_WritheMass` | captures relues par Claude Code : rien d'invisible ou de noir, échelles conformes à §4.5, ombres présentes ; le repère « 20 ≈ couvain » se vérifie en V02/V04 par la table, sans capture dédiée |
| V07 | V07_WormLight : ver de près sous une torche, puis sous une lampe dvergr ; captures à 0,5 m et 2 m, et séquence de 4 captures de reptation (phase 4A) | reflets nets, ondulation en S lisible |
| V08 | V08_BloodHit : coup sur un ver, capture au moment du jet, puis à la mort (phase 4A) | jet vert, petit (≈ 0,3 m) |
| V09 | V09_MassTransitions : captures de la formation (3 vers puis dôme), d'une absorption, d'une fusion, d'une dissolution et d'une mort de ver de surface (phase 4B) | transitions lisibles, jamais de saut de rayon |

Scénarios propres à la phase 3, hors numérotation S/V : `P3_Wander` (100 vers errent en mine réelle sans traverser sol ni murs) et `P3_Cobra` (coup de cobra et règle d'équipement) ; ils sont rejoués à chaque phase.

Dans les scénarios de combat de groupe (S18), le joueur est épinglé toutes les 0,25 s (le recul des morsures est voulu) et les créatures vanilla à moins de 40 m sont retirées avant la partie b). La frame de la première frappe de la session (premier `Damage()` vanilla et JIT, de 1,5 à 5,4 ms) est exclue du max de simulation : ce n'est pas un coût récurrent.

Chaque scénario remet l'état à zéro (`mw_kill`, inventaire vidé, mode dieu) avant et après ; un scénario qui dépasse 120 s échoue ; une exception du mod dans le log pendant un scénario le fait échouer.

### 15.6 Tests hors jeu (`src/MistWorms.Tests`, xunit, `dotnet test`)

Parseur glTF (sur les `.glb` du dépôt), maths du pool (§7.3, cas des 40 vers et de la fraction résiduelle), formule du rayon, sélection des pièces à mordre (distribution uniforme, Wisplight en dernier, report d'excédent), encodage/décodage des instantanés réseau, génération des poids d'appâts. Ces tests tournent sans le jeu et sont la première chose exécutée par `tools/build.ps1`.

### 15.7 Boucle de travail attendue de Claude Code

`build.ps1 -Debug` → `vh launch` → `vh wait-ready` → `vh tp-mine` (ou spawn en plaine de test) → scénario(s) → captures → lecture du log BepInEx (`BepInEx/LogOutput.log`, préfixe `[MistWorms]`) → correction → `vh quit`. Tout échec de scénario est corrigé avant de passer à la phase suivante (§16) ; le rapport final `docs/test-report.md` fait partie de la livraison.

## 16. Plan de réalisation

La livraison est un seul bloc, mais le travail suit dix phases (0 à 9) dans cet ordre, chacune fermée par ses scénarios verts avant d'ouvrir la suivante : c'est ce qui permet à Claude Code de tester au fur et à mesure et d'isoler les régressions. Chaque phase se termine par un commit dont le message cite la phase.

1. **Phase 0 — Reconnaissance et outillage** (rien de visible en jeu). Installer les outils (§1.3), créer la copie de jeu et le serveur local (§15.1), décompiler le jeu, épingler les versions (`docs/versions.md`). Écrire un plugin « dump » temporaire qui exporte dans `docs/vanilla-reference.md` : stats des créatures de référence (`Tick`, `Seeker`, `SeekerBrood`, `SeekerBrute` : PV, dégâts, vitesses, portées de perception, dimensions de collider), noms des prefabs de rampants, des destructibles candidats, des `sfx_*` / `vfx_*`, du Wisplight, des appâts et leurs recettes, des entrailles et sac de sang, de l'énumération `Room.Theme`, des emplacements de mines, des shaders (avec support de l'instancing testé), de la couche des attaques, des durabilités et recettes des armures. Trancher tous les **\[À VÉRIFIER\]** et consigner les décisions dans `docs/decisions.md`. Fini quand : le dump est complet et la copie de jeu moddée démarre avec Jötunn sans erreur.
2. **Phase 1 — Squelette du mod et harnais**. Plugin, config (§14), localisation, QuickStart, DevBridge, `vh.py`, tests xunit vides, `build.ps1`. Fini quand : `vh launch` arrive en jeu dans `MWTest` sans clic, `/status` répond, `vh scenario` exécute un scénario vide, `dotnet test` passe.
3. **Phase 2 — Assets**. Scripts Blender, loader glTF, `AssetCatalog`, rendu instancié d'un ver et d'un dôme statiques devant le joueur, choix du shader. Fini quand : V01–V03 (ver, amas 10 et 200 immobiles) sont validés visuellement et le chargement prend < 300 ms.
4. **Phase 3 — Vers virtuels**. Colonie, SoA, états Idle/Hunting/Striking/Fleeing, proxies et routage des coups, coup de cobra avec règle d'équipement, drops natifs, mort. Fini quand : S01 passe, 100 vers errent sans tomber dans le sol ni traverser les murs (vérifié en mine réelle).
5. **Phase 4 — Finition du ver, puis amas**, en deux temps.
    - **Phase 4A — Finition du ver**. Asset « pâle de caverne » (§13.4), crawl 16 images et idle 8 images (§12.1), variation par instance (échelle, roulis), sang vert (§12.4), sons du ver seul (§12.3 : idle/reptation 4 sources max à < 12 m, cobra, coup reçu, mort), comportement de groupe des vers libres (§3.3, §11.1, sans formation d'amas), remesure du chargement des assets (< 300 ms, §13.3). Fini quand : V01 (refait), V07 et V08 sont validés visuellement ; S18 passe ; S00, S01, P3_Cobra et P3_Wander (mine) passent toujours.
    - **Phase 4B — Amas**. Prefab créature (avec `Humanoid`), `WritheMassAI` (états Idle, Hunt, Dormant en stub, §4.10), formation, absorption, fusion, PV et pertes, dissolution (après une perte seulement), nom au survol, recul, pas de stagger, zone de coup (sphères `hitbox`, §4.5), coulée, rendu du dôme et des vers de surface, transitions visibles (§12.4), échelle du dôme à 0,6 quand la caméra est dedans (§6.6), sons de l'amas ; code dans `src/MistWorms/Mass/`. Fini quand : S02, S03, S07 et S08 passent ; V02 à V04 sont refaits avec un vrai `MW_WritheMass` en jeu ; V09 est validé visuellement.
6. **Phase 5 — Engloutissement et équipement**. Effet Infesté, tick agrégé, patches `ApplyArmor` / `ApplyDamage`, sélection des pièces, Wisplight, phase PV, blocage, texte flottant, peur du feu (durée de fuite de l'amas = `Fire.FearDuration`), état Engulf de l'amas (§4.10), règle de parade du mod (§4.8, §6.4). Fini quand : S04, S06 et S03b passent.
7. **Phase 6 — Pool et drops**. Pool, `RPC_Consumed`, parts entières par reste fractionnaire `carry` (§7.3), reliquat, capture des drops de créatures (patch `CharacterDrop.GenerateDropList`) et de structures, ciblage « tout », factions. Fini quand : S05, S12, S13 passent et la matrice `IsEnemy` est conforme.
8. **Phase 7 — Nids, spawn, dormance, persistance**. Nid, ponte, plafonds, ensemencement des mines, alerte et retour au calme, instantanés, sauvegarde. Fini quand : S09, S10, S14, S15, S16 passent.
9. **Phase 8 — Performance et multijoueur**. Profilage, cadences LOD, deltas réseau, dead reckoning, serveur local, tests non-propriétaire, effets de coup et de mort des vers chez les pairs distants (§12.4). Fini quand : S11 et S17 passent avec les budgets de §11.
10. **Phase 9 — Packaging et livraison**. Build Release sans `Dev/`, manifest, README, CHANGELOG, icône, zip Thunderstore, guide serveur, `docs/test-report.md` final, revue du code (patches protégés, aucune allocation dans les boucles chaudes, aucun `Debug.Log` oublié). Fini quand : §19 est entièrement coché.

Règles de conduite pendant les phases : jamais plus d'un **\[À VÉRIFIER\]** ouvert dans une phase en cours ; toute déviation de la spec (valeur, règle, nom) est notée dans `docs/decisions.md` avec sa raison ; un scénario rouge n'est jamais contourné en changeant ses assertions sans l'inscrire dans ce fichier ; les scénarios des phases précédentes sont rejoués (`vh scenario-all`) à la fin de chaque phase.

## 17. Packaging Thunderstore et installation

Le mod est publié sur Thunderstore sous le nom `MistWorms`, s'installe en un clic côté joueurs via r2modman ou Thunderstore Mod Manager, et se dépose sur le serveur HG Hosting dans `BepInEx/plugins/`.

### 17.1 Package (`tools/package.ps1` → `MistWorms-<version>.zip`)

| Fichier | Contenu |
| --- | --- |
| `manifest.json` | `name: "MistWorms"`, `version_number` (SemVer, `1.0.0` à la première publication), `website_url` (dépôt GitHub), `description` (≤ 250 caractères, en anglais), `dependencies: ["denikson-BepInExPack_Valheim-<ver>", "ValheimModding-Jotunn-<ver>"]` aux versions épinglées en phase 0 |
| `README.md` | en anglais avec une section française : ce que fait le mod (résumé §0 en 10 lignes), contre-mesures (feu, zone), configuration (tableau §14 abrégé), installation serveur/client, compatibilité (Valheim 1.0.x, aucun autre mod testé), crédits (assets originaux générés avec Blender ; sons du jeu), licence (MIT pour le code, assets CC BY 4.0) |
| `CHANGELOG.md` | une entrée par version |
| `icon.png` | 256×256 rendu par `render_icon.py` |
| `plugins/MistWorms/MistWorms.dll` + `plugins/MistWorms/assets/**` + `plugins/MistWorms/Localization/**` | la build Release et ses assets ; le dossier est préservé tel quel par les gestionnaires de mods |

Publication : via le site Thunderstore (compte de l'utilisateur, à lui de cliquer) ou `tcli publish` avec son jeton ; Claude Code prépare le zip et valide le manifest avec l'outil en ligne de Thunderstore. Versionnage : patch pour un correctif, mineur pour un changement de règle ou de config, majeur pour un changement de format `mw_snap` ou de dépendance majeure.

### 17.2 Serveur dédié (HG Hosting)

1. Sauvegarder le monde (téléchargement du dossier `worlds_local` ou sauvegarde du panel).
2. Installer BepInExPack\_Valheim : option du panel de l'hébergeur si elle existe, sinon dépôt du contenu du zip à la racine du serveur par SFTP ; sur Linux le démarrage doit passer par `start_server_bepinex.sh` (ou la ligne de commande équivalente exportant `DOORSTOP_*` / `LD_PRELOAD`), à adapter au panel. **À confirmer avec le support HG Hosting** : OS, prise en charge de BepInEx, script de démarrage modifiable.
3. Déposer `BepInEx/plugins/Jotunn/` et `BepInEx/plugins/MistWorms/` ; démarrer ; vérifier dans `BepInEx/LogOutput.log` : `Loading [Jotunn …]`, `Loading [MistWorms …]`, aucune erreur.
4. Ajuster `BepInEx/config/dev.pointvirgule.mistworms.cfg` sur le serveur : ce sont ces valeurs qui s'imposent à tous.
5. Un client sans le mod (ou avec une version mineure différente) est refusé à la connexion avec un message Jötunn explicite.

### 17.3 Joueurs

- r2modman ou Thunderstore Mod Manager → profil Valheim → installer `MistWorms` (BepInExPack et Jötunn viennent automatiquement) → lancer le jeu depuis le gestionnaire. L'utilisateur partage le **code de profil** exporté du gestionnaire pour que tout le monde ait exactement les mêmes versions.
- Le README donne aussi la méthode manuelle (zip BepInEx dans le dossier du jeu, puis les deux dossiers dans `BepInEx/plugins/`).
- Mise à jour : le serveur d'abord, puis les clients ; une mise à jour mineure du mod impose la mise à jour de tous.

## 18. Risques et solutions de repli

Les trois risques qui pourraient bloquer sont le shader (instancing), le démarrage automatique du jeu et le réseau à 400 vers ; chacun a un repli déjà décidé, pour que Claude Code n'ait jamais à s'arrêter pour demander.

| Risque | Probabilité | Repli décidé |
| --- | --- | --- |
| Aucun shader vanilla n'accepte l'instancing | moyenne | Fusion CPU des instances par groupe (§12.2) ; si le budget rendu dépasse 0,5 ms, réduire `MaxVisibleSurfaceWorms` à 20 et le flipbook à 8 images ; en dernier recours, `Graphics.DrawMesh` par ver (≤ 400 appels) |
| Pas de shader vanilla transparent utilisable pour le dôme | moyenne | Réduire l'échelle du dôme à 0,6 quand la caméra est à l'intérieur ; pas de dither |
| QuickStart impossible à reproduire en 1.0 (`FejdStartup` changé) | faible | Rejoindre le serveur dédié local par `-mwdev join=` (le chemin de connexion est plus stable) ; les scénarios tournent alors en multijoueur 1 client |
| Une seconde instance de Valheim refuse de démarrer sur le même compte Steam | élevée | S17 se limite à serveur + 1 client ; les tests à 2+ joueurs sont faits par l'utilisateur et ses amis avec une check-list fournie dans `docs/test-report.md` |
| `HitData.m_attacker` sans `Character` (coup de cobra émis par la colonie) casse une attente vanilla | moyenne | Donner à la colonie un `Character` fantôme désactivé (pas d'IA, pas de collider, invisible) qui sert d'attaquant nominal ; nom localisé « Ver des brumes » pour les messages de mort |
| Un trigger sur la couche `character` n'est pas touché par certaines attaques (projectiles, `Aoe`) | moyenne | Collider non-trigger sur une couche dédiée aux hitbox **\[décidé en phase 0 par test\]** + `Physics.IgnoreCollision` systématique avec les joueurs |
| Réseau > 6 Ko/s avec 400 vers actifs | moyenne | Passer `SyncRate` à 3 Hz pour les vers à plus de 30 m d'un joueur, quantifier la position sur 1 octet par axe relatif au dernier envoi, ne jamais envoyer les vers hors de 120 m d'un pair |
| Pathfinding vanilla trop lent ou absent pour les vers libres | faible | Les vers libres n'en ont pas besoin dans 95 % des cas (salles ouvertes) ; un ver bloqué 10 s est téléporté de 1 m vers l'amas le plus proche, invisible sous la pénombre des mines |
| Les fouineurs attaquent les nids ou les vers dormants | faible | Nid sur une couche ignorée par `BaseAI.FindClosestStaticPriority` ; les dormants n'ont pas de `Character` donc n'existent pas pour l'IA |
| Hook de génération des donjons absent ou tardif | faible | Ensemencer au premier `Update` de la colonie créée par un postfix sur `Location`/`DungeonGenerator.Awake`, avec le même drapeau `mw_seeded` |
| Le budget de simulation dépasse 1 ms | faible | Cadence des vers « loin » à 2 Hz, perception à 1 Hz, découpage en 4 tranches ; mesure par `PerfProbe` avant toute optimisation |
| Mise à jour de Valheim qui casse un patch | certaine à terme | Patches protégés par `try/catch`, chacun vérifié au démarrage par un test d'applicabilité qui loggue un avertissement clair ; versions épinglées ; le harnais rejoue tout en 10 minutes |

Un repli activé est consigné dans `docs/decisions.md` et, s'il change une règle visible, dans le README.

## 19. Critères d'acceptation

Le mod est livrable quand chaque case est cochée avec, pour les règles de jeu, le scénario correspondant vert dans `docs/test-report.md`.

- [ ] Le jeu moddé (copie de dev) démarre avec BepInEx + Jötunn + MistWorms sans erreur ni avertissement dans `LogOutput.log`
- [ ] `vh launch` arrive en jeu sans intervention ; `vh scenario-all` termine en vert (S01–S18, S02b, S03b, V01–V09)
- [ ] Un ver seul meurt d'un coup d'arme en bronze ; son coup de cobra ne touche que l'équipement (S01, S04)
- [ ] Le ver est « pâle de caverne », luisant sous les torches, ondule en S (serpentine + péristaltique) en marche et au repos, et jette un petit sang vert à chaque coup et à sa mort (V01, V07, V08)
- [ ] Des vers libres en groupe ne se chevauchent pas, avancent en file dans les couloirs et encerclent leur cible (S18)
- [ ] 3 vers proches forment un amas ; 40 vers forment un amas de 40 dont les PV et le nom reflètent le compte (S02)
- [ ] Un coup de D dégâts tue floor(D / PV d'un ver) vers, résistances comprises ; le feu compte double, le perçant moitié (S03)
- [ ] Sous 5 vers, après une perte, l'amas se dissout en fuyards qui se regroupent (S07) ; deux amas fusionnent jusqu'à 200 et jamais au-delà (S02b)
- [ ] Taille visuelle : 10 ≈ tique, 20 ≈ couvain, 200 un peu moins qu'un soldat (V02–V04, le repère « 20 ≈ couvain » étant vérifié par la table) ; un amas de 200 traverse un couloir de 2 m en coulée (S08)
- [ ] Aucun stagger ; recul décroissant ; parade sans stagger, qui annule le tick de morsure (S03b)
- [ ] Les transitions de l'amas (formation, absorption, fusion, dissolution, mort d'un ver de surface) sont visibles et sans saut de rayon (V09)
- [ ] Engloutissement : Infesté, vitesse ÷ 2, attaques et roulade possibles, roulade sans libération, fuite par sprint, feu ou dissolution (S04, S06)
- [ ] Morsures : 1 point de durabilité par ver toutes les 0,5 s ; tout l'inventaire au hasard ; Wisplight en dernier ; PV seulement après ; blocage réduit, armure non (S04)
- [ ] Pool : parts entières par ver tué, reliquat au dernier, arrondi vers le bas, recette de base + améliorations, rien pour les objets sans recette (S05)
- [ ] Tout est cible une fois éveillé (joueurs, apprivoisés, créatures, dvergar, structures) ; rien ne s'éveille sans joueur ; ce qui est dévoré va dans le pool (S12, S13, S14)
- [ ] Drops natifs : appâts pondérés par rareté, entrailles, sac de sang ; aucun trophée ; aucun asset nouveau pour les items (S01)
- [ ] Un nid par salle éligible + vers isolés ; ponte toutes les 20 s ; plafonds 50 / 200 / 400 ; mine propre une fois les nids détruits ; jamais hors mine (S09, S10, S16)
- [ ] Dormance et réveil conservent compteurs et pools ; sauvegarde/rechargement idem (S14, S15)
- [ ] Performance : 400 vers actifs + 2 amas de 200 ≤ 1,0 ms de simulation, ≤ 0,5 ms de rendu, ≤ 30 appels, GC stable, ≤ 6 Ko/s (S11, S17)
- [ ] Multijoueur : config imposée par le serveur, client sans mod refusé, amas frappable depuis un non-propriétaire (S17)
- [ ] Assets originaux générés par les scripts Blender du dépôt, régénérables depuis zéro ; icône et bannière Thunderstore produites
- [ ] Textes FR et EN complets ; aucun texte en dur
- [ ] Build Release sans pont de dev ni commandes `mw_*` autres que `mw_version` ; zip Thunderstore validé ; README, CHANGELOG, guide serveur, `docs/decisions.md`, `docs/vanilla-reference.md`, `docs/test-report.md` à jour
- [ ] Tous les **\[À VÉRIFIER\]** de ce document ont une réponse consignée

## 20. Annexes

### 20.1 Données vanilla à vérifier en phase 0

Chaque ligne est une hypothèse de ce document ; Claude Code la confirme ou la corrige dans `docs/vanilla-reference.md` et reporte le résultat dans `VanillaNames` et `docs/decisions.md`.

| Hypothèse | Où vérifier | Si faux |
| --- | --- | --- |
| PV de la tique ≈ 100 (d'où 10 PV par ver) — **tranché** : 50 PV en 1.0.20, `Worm.Health` reste 10 par décision | `Tick` : `Character.m_health` | — |
| Perception de la tique : vue ≈ 10–12 m, ouïe ≈ 15 m — **tranché** : tique 30 m / demi-angle 90° ; le ver retient 20 m / 360° par décision | `Tick` : `MonsterAI.m_viewRange`, `m_viewAngle`, `m_hearRange` | — |
| Taille : tique ≈ 0,6–0,7 m, fouineur ≈ 1,5 m, soldat ≈ 3 m — **tranché** : tique ≈ 1,2 × 0,9 m, couvain ≈ 1,8 × 1,0 m, fouineur ≈ 4,9 × 2,5 m, soldat ≈ 4,6 × 2,5 m (emprise × hauteur) ; recalibré en `R_{10}` = 0,83 m, `e` = 0,36 (§4.5) | colliders / `Renderer.bounds` des prefabs | — |
| Rampant de base pour l'amas : `SeekerBrood` | `ZNetScene.m_prefabs` | `Tick`, puis tout prefab avec `MonsterAI` + `Character` + `ZSyncAnimation` sans vol ni nage |
| Wisplight = prefab `Demister` | `ObjectDB.m_items` (token `$item_demister`) | Corriger `Engulf.WisplightPrefab` |
| Entrailles = `Entrails`, sac de sang = `Bloodbag` | `ObjectDB.m_items` | Corriger |
| Appâts = prefabs `FishingBait*` (base, forêt, grotte, marais, brumes, océan, plaines, cendres, nord…) et leurs recettes au chaudron | `ObjectDB.m_items`, `m_recipes` | Adapter l'énumération et le calcul des poids (§7.5) |
| Thème des mines = `Room.Theme.DvergerTown` ; entrées `Mistlands_DvergrTownEntrance1/2` ; citadelle = `DvergerBoss` | `Room.Theme` (enum), `ZoneSystem.m_locations` | Corriger les filtres de §8.3 |
| `DungeonGenerator.Generate` et `.Load` exposent `m_placedRooms` avec leurs boîtes | décompilation | Hook sur `Room` / `Location` à la place |
| Shader `Custom/Creature` existe, propriétés `_MainTex`, `_BumpMap` | matériau de `Tick`, `Shader.Find`, test d'instancing | Liste `Performance.ShaderCandidates` |
| Les attaques (`Attack`, `Aoe`, `Projectile`) touchent un collider trigger de la couche `character` | `Attack.m_hitMask`, `Aoe.m_hitMask`, test en jeu | Collider non-trigger sur la couche des hitbox + `IgnoreCollision` |
| `BaseAI.IsEnemy` : une faction ennemie de toutes (`Boss` ?) | décompilation + matrice imprimée par `/state` | Postfix Harmony |
| `BaseAI` : peur du feu (`m_afraidOfFire`, `AvoidFire`, critère « torche tenue ») | décompilation | Reproduire la logique trouvée |
| `HitData.GetAttacker()` tolère un ZDOID sans `Character` | décompilation | `Character` fantôme sur la colonie (§18) |
| `Character.Damage` → `BlockAttack` → `ApplyArmor` → `ApplyDamage` dans cet ordre en 1.0 (Call to Arms) | décompilation | Déplacer les patches sur les bons points |
| `CharacterDrop.DropItems` statique recevant la liste ; `Piece.DropResources` ; `WearNTear.Destroy` | décompilation | Adapter les captures (§7.4) |
| `Pathfinding.AgentType` le plus petit praticable en mine | essais en jeu | Choisir par test S08 |
| `sfx_` / `vfx_` : idle, attaque, mort de `Tick` et `SeekerBrood` ; destruction du sac d'œufs | `ZNetScene.m_prefabs` filtrés | Choisir les plus proches |
| Version Unity du jeu (choix `RenderMeshInstanced` vs `DrawMeshInstanced`) | `Application.unityVersion` dans le log | Utiliser l'API disponible |
| Chemin de démarrage solo (`FejdStartup` → `Game`) | décompilation, mods de démarrage rapide open-source | `-mwdev join=` (§18) |

### 20.2 Formules récapitulatives

```latex
\begin{aligned}
&\text{PV}_{amas} = n \cdot H_w \qquad kills = \left\lfloor \frac{partial + D}{H_w} \right\rfloor,\ partial \leftarrow (partial + D) \bmod H_w \\
&r(n) = R_{10}\,(n/10)^{e} \qquad knockback = clamp(10/n,\ k_{min},\ 1) \\
&P_{tick} = n \cdot b \cdot f_{block} \qquad k_{items} = clamp(\lceil \iota\, n/10 \rceil,\ 1,\ |eligibles|) \\
&pool[r] \mathrel{+}= \frac{d}{D_{max}(q)}\,(a_r + (q-1)\,u_r)\,\rho \qquad share_r = pool[r] \cdot \frac{k}{n},\ carry_r \mathrel{+}= share_r,\ part_r = \lfloor carry_r \rfloor
\end{aligned}
```

Avec `H_w` = `Worm.Health`, `D` = dégâts effectifs après résistances et blocage, `b` = `Worm.DurabilityPerBite`, `f_block` = facteur de blocage vanilla (1 sans bouclier), `ρ` = `Drops.EquipmentDropRatio`, `k` = vers tués par ce coup, `n` = compte avant le coup, `ι` = `Engulf.ItemsPerTenWorms`, `carry_r` = reste fractionnaire de la ressource `r` (§7.3), diminué de `part_r` après le drop.

### 20.3 Déroulé de référence (pour calibrer et pour tester)

1. Un joueur en cuirasse rembourrée complète (≈ 4 pièces × 1000) + cape + arme + bouclier + Wisplight + divers (≈ 9 000 points de durabilité au total) entre dans une salle : un nid, 8 dormants, 3 isolés.
2. À 10 m du nid, le groupe s'éveille ; en 4 s, 11 vers forment un amas de 11 (r ≈ 0,86 m) et le chargent à 2,4 m/s.
3. Sans torche, engloutissement : 22 points/s, deux pièces mordues par tick ; le joueur perd en moyenne 1 % de durabilité totale toutes les 4 s — il a plusieurs minutes pour réagir, et chaque coup d'arme de zone retire 3 à 6 vers.
4. Avec 40 vers (deux salles alertées) : 80 points/s, quatre pièces par tick, l'équipement complet est dévoré en ≈ 2 minutes, le Wisplight en 2 s de plus, puis 80 PV/s : la mort suit en 2 s.
5. Avec une torche : l'amas recule à 4 m et tourne autour ; sans bouclier le joueur affronte les fouineurs de la salle avec une arme à une main.
6. Deux salles nettoyées sans détruire les nids : 20 minutes plus tard, 60 nouveaux vers dorment autour des nids.

### 20.4 Ce qui est volontairement hors périmètre (v1)

Trophée, item nouveau, compatibilité testée avec d'autres mods, événement de raid, vers hors des mines, étoiles, apprivoisement, bestiaire, effets d'écran, et toute forme d'AssetBundle Unity.
