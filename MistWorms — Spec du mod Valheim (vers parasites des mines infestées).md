# MistWorms — Spec du mod Valheim (vers parasites des mines infestées)

Oct 24, 2026 · @Clément PLANÇON

## 0. Résumé et décisions clés

MistWorms ajoute aux mines infestées des Terres brumeuses un parasite : le **Ver des brumes**, insignifiant seul, redoutable en nombre. Les vers rampent, se rejoignent et forment un **Amas grouillant** qui se déplace comme un seul être, engloutit sa cible et dévore d'abord tout son équipement avant de s'en prendre à ses points de vie. Chaque ver tué rend une part de ce que l'amas a mangé.

Ce document est la spécification complète destinée à Claude Code (Opus) : il doit pouvoir réaliser le mod de bout en bout — code, assets, tests in-game, packaging — sans autre source. Les points marqués **\[À VÉRIFIER\]** sont des hypothèses sur les données internes de Valheim 1.0 à confirmer en phase 0 (§16) avant d'implémenter.

| Sujet | Décision retenue |
| --- | --- |
| Jeu cible | Valheim 1.0, branche publique (patch 1.0.17 du 6 oct. 2026 au moment de la rédaction) ; aucun autre mod sur le serveur |
| Stack | BepInExPack\_Valheim + Jötunn (≥ 2.30) + HarmonyX ; C# ciblant .NET Framework 4.7.2 ; Unity Editor requis seulement pour reconstruire le bundle de shaders (6000.0.75f1, décision 179) |
| Assets | Vrais modèles créés par Claude Code avec Blender en mode headless (scripts bpy versionnés), chargés à l'exécution au format glTF ; shaders et compute shaders de la colonie GPU dans un AssetBundle construit avec Unity 6000.0.75f1 (décision 179) |
| Ver seul | ≈ 10 PV (PV d'une tique ÷ 5, décision 17), une morsure = 1 point de durabilité, un coup d'arme bronze ou mieux le tue |
| Résistances | Feu : très faible ; tranchant : faible ; perçant : résistant ; poison et spirituel : immunisé. Un coup sur l'amas tue plusieurs vers (dégâts ÷ PV d'un ver) : les armes de zone sont la réponse |
| Feu | Les repousse (torche tenue, feu de camp), comme les grisâtres |
| Niveaux | Jamais d'étoiles |
| Amas | Se forme dès 3 vers proches, fusionne jusqu'à 200, se dissout sous 5 après une perte (§4.4) ; rayon ∝ n^0,36 avec R10 = 0,26 m (r(200) = 0,76 m, §4.5) ; fait de vers de 30 cm simulés par le GPU (§12.1) |
| Combat contre l'amas | Pas d'étourdissement ; recul décroissant avec la taille ; parade possible sans stagger |
| Passages étroits | L'amas « coule » : il s'étire visuellement, passe, se reforme (aucune scission d'entité) |
| Engloutissement | Joueur ralenti ×0,5 ; attaque et roulade autorisées ; la roulade ne libère pas ; on s'échappe en courant, avec du feu ou en tuant assez de vers |
| Morsures | 1 morsure par ver toutes les 0,5 s ; l'attaque cobra d'un ver seul suit la même règle (équipement d'abord) |
| Cibles | Tout : joueurs, apprivoisés, créatures, dvergar, structures — mais l'activation ne se fait qu'à la détection d'un joueur |
| Blocage / armure | Un bouclier levé réduit les morsures et perd sa durabilité comme en vanilla ; l'armure ne réduit rien |
| Équipement dévoré | Tout l'inventaire, équipé ou non (seules les pièces équipées si `Engulf.IncludeUnequippedInventory` = false), pièce au hasard, Wisplight en dernier ; exploit « réparation gratuite » assumé |
| Drops | Pool au niveau de l'amas ; recette de base + matériaux d'amélioration selon la qualité ; objets sans recette = rien ; arrondi vers le bas ; drops natifs : appâts de pêche pondérés par rareté, entrailles, sac de sang ; pas de trophée ; assets vanilla réutilisés |
| Nids et spawn | Un petit nid par salle de mine + quelques vers isolés ; le nid pond ; max 25 vivants par nid, 200 par amas, 200 par mine (décision 191) ; mine propre une fois les nids détruits ; jamais hors mine |
| Performance | Priorité absolue : pas de 400 Characters, vers « virtuels » simulés en tableaux, rendu instancié, budgets chiffrés (§11) |
| Livraison | Tout d'un bloc, avec un harnais de test in-game (§15) pour que Claude Code vérifie lui-même chaque règle |
| Distribution | Thunderstore public ; serveur dédié générique, aucun hébergeur imposé (décision 233) ; configuration BepInEx imposée par le serveur |

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
| Unity Editor | 6000.0.75f1, **seulement pour reconstruire le bundle de shaders** (décision 179) | Projet `assets/unity/MWShaders`, lancé en `batchmode` par `tools/build-shaders.ps1` ; le bundle `assets/build/mistworms_shaders` est versionné, donc le build du mod ne demande pas l'éditeur |

Claude Code vérifie les versions réelles au début de la phase 0 (API Thunderstore `https://thunderstore.io/api/experimental/package/<ns>/<name>/`), les note dans `docs/versions.md` et les épingle dans le projet et le manifest.

### 1.2 Machines et rôles

| Machine | Rôle | Détails |
| --- | --- | --- |
| PC Windows (jeu) | Dev, build, tests client, serveur dédié local de test | Claude Code (CLI ou VS Code) installé dessus ; Steam et Valheim présents ; Unity 6000.0.75f1 installé pour le bundle de shaders (`tools/build-shaders.ps1`) |
| Serveur dédié | Serveur de jeu des potes | Vanilla aujourd'hui ; pas de déploiement chez un hébergeur précis pour l'instant (décision 233) : le mod s'installe comme n'importe quel autre mod (§17.2) |
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
    blender/                # make_worm.py, make_knots.py, make_strands.py, make_nest.py, render_icon.py, common.py
    unity/MWShaders/        # projet Unity 6000.0.75f1 : shaders et compute shaders de la colonie GPU (§13.5)
    build/                  # sortie : *.glb, *.png, bundle mistworms_shaders (versionnés, régénérables)
  tools/                    # build-shaders.ps1 (bundle Unity), install-tools.ps1, setup-game.ps1, setup-server.ps1, build.ps1, run-game.ps1, run-server.ps1, vh.py, package.ps1, decompile.ps1
  thunderstore/             # manifest.json, README.md, CHANGELOG.md, icon.png (généré)
  docs/                     # versions.md, vanilla-reference.md (dump phase 0), decisions.md, test-report.md
  reference/                # decompiled/ (gitignoré), game-dlls/ (gitignoré)
```

Le build (`tools/build.ps1 -Configuration Debug|Profile|Release`) compile, copie `MistWorms.dll` + `assets/build/` dans `C:\ValheimDev\BepInEx\plugins\MistWorms\` (et dans le serveur local), et la build Release exclut tout le dossier `Dev/` par la constante de compilation `MW_DEV`.

## 2. Noms, identifiants et localisation

Le mod s'appelle **MistWorms** ; ses trois entités visibles sont le ver, l'amas et le nid. Tous les noms affichés passent par la localisation Jötunn (français + anglais), jamais en dur dans le code.

| Élément | Identifiant | Français | Anglais |
| --- | --- | --- | --- |
| Plugin GUID | `dev.pointvirgule.mistworms` | — | — |
| Nom du plugin / package Thunderstore | `MistWorms` (namespace Thunderstore = compte de l'utilisateur, à renseigner dans `manifest.json`) | — | — |
| Namespace C# | `MistWorms` | — | — |
| Fichier de config | `BepInEx/config/dev.pointvirgule.mistworms.cfg` | — | — |
| Amas (prefab créature) | `MW_WritheMass`, token `$mw_mass` | Amas grouillant | Writhing Mass |
| Nom au survol de l'amas | `$mw_mass_count` = « {0} ({1}) » | Amas grouillant (40) | Writhing Mass (40) |
| Nid (prefab destructible) | `MW_WormNest`, token `$mw_nest` | Nid de vers | Worm Nest |
| Colonie (prefab invisible, un par mine) | `MW_WormColony` | — | — |
| Effet de statut | `MW_SE_Infested`, tokens `$mw_se_infested_name` / `$mw_se_infested_desc` | Infesté / « Des vers vous recouvrent. Vitesse réduite, votre équipement est dévoré. » | Infested / "Worms cover you. Speed reduced, your equipment is being eaten." |
| Texte flottant de morsure | `$mw_text_eq` = « Équipement −{0} » / "Gear −{0}" | Équipement −12 | Gear −12 |
| Message de mort (vanilla, via le nom de l'attaquant) | nom de l'amas | « Tué par Amas grouillant » | "Killed by Writhing Mass" |
| Commandes console (build Debug uniquement, sauf `mw_version`) | préfixe `mw_` : `mw_version`, `mw_spawn <n> [rayon]`, `mw_mass <n>`, `mw_nest`, `mw_kill`, `mw_state`, `mw_tpmine`, `mw_scenario <nom>` (commandes et messages de dev en anglais ; `mw_wake`, `mw_calm` et `mw_perf` supprimées ; en Release, seule `mw_version` est exposée, décision 237) | — | — |

Règles : les fichiers `Localization/fr.json` et `Localization/en.json` contiennent tous les tokens ; l'anglais est la langue de repli. Les noms de prefabs vanilla utilisés (base de clonage de l'amas, items de drop, sons, effets) sont listés en annexe (§20) avec leur statut de vérification ; ils sont centralisés dans une classe `VanillaNames` et jamais répétés en chaîne littérale ailleurs.

## 3. Design — le ver seul

Un ver seul est une menace dérisoire : 10 PV, un coup de n'importe quelle arme en bronze ou mieux le tue, et sa seule attaque, le coup de cobra, grignote 2 points de durabilité. Sa vraie fonction est de rejoindre les autres : dès qu'un ver repère un joueur, il cherche d'abord un groupe à rejoindre et n'attaque seul que s'il n'y en a aucun à portée.

### 3.1 Caractéristiques

| Caractéristique | Valeur par défaut | Clé de config | Justification |
| --- | --- | --- | --- |
| Points de vie | 10 | `Worm.Health` | PV d'une tique ÷ 5 (la tique a 50 PV en 1.0.20, dump du 24 oct. 2026 ; 10 PV conservés par décision) ; un couteau en silex (≈ 12) ou une massue (≈ 12) tue en un coup, une flèche en bois (≈ 22 × 0,5 perçant = 11) aussi, un poing non |
| Longueur / largeur | 0,30 m / 0,018 m (tête 0,021 m) | — | Choix de l'utilisateur (décision 178) : un ver court et fin ; l'impression de masse vient du nombre de vers, 10 par unité de jeu, simulés par le GPU (§12.1). L'utilisateur a vu 0,15 m / 0,009 m en jeu (décision 169) et a demandé de doubler. Avant la refonte V2 : 0,40 m / 0,06 m |
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

1. **Sleeping** — dormant autour de son nid (§5), à demi enfoui, animation de pulsation lente. Son pas est de 2 Hz : il garde son sol et guette un joueur (vue et ouïe dormantes, §3.1). Se réveille : groupe alerté (§5.1), joueur dans la vue/ouïe dormante, ou dégâts infligés **par un joueur** (à lui, à son nid ou à un amas de son groupe), ce qui réveille aussi les dormants à moins de 5 m du coup. Un coup sans attaquant joueur ne réveille rien. Au réveil il passe Idle après un petit délai propre (0 à 0,4 s, déterministe) pour que le groupe ne démarre pas en bloc ; sa position de réveil devient son `home` et son `den` (décision 201).
2. **Idle** — actif sans cible : erre dans un rayon de 6 m autour de sa **position de réveil** (ou de création, pour un ver sans réveil), pauses aléatoires de 1 à 3 s.
3. **Gathering** — a détecté un joueur et il existe un amas ou un groupe (≥ 2 autres vers actifs) à moins de 20 m : se dirige vers l'amas le plus proche, sinon vers le centroïde du groupe. À moins de `Mass.FormRadius` du centroïde avec ≥ 3 vers → formation d'un amas (§4.1). À moins de `r × 1,3 + 0,5 m` d'un amas (enveloppe de l'amas) → passe en Absorbing (§4.1). Alignement avec les voisins de même état (voir « Comportement de groupe » ci-dessous).
4. **Hunting** — a détecté un joueur et rien à rejoindre : se dirige vers la cible la plus proche (règles de ciblage §5) ; à ≤ 1,2 m passe en Striking. Les vers qui chassent la même cible prennent chacun une place d'approche autour d'elle, et s'alignent sur leurs voisins en Hunting (voir « Comportement de groupe » ci-dessous).
5. **Striking** — préparation (se dresse), frappe (sphère de 0,5 m devant la tête), récupération ; puis Hunting. Réévalue « quelqu'un à rejoindre ? » à chaque récupération.
6. **Fleeing** — après la dissolution d'un amas (§4.4) ou si un feu est à moins de `Fire.FearRadius` : s'éloigne pendant `Worm.FleeDuration` (4 s) à vitesse × 1,35, puis Gathering si un groupe existe à moins de `Mass.RegroupRadius` (30 m), sinon Idle. La durée de fuite d'un amas est différente : il suit `Fire.FearDuration` (3 s, §9.1, phase 5).
7. **Absorbing** — absorption en cours (0,5 s, §4.1) : le ver grimpe sur le tas et devient un ver « flux » (rien ne s'enfonce, §12.4). `n` augmente à la fin de l'animation. Pendant l'animation, le ver reste un ver à part entière : simulé, frappable, proxy actif. S'il meurt, il n'est pas absorbé (`n` n'augmente pas) et passe en Dead.
8. **Absorbed** — fait partie d'un amas : plus simulé ni rendu individuellement, seul son compteur et son origine (nid) sont conservés dans l'amas.
9. **Dead** — effet visuel de mort (§12.4), drops (§7), slot libéré.
10. **Calming** (phase 7-2) — le groupe du ver s'est rendormi (§5.2) : il abandonne sa cible, marche vers son `den` (le lieu où il dormait) avec la locomotion d'Idle, à pleine vitesse, et passe Sleeping à moins de 0,8 m du `den`, ou après 20 s sur place. Il ne rejoint aucun amas ni groupe. Un réveil du groupe pendant le retour le rend Idle. (Valeur 6 de l'énumération, entre Fleeing et Absorbing.)

**Comportement de groupe des vers libres.** Les poids et rayons sont des constantes nommées documentées, pas des clés de config (§14). La séparation, la cohésion et l'alignement sont détaillés en §11.1.

- **File indienne** : si un ver qui va dans la même direction (écart < 45°) est devant à moins de 0,6 m, le suiveur cale sa vitesse sur la sienne au lieu de le doubler en le traversant. Les vers qui visent le même but réutilisent le même chemin (cache existant, §11.1), donc les couloirs produisent des files.
- **Places d'approche** : les vers qui chassent la même cible se répartissent autour d'elle. Chaque ver reçoit un angle d'approche, dérivé de son indice, réparti sur 360° parmi les chasseurs de cette cible. Il vise le point à `Worm.CobraRange` de la cible dans cette direction s'il est atteignable (sol, pas de mur), sinon le point direct. Ils encerclent au lieu de s'empiler sur un point.
- **Cohésion légère** en errance (Idle) : poids 0,2 vers le centre des voisins à moins de 3 m ; les vers errent en grappes lâches.
- **Alignement** en Gathering et en Hunting : poids 0,3 vers le cap moyen des voisins de même état.
- Gathering : il vise en priorité l'amas de la colonie le plus proche qui a de la place (recalculé à chaque pas), sinon le centroïde du groupe. Un ver en Hunting qui voit un amas de sa colonie bascule en Gathering.
- **Déblocage** : un ver qui progresse de moins de 0,5 m en 3 s hors de portée de sa cible fait 2 s de pas directs vers son but (décision 120).

Un ver ne saute pas, ne nage pas, ne grimpe pas aux murs ; il franchit une marche ≤ 0,4 m et une pente ≤ 60°. Il ne despawn jamais tant que la mine est chargée : son état est persisté (§11.4).

## 4. Design — l'amas

L'amas est un seul être aux yeux du jeu : une créature (`Character`) dont les points de vie, les dégâts et la taille sont la somme de ses vers, et dont le nom au survol affiche ce compte. Il n'y a jamais 40 entités : il y en a une, qui porte un compteur `n` et un pool de butin.

### 4.1 Formation et absorption

- Un amas naît quand ≥ `Mass.FormThreshold` (3) vers actifs en état Gathering sont à moins de `Mass.FormRadius` (2,5 m) de leur centroïde : la colonie crée un `MW_WritheMass` au centroïde avec `n` = nombre de vers, et retire ces vers de la simulation (état Absorbed). Rendu seulement, la logique reste celle de la spec : les vers rassemblés deviennent les premiers vers du tas et glissent vers lui en 0,4 s, tandis que le tas pousse de 0 à r(n) en 0,5 s (§12.4).
- Un ver actif à moins de `r × 1,3 + 0,5 m` d'un amas (enveloppe de l'amas) passe en Absorbing (0,5 s, §3.3) ; à la fin de l'animation il est absorbé : `n` += 1, son pool (§7) fusionne, son nid d'origine est compté dans `nestCounts` de l'amas.
- Deux amas en contact (distance horizontale < max(r1 + r2, 1,3 m), décision 185 ; capsules de 0,5 m) fusionnent si `n1 + n2 ≤ Mass.MaxCount` (200) : le plus gros absorbe le plus petit (compteur, pool, nestCounts). Sinon, le plus petit transfère exactement `200 − n1` vers au plus gros et continue seul avec le reste ; un amas plein (200) n'absorbe plus rien. Le pool et `nestCounts` sont transférés au prorata des vers transférés ; le `partial` (§4.2) du plus petit est perdu dans une fusion complète, et conservé par le reste dans une fusion partielle. Un reste qui passe sous `Mass.DissolveThreshold` à la suite d'une fusion partielle (sans perte par dégâts) ne se dissout pas : seules les pertes déclenchent la dissolution (§4.4). Rendu : le plus petit amas glisse vers le plus gros ; ses vers coulent vers le gros, chacun vers une place libre, en 0,5 s, et le gros grossit avec le lissage du rayon (§4.5, §12.4). Recherche de fusion : un amas qui en voit un plus gros de sa colonie à moins de 8 m le rejoint, prioritairement sur la chasse pendant 6 s au plus ; deux grappes formées de part et d'autre d'un joueur finissent ainsi en un seul amas.
- Les vers libres qui ne peuvent rejoindre un amas plein forment entre eux un nouvel amas selon la règle de formation.
- Un amas formé à 3 ou 4 vers (sous `Mass.DissolveThreshold`) vit jusqu'à sa première perte : la dissolution ne s'applique qu'après une perte (§4.4).

### 4.2 Points de vie et dégâts reçus

- `maxHealth = n × Worm.Health` ; `health` suit `n` (mise à jour de `Character.SetMaxHealth` / `SetHealth` à chaque changement de `n`). Les PV sont exactement `n × Worm.Health`, sans multiplicateur NG+ ni modificateur de monde ; ils sont réimposés après `Character.Awake` (qui recalcule les PV max d'un non-joueur, décision 1.3), et `SetHealth` est appelé explicitement quand `n` augmente (absorption, fusion). Modèle de vie réalisé en 4B : la santé vanilla est la source de vérité, `n = ceil(hp / Worm.Health)` et `partial = n × Worm.Health − hp` ; la vie est réimposée au `Start` ; le feu passe par le statut vanilla `Burning` (dégâts par ticks) et la régénération vanilla est désactivée (décision 101).
- Chaque coup reçu de dégâts effectifs `D` (après résistances §3.2 et blocage vanilla) fait : `partial += D` ; `kills = floor(partial / Worm.Health)` ; `partial −= kills × Worm.Health` ; `kills = min(kills, n)` ; `n −= kills`. Un marteau à 55 contondant sur un amas de 40 tue donc 5 vers, et le reste (5) est mémorisé pour le coup suivant. Les armes de zone (`Aoe`) touchent l'amas une fois par instance d'`Aoe`, comme toute créature.
- Chaque ver tué rend une part du pool (§7.3). Rendu : un ver de surface se détache, glisse et tombe au sol, puis l'effet de mort vert (§12.4) se joue (plafond de 10 par seconde inchangé).
- `n = 0` → l'amas meurt (vanilla `Character.OnDeath`), le reliquat du pool est droppé (§7.3). Aucun cadavre, aucun ragdoll.

### 4.3 Nom, HUD, niveau

- Nom au survol : « Amas grouillant (n) », recalculé à chaque changement de `n` (patch `Character.GetHoverName`). L'`EnemyHud` vanilla affiche la barre de PV comme pour toute créature ; aucune étoile (`level = 1`).
- L'amas apparaît dans le message de mort vanilla et dans les trophées de combat comme une créature normale.

### 4.4 Dissolution et fuite

- Si `n` tombe sous `Mass.DissolveThreshold` (5) **par suite de dégâts** (une perte, §4.2), l'amas se dissout : `n` vers libres sont recréés autour de sa position en état Fleeing (§3.3), le pool est réparti à parts égales entre eux (fractions autorisées), l'entité amas est détruite. La dissolution ne s'applique qu'après une perte : un amas formé à 3 ou 4 vers (§4.1) vit jusqu'à sa première perte. Elle est distincte du retour à la dormance (§5.2). Rendu : le tas se résorbe en 0,4 s, et les `n` vers libres partent des positions des vers visibles du tas, en un petit bond (hauteur 0,3 à 0,6 m, distance 0,5 à 1,5 m, sol vérifié par `WormPhysics`), avant de passer en Fleeing.
- Les fuyards fuient `Worm.FleeDuration` (4 s), puis cherchent un groupe à moins de `Mass.RegroupRadius` (30 m) ; trois fuyards qui se retrouvent reforment un amas. Les fuyards se regroupent même sans joueur détecté : le ver compte dans son propre centroïde de groupe (§3.3).

### 4.5 Taille et forme

```latex
r(n) = R_{10} \left( \frac{n}{10} \right)^{e} \quad R_{10} = 0{,}26\ \text{m},\ e = 0{,}36
```

| n | r(n) | Diamètre | Hauteur | Repère visuel |
| --- | --- | --- | --- | --- |
| 3 | 0,17 m | 0,34 m | 0,14 m | une poignée de vers |
| 10 | 0,26 m | 0,52 m | 0,21 m | — |
| 20 | 0,33 m | 0,67 m | 0,27 m | — |
| 50 | 0,46 m | 0,93 m | 0,37 m | — |
| 100 | 0,60 m | 1,19 m | 0,48 m | — |
| 200 | 0,76 m | 1,53 m | 0,61 m | assez large pour envelopper un joueur (ancres sur son corps, §12.1) |

`R_{10}` = `Mass.BaseRadius` (0,26 m depuis la refonte visuelle V2-B, décisions 178 et 185 ; migration de config v3, ancien défaut 0,83 m), `e` = `Mass.RadiusExponent`. Hauteur de l'enveloppe de référence = 0,8 × r (hauteur de la zone de coup et du collider, pas du rendu).

**Calibrage (refonte visuelle V2-B, 25-26 oct. 2026, décisions 172 et 185).** L'ancien calibrage sur les créatures vanilla (tique `L` 1,04 m, soldat `L` 3,40 m, joueur témoin 1,905 m ; ancres r(10) = 0,825 m et r(200) = 2,42 m, mesures du 24 oct. 2026, dump §1b) est abandonné : la taille de jeu de l'amas rétrécit avec le visuel, par choix de l'utilisateur. Le ver mesure 30 cm × Ø 1,8 cm (décision 178). Avec 10 vers visuels par unité de jeu (§12.1), une compacité de 0,5 et un dôme de demi-hauteur 0,45 r, la loi garde son exposant `e` = 0,36 et `R_{10}` passe de 0,83 à 0,26 m (la valeur 0,35 m de la décision 172, calculée pour un ver de 15 cm, est remplacée), d'où r(10) = 0,26, r(50) = 0,46, r(200) = 0,76 m. Les repères « 10 ≈ tique », « 20 ≈ couvain » et « 200 un peu moins qu'un soldat » ne tiennent plus. Tout ce qui découle de `r` suit (volumes de coup, absorption, flux, cadrage). Valeurs absolues recalculées (décision 185) : `Mass.MaxColliderRadius` 0,5 m ; `Mass.DetachDistance` 2,5 m (migration v4) ; `Mass.EngulfRange` 0,6 m inchangée, mais comptée à partir de `max(r, rayon de la capsule de l'amas + 0,3 m)` et augmentée du rayon du corps d'une créature (§4.9) ; contact de fusion 1,3 m (§4.1). Restent à ajuster par calcul : `MaxHeightGap`, la sonde de coulée (§4.7). Le gameplay de la phase 5 ne change pas (évasion en sprint, engloutissement atteignable, vitesses) ; S03, S04 et S06 restent verts avec des seuils ajustés par calcul, S08 est ouvert (§16). La **sphère physique** (collider de la créature, utilisée pour le déplacement et pour recevoir les coups) est bornée à `Mass.MaxColliderRadius` (0,5 m) quel que soit `n` : la forme visuelle peut dépasser le collider, c'est voulu, cela permet le passage des couloirs (§4.7) et évite qu'un amas de 200 bloque physiquement les joueurs. La hauteur du centre suit le sol (le collider repose au sol, l'enveloppe visuelle part du sol).

**Rayon affiché.** Rendu seulement : le rayon affiché d'un amas suit `r(n)` avec un lissage de 0,3 s, jamais de saut quand `n` change.

**Zone de coup.** Le collider physique reste borné à `Mass.MaxColliderRadius`. Les coups passent par des sphères **sur la couche `hitbox`** (pas de collision physique, décision 47), routées vers l'amas : en dôme, une sphère de rayon 0,9 × r(n), centrée à 0,4 × r(n) de hauteur (elle suit donc le nouveau `r(n)`) ; en Stream (§4.7), une chaîne de 6 sphères `hitbox` préallouées le long du ruban. Un `Aoe` touche l'amas une fois (§4.2).

**Rendu sans noyau.** Le « dôme » de ce chapitre est l'enveloppe de référence (demi-ellipsoïde de rayon `r(n)` et de hauteur 0,8 r) de la zone de coup. Visuellement, l'amas est **uniquement fait de vers** (aucun noyau, aucune forme pleine au centre, décisions 105 et 180) : 10 vers visuels par unité de jeu, chacun une corde de 8 nœuds simulée par le GPU, qui coule, s'étale, épouse le décor et enveloppe ce qu'il traverse (§12.1, décisions 181 à 184). Le gameplay ne dépend pas du rendu : `r(n)`, la table ci-dessus, le collider, les sphères de coup et `n`.

### 4.6 Déplacement

- Vitesse `Mass.MoveSpeed` = 2,4 m/s, égale à celle du ver seul ; pas de saut, pas de nage ; pathfinding vanilla avec `Pathfinding.AgentType.Humanoid` (plus petit gabarit marcheur, `MassPrefab.cs`) ; les escaliers des mines passent avec cet agent (S08b, `stairs_climbed` = ok ; décision 24).
- Rotation : l'amas ne « se tourne » pas, il glisse ; les vers de surface rampent dans le sens du mouvement (§12).
- Hors combat (actif sans cible), l'amas erre à 6 m de son dernier point d'intérêt et retourne vers son nid d'origine majoritaire quand la colonie se calme (§5.2).

- **Aide de marche** : une aide `StepAssist` soulève l'amas de 0,35 m pour franchir une marche ; elle est limitée à l'amas (décision 102).
- **Filet anti-chute** : l'amas n'est remis à sa dernière position au sol que dans trois cas : aucun sol n'est sous lui (raycast de 50 m), il est sous le terrain en extérieur (y < hauteur du terrain − 1 m), ou il tombe à plus de 30 m/s. Une chute vers un sol réel est laissée à la physique : un amas peut sauter d'un rebord (décision 119).

### 4.7 Passages étroits — la « coulée »

L'amas se scinde et se reforme visuellement, sans jamais se scinder en entités. Chaque 0,25 s, l'amas sonde la largeur libre devant lui (sphère de rayon `r(n)` projetée sur 2 m le long du chemin) : si elle est inférieure à `2 r(n) + 0,2 m`, il passe en mode **Stream** (une forme, pas un état, §4.10). Le rendu est alors un **ruban** entre deux murs, que la simulation GPU remplit naturellement (§12.1, décision 181) : la forme cible s'aplatit (hauteur × `Mass.StreamHeightFactor`, 0,4) et s'allonge le long du chemin (longueur = min(n × `Mass.StreamLengthPerWorm` (0,08 m), `Mass.StreamMaxLength` (6 m)), largeur = largeur libre − 0,1 m) ; les vers s'alignent dans l'écoulement. La zone de coup suit la coulée par une chaîne de 6 sphères `hitbox` préallouées le long du ruban (§4.5), pour rester frappable. Dès que 3 m de chemin sont libres, retour à la forme dôme. La transition dôme ↔ ruban dure 0,5 s dans les deux sens. La vitesse ne change pas. Les autres paramètres de la coulée (période de sondage 0,25 s, projection 2 m, marges 0,2 m et 0,1 m, 3 m libres) sont des constantes nommées, pas des clés de config. **En cours (§16)** : le couloir de S08, plus étroit avec la nouvelle taille, n'est pas encore validé en jeu ; la sonde de largeur (projection, marges) reste à revoir.

### 4.8 Recul, étourdissement, parade

- Aucun étourdissement : `m_staggerWhenBlocked = false`, patch qui annule `Character.Stagger` et `AddStaggerDamage` pour `MW_WritheMass`.
- Recul : le facteur `clamp(10 / n, Mass.KnockbackMinFactor, 1)` s'applique à `HitData.m_pushForce` avant le recul vanilla (le point exact du patch sera choisi au dump) — un amas de 10 recule comme une tique, un amas de 200 bouge à peine.
- Parade : le blocage et la parade vanilla fonctionnent contre les morsures (§6.4) ; une parade parfaite (vanilla) ne stagger pas l'amas et, par règle du mod, **annule** ce tick de morsure (sans perte de durabilité, de PV ni d'effet) ; le tick suivant, retardé de 0,5 s, arrive 1,0 s après le tick annulé (0,5 s normal plus 0,5 s de retard). C'est une logique du mod, indépendante du fait que le vanilla n'annule pas le coup (décision 1.3). Phase 5 (S03b).

### 4.9 Engloutissement (résumé, détails §6)

Phase 5 (en phase 4B l'amas s'arrête au contact sans engloutir, §4.10). Rendu : l'engloutissement **visuel** est porté par les vers de la colonie GPU, qui habillent la surface réelle de la cible, sans effet d'écran (§6.6, §12.1, décision 184) ; un paramètre `engulf` de 0 à 1 le pilote (forcé en dev avant la phase 5, piloté par l'IA de l'amas ensuite).

En Hunt, quand la cible est à moins de la **portée d'engloutissement** (distance horizontale du centre de l'amas à la cible), l'amas passe en état Engulf (écrit dans `mw_state`, §4.10) et la recouvre. Portée (décision 185) : `max(r(n), rayon de la capsule de l'amas + 0,3 m) + Mass.EngulfRange` (0,6 m), plus, pour une créature cible, ce que son collider dépasse de 0,3 m (la capsule du joueur) ; les deux capsules ne pouvant pas se rapprocher de moins de 0,8 m, la portée vaut au moins 1,4 m pour un joueur, même pour un petit amas. L'amas **marche vers la cible** à `Mass.MoveSpeed` (2,4 m/s), arrêt à 0 m, sans téléport ; le retard de 0,15 s ne concerne que l'adhérence visuelle (la gaine suit la cible avec un lissage de 0,15 s). `Physics.IgnoreCollision` est posé entre la capsule de l'amas et ses sphères de coup d'une part, et tous les colliders de la cible d'autre part, puis retiré au détachement. La cible reçoit l'effet **Infesté** (§6.2) et le cycle de morsures commence. Un joueur ralenti qui sprinte (≈ 3,5 m/s) creuse l'écart.

Détachement, l'une des conditions suffit : distance supérieure à `r(n) + Mass.DetachDistance` (2,5 m depuis la migration v4, décision 185 : avec le petit rayon d'un amas de 40, l'ancienne valeur de 1,5 m laissait une limite de 1,9 m qu'une roulade suffit à franchir) pendant `Mass.DetachDelay` (0,75 s) ; feu (§9.1) ; cible morte, fantôme, téléportée ou déconnectée ; dissolution (perte faisant tomber `n` sous le seuil) ; `mw_kill`. Après le détachement : Infesté retiré, `IgnoreCollision` retiré, état Hunt ; si la cible est invalide, nouvelle cible au tick suivant (§9.2, §9.3). Phase 5 : cibles joueurs seulement (créatures et structures en phase 6).

### 4.10 États de l'amas

| État | Rôle | Phase |
| --- | --- | --- |
| Idle | errance, retour vers le nid (§4.6) | 4B |
| Hunt | se dirige vers sa cible (§5.3) ; à la portée d'engloutissement de sa cible joueur (§4.9) il passe en Engulf (phase 5 ; en 4B il s'arrêtait au contact sans engloutir) | 4B, 5 |
| Engulf | marche vers la cible (arrêt à 0 m), tick de morsure, `mw_state` Engulf (§4.9, §6) ; détachement → Hunt, ou fuite devant le feu pendant `Fire.FearDuration` puis Hunt (§9.1) | 5 |
| Dormant | le groupe de l'amas s'est rendormi : l'amas ne cible rien, marche vers le nid de son groupe puis se dissout en vers Sleeping autour (§5.2) ; un réveil du groupe le ramène à Idle (stub immobile en 4B, 7-2 pour la marche) | 7-2 |

Stream (§4.7) est une forme, pas un état : il se combine à n'importe quel état de déplacement et se synchronise par un bit séparé (§11.3).

## 5. Design — activation, dormance et cibles

La colonie ne s'éveille que pour un joueur ; une fois éveillée, elle dévore tout ce qui bouge ou qui tient debout, joueurs, apprivoisés, fouineurs, dvergar et constructions, puis se rendort quand plus aucun joueur n'est là.

### 5.1 Réveil

- Un **groupe de nid** est Dormant ou Actif. Il réunit le nid d'indice k, tous les vers dont `nestIndex` vaut k (les isolés rattachés compris) et les amas dont le nid majoritaire des `nestCounts` est k ; un ver sans nid (`nestIndex` -1) est un groupe d'un seul. Le propriétaire de la colonie tient l'état (un drapeau par groupe) et le publie dans le ZDO de la colonie (`mw_awake`, un bit par groupe), où le propriétaire d'un amas le lit (décision 200). Au réveil d'un nid, tous ses vers endormis passent Idle (avec un petit délai propre à chacun, §3.3) puis Gathering/Hunting, et l'alerte se propage à tout nid à moins de `Colony.AlertRadius` (25 m), nid après nid, en parcours en largeur (une alerte ne traverse pas plus de 2 sauts par seconde : chaque saut réveille le groupe voisin 0,5 s plus tard, pour que l'éveil d'une grande mine se fasse en vague, pas instantanément). Un groupe dont le nid n'existe plus ne reçoit ni ne transmet l'alerte.
- Déclencheurs de réveil, uniquement par un **joueur** (`Targets.WakeOnlyByPlayers = true` ; à faux, tout `Character` qui n'est ni un joueur ni un amas compte) : joueur dans la vue/ouïe dormante d'un ver (§3.1 ; l'ouïe suit la règle vanilla avec le bruit du joueur, un joueur accroupi passe plus près), joueur à moins de `Nest.WakeRadius` (10 m, **distance 3D** de l'œil du nid, 0,4 m au-dessus de son point, au centre du joueur, `GetCenterPoint`) du nid en ligne de vue (test à 2 Hz tant que le groupe dort), dégâts infligés par un joueur à un ver, un nid ou un amas (quel que soit l'attaquant réel : un fouineur qui tue un ver dormant ne réveille rien, un joueur si ; l'attaquant se lit par `HitData.GetAttacker()`). Le groupe touché est alerté (pour un amas, chaque groupe de ses `nestCounts`) et les vers endormis à moins de 5 m du point d'impact se réveillent aussi, quel que soit leur groupe. Les créatures vanilla ne peuvent pas cibler un ver dormant : il n'existe pas pour leur IA (§10).
- Il n'y a **pas d'amas dormant persistant** (corrige la formulation de 4B : « un amas dormant reste une entité ») : une colonie qui se rendort fait rentrer ses amas au nid, où ils se dissolvent en vers Sleeping (§5.2) ; le mode `Dormant` de l'amas (§4.10) n'est que ce trajet de retour. Un amas Dormant ne cible rien ; un coup d'un joueur sur lui, ou le réveil de son groupe, le remet à Idle.

### 5.2 Retour au calme

- Le propriétaire de la colonie évalue chaque groupe Actif à 1 Hz sur la **boîte englobante de ses membres** (position du nid, vers libres non endormis, amas du groupe). Le groupe reste Actif tant qu'un joueur est à moins de `Targets.ActiveRadius` (45 m) de cette boîte (mesure depuis un membre, pas depuis la boîte de la mine). Après `Targets.CalmDelay` (60 s) sans joueur, il se rendort : les vers libres passent en Calming (§3.3 point 10, y compris ceux qui se rassemblent, absorbent ou fuient) et retournent dormir à leur `den` en 20 s au plus, sinon sur place ; les amas rentrent vers le nid de leur groupe (leur nid majoritaire), puis, à moins de 1,5 m, après 30 s ou si le nid a disparu, se dissolvent en vers Sleeping sur un anneau de 0,4 à 2 m autour du nid (sol accroché, sans bond ni fuite ; autour de l'amas lui-même s'il est resté à plus de 3 m du nid). Le compte, le pool (à parts égales entre les dormants) et les origines (`nestIndex` redonné exactement selon les `nestCounts`) sont conservés (§11.4). Un amas sans nid de groupe (fait d'isolés) tient son propre délai de calme et se dissout sur place. Un ver sans nid est un groupe d'un seul : mêmes règles à partir de sa position.
- Un joueur qui revient réveille tout de nouveau ; le butin dévoré n'est jamais perdu par la dormance.

### 5.3 Cibles et priorités (groupe Actif)

| Catégorie | Attaquée ? | Clé | Poids de distance |
| --- | --- | --- | --- |
| Joueurs | oui | `Targets.AttackPlayers` | 0,6 (`Targets.PlayerWeight`) |
| Créatures apprivoisées | oui | `Targets.AttackTamed` | 0,8 |
| Créatures sauvages (fouineurs, tiques, couvains, soldats, dvergar…) | oui | `Targets.AttackCreatures` | 1,0 |
| Structures (`WearNTear` : pièces construites par les joueurs, portes ; `Destructible` dvergr de la mine, ci-dessous) | oui, en repli ou en obstacle seulement (ci-dessous) | `Targets.AttackStructures` | 1,5 (départage les structures entre elles) |
| Nids, vers, amas, pierres tombales, portails, objets non destructibles | jamais | — | — |

Structures cibles : les `WearNTear` et les `Destructible` dvergr de la mine, reconnus par préfixe de prefab (`dvergrtown_`, `dvergrprops_`, constantes de `VanillaNames`). Ce ne sont jamais les nids, ni d'autres `Destructible`.

Choix de cible, réévalué toutes les 0,5 s :

- **Distance** : 3D, au point le plus proche (`Character` : son centre ; structure : le collider le plus proche). Un collider `MeshCollider` non convexe est mesuré sur sa surface réelle (rayon vers le centre de ses bounds, puis vers le point le plus proche de ses bounds) ; sans surface touchée, il n'y a pas de contact et la structure n'est pas une cible. La frappe du cobra et l'engloutissement utilisent la même mesure.
- **Atteignabilité** : l'écart vertical entre la cible (structure : son point le plus proche ; `Character` : l'étendue verticale de son collider) et le sol de l'attaquant doit être inférieur ou égal à sa portée verticale réelle : œil 0,15 m + `Worm.CobraRange` pour un ver, `Mass`/engloutissement `MaxHeightGap` (2,5 m) pour un amas. Une poutre à 3 m au-dessus d'un ver, ou une chauve-souris, n'est pas une cible.
- **Liste noire** : si la distance à une cible (créature ou structure) ne diminue pas d'au moins 0,5 m en 4 s hors frappe, hors engloutissement et hors portée de frappe, la cible est écartée 10 s pour ce ver ou cet amas (4 entrées par ver et par amas, sans allocation).
- **Visibilité** : linecast `ViewBlockMask` depuis l'œil du ver ou de l'amas jusqu'au centre de la cible, en ignorant le collider de la cible.
- **Score** : `distance × poids` ; parmi les cibles **vivantes** (joueurs, apprivoisés, créatures) visibles à moins de `Mass.TargetRange` (20 m), la plus petite valeur gagne. Une **structure** n'entre pas dans cette comparaison : elle n'est choisie que si aucune cible vivante n'est perçue (repli : la colonie ronge les constructions quand personne n'est là), et `Targets.StructureWeight` ne sert alors qu'à départager les structures entre elles (décision 164).
- **Règle d'obstacle** : pendant la chasse d'une cible vivante, si la progression cale (même veille de 4 s que la liste noire), la **première structure ciblable touchée** par un `SphereCast` du centre de l'attaquant vers la cible, à portée d'engloutissement (`r(n)` + `Mass.EngulfRange` + 0,5 m) ou de cobra, devient la cible : on ronge la porte derrière laquelle le joueur se cache. Les pièces dont le haut est sous les pieds + 0,3 m (sols) sont ignorées ; la liste noire est respectée. Elle reste la cible jusqu'à sa destruction, un détachement ou son éloignement, puis la chasse reprend. Un joueur n'est jamais mis sur liste noire par cette veille. Le repli sur structure n'est permis qu'après `ForgetTime` (5 s) sans cible vivante perçue (décision 166).
- Le `FindEnemy` vanilla n'est pas utilisé : il n'a pas de poids.

Suite : une cible engloutie est conservée jusqu'au détachement (§4.9). Un ver seul suit les mêmes règles avec `Worm.ViewRange` (20 m) pour portée de ciblage (§3.1). Les vers virtuels ne sont pas des `Character` : leur ciblage applique la règle de faction de l'amas (§5.4) par notre propre code, sans patch d'`IsEnemy`. Les prefabs listés dans `Targets.ExcludedPrefabs` (défaut `SW_DivineWhale`) ne sont jamais ciblés, ni par les vers ni par les amas (§10.4 ; phase 6, plus la sélection de cibles de l'amas en 4B). Les joueurs sont donc préférés à distance égale, mais un fouineur collé à l'amas sera mangé avant un joueur à 15 m.

### 5.4 Factions et guerre dans la mine

L'amas doit être ennemi de toutes les factions, y compris `MistlandsMonsters` et `Dverger`, pour que fouineurs et dvergar ripostent et pour que la mine devienne un champ de bataille à trois camps. Implémentation : faction `PlayerSpawned`, hostile à toutes les autres dans les deux sens, sauf le mannequin d'entraînement qui ne riposte pas (`Faction.Boss` ne convient pas, décision 1.1 ligne 28), sans postfix sur `BaseAI.IsEnemy` pour l'amas. Conséquence voulue : les créatures de la mine tuent les amas (décision 121). Le harnais (§15) imprime la matrice `IsEnemy` réelle (un amas contre une créature de chaque faction présente) et S19 la compare exactement à la matrice ci-dessous, lue dans `BaseAI.IsEnemy` (`BaseAI.cs` : cas `PlayerSpawned` du `switch`, ligne 1232 ; cas `TrainingDummy`, lignes 1285-1286 ; branche apprivoisée, lignes 1214-1221).

| Autre côté | `IsEnemy(amas, autre)` | `IsEnemy(autre, amas)` |
| --- | --- | --- |
| Toute faction vanilla (joueurs, `MistlandsMonsters`, `Dverger`, `ForestMonsters`, etc.) | true | true |
| `PlayerSpawned` (autre amas) | false | false |
| `TrainingDummy` | true | false |
| Créature apprivoisée, de n'importe quelle faction | true | true |
| Baleine SkyWhale (postfix SkyWhale, §10.4) | false | false |

`IsEnemy(amas, mannequin)` est vrai parce que le cas `PlayerSpawned` n'a pas d'exception ; `IsEnemy(mannequin, amas)` est faux parce que le mannequin ne combat que les joueurs. Le ciblage du mod ignore tout de même le mannequin (`TargetRules.FactionTargetable`). Pour une créature apprivoisée, les exceptions vanilla ne concernent que les joueurs, les Dverger et un autre apprivoisé : contre l'amas, vrai dans les deux sens.

Les vers virtuels suivent la même règle (§5.3).

Quand l'amas tue une créature ou détruit une structure, ce qu'elles auraient laissé tomber va dans son pool au lieu du sol (§7.4) : « ça drop tout ce que ça a bouffé ».

## 6. Design — engloutissement, morsures et équipement

Les morsures d'un amas ou d'un ver ne touchent jamais les points de vie d'un joueur tant qu'il lui reste un objet à dévorer : elles consomment la durabilité de son inventaire, pièce au hasard, Wisplight en dernier, et ne passent aux PV qu'une fois tout détruit.

### 6.1 Le tick de morsure

Toutes les `Worm.BiteInterval` (0,5 s), l'amas englouti autour d'une cible produit **un seul coup agrégé** (une `HitData`) : `P = n × Worm.DurabilityPerBite` points pour un joueur, `n × Worm.CreatureDamagePerBite` dégâts perçants pour une créature, `n × Worm.StructureDamagePerBite` contondants pour une structure. Agréger par tick (plutôt que 40 micro-coups) garde le réseau et le blocage vanilla sains. Le coup est émis par le propriétaire de l'amas avec `m_attacker = ZDOID de l'amas` (un vrai `Character`, ce qui permet la riposte et le blocage vanilla ; le cobra du ver seul reste « sans attaquant », décision 20). Contenu de la `HitData` pour un joueur : perçant `n × Worm.HealthDamagePerBite`, `m_blockable` = `m_dodgeable` = true. `P = n × Worm.DurabilityPerBite`, multiplié par le multiplicateur de monde si `Engulf.ScaleWithWorldModifiers`, voyage dans un champ de contexte : une RPC `MW_MassBite(ZDOID mass, float P, float hp, Vector3 point)` vers le propriétaire de la cible ouvre le `WormBiteContext` puis appelle `target.Damage(hit)` localement, exactement comme le cobra ; `DamagePatches` est ainsi réutilisé sans changer sa condition. Les règles ci-dessous s'appliquent chez le propriétaire de la cible.

### 6.2 Effet « Infesté » sur la cible

- Appliqué à l'engloutissement, retiré au détachement (`SEMan` vanilla, `StatusEffect` custom `MW_SE_Infested` enregistré dans l'`ObjectDB` par Jötunn `CustomStatusEffect`, icône vanilla réutilisée : celle de l'effet `Slimed`, sinon celle de `Poison`, sinon aucune icône (`InfestedEffect.cs`) ; aucun effet d'accroche de la tique n'est identifié dans le dump confirmé en jeu le 2026-10-27 : `[Infested] icon of 'Slimed' (preferred), sprite 'Slimed'; registered in the ObjectDB: True`). Il est appliqué et retiré **chez le propriétaire de la cible**, par RPC `MW_Infest(bool)` depuis le propriétaire de l'amas ; il est maintenu tant que l'état Engulf est actif (rafraîchissement toutes les 0,5 s pour couvrir une perte de RPC, durée de 1,5 s qui expire seule si l'amas disparaît).
- Effets : `m_speedModifier` = `Engulf.SlowFactor − 1` (ou l'équivalent vanilla trouvé au décompilé), soit une vitesse de déplacement × 0,5 y compris sprint ; attaques, blocage, parade, roulade, consommables et interactions autorisés ; aucun dégât périodique propre.
- La roulade ne libère pas (l'amas suit) mais ses frames d'invincibilité vanilla annulent le tick de morsure qui tombe pendant la roulade : ce tick est **perdu**, pas reporté ; la roulade parfaite de Call to Arms est traitée par le jeu comme d'habitude.
- Se libérer : sprinter hors de portée (le joueur ralenti sprinte à ≈ 3,5 m/s, l'amas marche à 2,4 m/s : on s'en sort, lentement ; détachement au-delà de `r + DetachDistance` (2,5 m) pendant `DetachDelay`, §4.9), brandir du feu (§9.1), ou réduire l'amas sous le seuil de dissolution.

### 6.3 Ordre de destruction de l'inventaire (joueur)

1. **Éligibles** : par défaut (`Engulf.IncludeUnequippedInventory` = true), tous les objets de l'inventaire du joueur, équipés ou non (toutes les cases, emplacements rapides compris) ; si la clé est à false, seules les pièces **équipées**. Dans les deux cas : tels que `m_shared.m_useDurability` et `m_durability > 0`, **sauf** le Wisplight (`Demister`, décisions 8 et 9) tant qu'il reste un autre éligible. Les objets sans durabilité (matériaux, flèches, nourriture, trophées, Megingjord…) ne sont jamais touchés.
2. **Nombre de pièces mordues par tick** : `k = clamp(ceil(n / 10 × Engulf.ItemsPerTenWorms), 1, nombre d'éligibles)` — un gros amas ronge plusieurs pièces à la fois.
3. `k` objets tirés au hasard sans remise parmi les éligibles ; chacun perd `P / k` points, plafonné à sa durabilité restante ; l'excédent est reporté sur un autre éligible tiré au hasard, jusqu'à épuisement de `P` ou des éligibles.
4. **Wisplight en dernier** (`Engulf.WisplightLast = true`) : il ne devient éligible que lorsqu'aucun autre éligible ne subsiste. Sa destruction ramène la brume : c'est le coup de grâce voulu. Les valeurs de durabilité du `Demister` sont lues au démarrage (`ObjectDB` prêt) et journalisées ; s'il n'a pas de durabilité, le prefab `Engulf.WisplightPrefab` (`Demister`) reçoit une durabilité virtuelle (`m_useDurability` = true, `m_maxDurability` = `Engulf.WisplightVirtualDurability`, défaut 100, `m_durabilityPerLevel` = 0), conditionnée à la clé de config ; les instances déjà dans des inventaires qui étaient à 0 sans durabilité reçoivent `m_durability` = max (première fois seulement). Il ne s'use alors que sous les morsures, se casse à 0 comme une pièce d'armure (comportement vanilla) et se répare gratuitement à l'établi, comme le reste.
5. **Phase PV** : le reste de `P` va aux PV **dans le même tick** quand les éligibles sont épuisés en cours de tick, au prorata `reste / P × dégâts PV` (`n × Worm.HealthDamagePerBite` dégâts perçants par tick, via le chemin vanilla ; ce reste remplace la décision 54 (3) « reste de P perdu », décision 131). S'il ne reste aucun éligible (Wisplight inclus) au début du tick, tout le tick va aux PV (l'armure est nulle à ce stade si toutes les pièces sont cassées ; un bouclier levé mais cassé ne bloque plus non plus). Le coup de cobra d'un ver seul garde ses 2 dégâts perçants (`Worm.CobraCreatureDamage`) quand il ne reste aucun éligible.
6. Un objet à 0 suit le comportement vanilla : cassé, déséquipé automatiquement, réparable gratuitement à l'établi (exploit assumé, §0) ; un objet vanilla `m_destroyBroken` disparaît comme en vanilla.

### 6.4 Blocage et armure

- **Blocage** : le coup agrégé passe par `Humanoid.BlockAttack` vanilla sur le client de la cible : bouclier levé et amas dans l'angle de blocage → réduction selon la puissance de blocage, perte de durabilité du bouclier et consommation d'endurance comme pour n'importe quel coup. Si `Engulf.BlockReducesBites`, la valeur retenue pour `P` est `P × (dégâts après blocage / dégâts avant)`. **Parade parfaite** (blocage dans la fenêtre de parade vanilla, Call to Arms) : le vanilla n'annule pas le coup (il multiplie seulement la puissance de blocage, décision 1.3), mais la règle du mod (§4.8) **annule** ce tick (ni durabilité, ni PV, ni effet) et le tick suivant arrive 1,0 s après le tick annulé ; l'amas n'est pas étourdi. Détection : un postfix de `Humanoid.BlockAttack` remonte « parade parfaite » au contexte de morsure (minuteur de parade vanilla, champ à identifier au décompilé).
- **Armure** : par défaut (`Engulf.ArmorReducesBites` = false), jamais appliquée aux morsures (préfixe sur `HitData.ApplyArmor` qui saute l'appel quand `m_attacker` est un amas ou un ver, et préfixe sur `Player.DamageArmorDurability`, décision 54). Si la clé est à true, l'armure vanilla réduit `P` comme des dégâts (ratio après/avant).
- **Esquive** : frames d'invincibilité de la roulade respectées (vanilla) ; le tick qui tombe pendant la roulade est perdu (§6.2).
- **Mode dieu / fantôme** (devcommands) : aucun coup appliqué (vanilla) ; le harnais désactive le mode dieu pendant les scénarios de morsure.

### 6.5 Créatures et structures

Phase 6 : en phase 5, l'amas ne cible que des joueurs (le ciblage « tout » arrive avec la phase 6).

- Créature engloutie (apprivoisée ou sauvage) : coup agrégé perçant, armure et résistances vanilla de la créature appliquées, riposte vanilla (la créature attaque l'amas). Pas d'effet Infesté sur les créatures (mais ralentissement : `StatusEffect` caché sans icône `MW_SE_Engulfed`, qui applique `m_speedModifier` = `Engulf.SlowFactor` − 1 ; appliqué par le propriétaire de la créature, sur le même modèle qu'Infesté, §6.2).
- Structure : coup agrégé contondant sur un `WearNTear` ou un `Destructible` dvergr (§5.3) ; une porte fermée est une cible valide (l'amas la mange plutôt que de la contourner) ; les pièces à durabilité infinie ou protégées par une pierre de garde suivent la règle vanilla (pas de dégâts).
- Ce que la cible aurait laissé tomber va dans le pool (§7.4).

### 6.6 Texte et retour visuel

- Chaque tick sur un joueur affiche (si `Engulf.ShowDurabilityText`) un texte flottant vanilla (`DamageText`, `TextType.Bonus`, orange, décision 52) au lieu du rouge des dégâts : « Équipement −12 » (valeur `P` effective), puis le nombre rouge vanilla en phase PV.
- Son : à chaque tick, le son de morsure de tique (nom vérifié au dump) avec le plafond existant ; le « son humide » est choisi au dump (`sfx_blob_*` ou `sfx_leech_*`), sinon omis.
- Quand une pièce casse, le message vanilla « X est cassé » suffit ; aucun message custom.
- Rendu de l'engloutissement (piloté par `engulf`, 0 à 1, et la cible ; décision 184) : les vers de la colonie GPU habillent la **vraie surface** de la cible, par des ancres skinnées sur le GPU à partir de ses matrices d'os. L'effectif de la gaine est la surface de la cible × 2 couches de vers ; elle se remplit de bas en haut (hauteur couverte = `engulf` × hauteur de la cible), les vers se plaquent sur le plan tangent de la peau, certains pendent en grappes ou tombent, la bordure est irrégulière. En jeu, 80 % de la gaine sont formés en 3,5 s. Aucun effet d'écran.
- Pour ne pas aveugler la caméra, les vers à moins de 0,5 m de la caméra ne sont pas rendus quand la caméra est à moins de `r + 0,7 m` du centre (phase 4B). Le dither (matériau translucide) n'est fait que si un shader vanilla le permet (à vérifier, sinon abandonné).

## 7. Design — drops

Tout ce que l'amas dévore entre dans un **pool** commun porté par l'amas ; chaque ver tué en rend sa part entière, le dernier rend le reliquat, et rien n'est jamais arrondi vers le haut. Un ver qui n'a rien mangé peut encore lâcher un appât de pêche, des entrailles ou un sac de sang.

### 7.1 Le pool

Le pool est une table fixe sans allocation `prefab → quantité (float)` (§11.3). Il existe sur chaque amas et sur chaque ver libre (vide pour un ver qui n'a rien mordu). Absorption d'un ver → son pool est additionné à celui de l'amas. Fusion d'amas → addition. Dissolution → division à parts égales entre les vers libérés (fractions conservées). Dormance → persisté avec l'amas ou le ver (§11.4). Chaque pool porte aussi un reste fractionnaire par ressource, `carry[r]` (§7.3), additionné, divisé et persisté comme le pool.

### 7.2 Ce qu'une morsure d'équipement ajoute au pool

Pour un objet de qualité `q` ayant perdu `d` points de durabilité sur une durabilité maximale `Dmax(q)` (`ItemData.GetMaxDurability(q)`), `d` étant la différence de `m_durability` avant et après l'application, en `float`, telle que `BiteInventory` la calcule (après blocage) :

```latex
\text{pool}[r] \mathrel{+}= \frac{d}{D_{max}(q)} \times \rho \times C_r(q) \qquad C_r(q) = a_r + \sum_{l=2}^{q} g_r(l) \quad \text{pour chaque ressource } r \text{ de la recette}
```

- `C_r(q)` est le **coût vanilla cumulé** de l'objet : coût de fabrication `a_r` (qualité 1) plus la somme des coûts d'amélioration des niveaux 2 à `q`. Les quantités viennent de `Requirement.GetAmount(level)` vanilla (`g_r(l)` = `GetAmount(l)`), `m_upgraderResource` compris, avec la même sémantique que l'atelier. Cette formule remplace la formule linéaire `a_r + (q−1) × u_r`.
- `Drops.IncludeUpgradeMaterials = true` ajoute le second terme (la somme sur `l`) ; à false, `C_r(q) = a_r`.
- Recette = `ObjectDB.GetRecipe(item)` ; objet sans recette, ou recette sans ressources (objets de marchand, de quête, de drop) → rien.
- `ρ` = `Drops.EquipmentDropRatio` (défaut 1,0) multiplie le tout ; il existe pour le jour où l'exploit « réparer gratis » lasserait.
- Le calcul se fait sur le client de la cible (seul à connaître son inventaire) et le résultat est envoyé à l'amas par RPC `MW_Consumed(ZDOID mass, ZPackage)` (§11.3).

Exemple : une cuirasse rembourrée q1 (10 fer, 20 fil de lin, 1000 de durabilité) qui perd 437 points → +4,37 fer et +8,74 fil de lin dans le pool (en qualité 1, `C_r(1) = a_r` : l'exemple est inchangé).

### 7.3 Ce qui tombe quand un ver meurt

- Le calcul est indépendant du chemin (phase 6). Chaque ressource `r` garde un reste fractionnaire `carry[r]`. Pour `k` vers tués d'un coup sur un amas de `n` (avant les morts) : `share = pool[r] × k / n` ; `pool[r] −= share` ; `carry[r] += share` ; on droppe `floor(carry[r])` objets au point d'impact (`ItemDrop` vanilla, empilés par `m_maxStackSize`) et on les retire de `carry[r]`. Le total droppé est ainsi le `floor` de la part cumulée, quel que soit le découpage des coups (un coup de zone ou ver par ver). Exemple : 40 vers qui ont rongé 4,37 fer ; chaque ver tué en rend 4,37 / 40 = 0,109, donc rien pour les premiers (le `carry` monte), puis 1 fer dès le 10e ver tué. Les gros coups de zone rendent plus régulièrement.
- Mort du dernier ver (`n` → 0) : on droppe `floor(pool[r] + carry[r])` pour chaque entrée ; le reste fractionnaire est perdu (« arrondi à l'intérieur »). Tout est droppé en piles maximales, **sans perte** d'objets, sur plusieurs frames (3 objets par frame) si plus de 60 objets.
- Ver libre tué : `floor` de son propre pool (reste `carry` compris), reste perdu.
- Les drops apparaissent avec la physique vanilla d'`ItemDrop` (petite impulsion aléatoire), et sont limités par `Drops.MaxItemsPerDropEvent` (60) pour éviter une pluie d'items. La limite compte des objets `ItemDrop` déjà empilés (`m_maxStackSize`). Un événement = un appel de répartition (un coup, qui peut tuer `k` vers). Le surplus reste dans le pool pour le ver suivant ; au dernier ver, voir plus haut. Ordre : drops natifs d'abord (§7.5), puis pool, chacun avec sa limite propre.

### 7.4 Créatures et structures dévorées

- **Créature tuée par un amas** (dernier coup `m_lastHit.m_attacker` = l'amas) : la liste de drop vanilla (`CharacterDrop.GenerateDropList`, étoiles et modificateurs de monde compris) est calculée normalement puis **capturée** dans le pool au lieu d'être créée au sol. Un postfix de `GenerateDropList` (méthode d'instance) capture dans le pool les entrées qui sont des objets (prefab avec `ItemDrop`) et les **retire** de la liste retournée : le ragdoll ne stocke plus ces objets, ce qui évite les doublons. Les autres entrées (les `Blob` d'un `BlobElite`, le `BlobMorkMini` d'un `BlobMork` sont des créatures) restent dans la liste vanilla. L'amas n'a pas de `Ragdoll`, même chemin. Les trophées et objets à chance en font partie tels qu'obtenus (§10.3).
- **Créature tuée par un ver seul** : le cobra reste sans attaquant (§11.1). Un **registre « dernier coup de ver »** est écrit au coup de cobra : clé = `ZDOID` de la créature, valeur = la colonie et l'heure, expiration au bout de 2 s. Le coup de cobra porte une marque (`m_skillRaiseAmount` = 0,0625, champ que le jeu ne lit que pour un attaquant joueur). Le postfix de `GenerateDropList` consulte le registre en plus de `m_lastHit.m_attacker`, mais seulement si le coup mortel, le dernier appliqué à la victime, est un coup de cobra sans attaquant : une brûlure, un poison, une chute ou une noyade qui tue après le cobra n'est pas attribuée au ver. Le butin capturé va dans le pool du ver tueur, ou de la colonie si le ver n'existe plus.
- **Selle d'un animal apprivoisé** tué par un amas ou un ver (même attribution) : `Tameable.OnDeath` ne fait pas tomber la selle, elle va dans le pool.
- **Structure détruite par un amas ou un ver** : les ressources sont capturées de la même façon ; les dégâts partiels ne rendent rien.
    - `WearNTear` : ce que `Piece.DropResources` aurait rendu, avec la règle vanilla ÷3 si la pièce n'a pas été posée par un joueur.
    - `Destructible` dvergr (§5.3) : ce que `DropOnDestroyed` aurait rendu.
    - Une pièce qui s'effondre (dégâts structurels sans attaquant) n'est pas capturée : ses drops suivent le vanilla.
- Ces quantités sont entières ; elles suivent ensuite la règle 7.3 comme le reste.

### 7.5 Drops natifs (indépendants du pool)

| Drop | Probabilité par ver tué | Clé | Item vanilla |
| --- | --- | --- | --- |
| Appât de pêche | 8 % | `Drops.BaitChance` | un prefab de la famille `FishingBait*`, tiré selon `Drops.BaitWeights` |
| Entrailles | 8 % | `Drops.EntrailsChance` | `Entrails` |
| Sac de sang | 4 % | `Drops.BloodbagChance` | `Bloodbag` |

Les trois tirages sont indépendants ; aucun trophée, aucun item nouveau, aucune icône à créer. **Pondération des appâts par rareté** : au premier lancement, le mod énumère les items dont le prefab commence par `FishingBait` et écrit dans la config `Drops.BaitWeights` une liste `prefab:poids` avec : appât de base (acheté chez Haldor) = 40, appât des Terres brumeuses = 25, tout autre appât = `max(2, round(10 / (1 + T)))`, arrondi loin de zéro (2,5 donne 3, décision 57), où `T` = nombre d'ingrédients de la recette qui sont des trophées (les appâts fabriqués avec un trophée de boss ou de mini-boss sont les plus rares). L'utilisateur peut ensuite éditer la liste ; un prefab inconnu dans la config est ignoré avec un avertissement dans le log.

### 7.6 Ce que ne font pas les drops

Pas de drop à l'absorption, à la fusion ni à la dissolution ; pas de drop quand un nid est détruit (il libère ses derniers vers, §8) ; pas de drop pour un ver qui despawn par limite (§8.4) — il n'y a pas de despawn.

## 8. Design — nids, reproduction, plafonds et spawn

Au plus 8 salles éloignées les unes des autres reçoivent un petit nid, et les salles éligibles reçoivent quelques vers isolés (70 au plus par mine) ; le nid pond tant qu'il vit, jusqu'à 25 vers vivants par nid et 200 par mine (décision 191 : peu de vers, pour ne pas dégrader les performances), et une mine dont tous les nids sont détruits ne produit plus jamais de ver. Rien de tout cela n'existe hors des mines.

### 8.1 Le nid

| Caractéristique | Valeur | Clé |
| --- | --- | --- |
| Points de vie | 150 | `Nest.Health` |
| Résistances | table §3.2 (le feu nettoie un nid en deux coups de torche) | `Resist.*` |
| Couvée initiale | 6 vers Sleeping à 1–3 m du nid | `Nest.InitialWorms` |
| Vers isolés par mine | budget de 70, Sleeping, répartis sur **toutes** les salles éligibles (celles à nid ou non) en round-robin, les plus grandes d'abord (§8.3), à ≥ 4 m d'un nid ; rattachés au nid le plus proche (même `nestIndex`) pour l'alerte seulement : ils ne comptent pas dans le plafond du nid (décision 193) | `Nest.LoneWormsPerMine` |
| Vers isolés par salle | 2 au plus | `Nest.MaxLoneWormsPerRoom` |
| Ponte | 1 ver Sleeping toutes les 30 s à 1–2 m du nid | `Nest.SpawnInterval` |
| Plafond par nid | 25 vers vivants issus de ce nid (libres ou absorbés dans un amas, §8.2) ; les vers isolés n'en font pas partie | `Nest.MaxAlivePerNest` |
| Pause de ponte | aucune ponte pendant 3 s après un coup (la minuterie de ponte s'arrête, elle ne se remet pas à zéro, décision 196) | `Nest.HitPause` |
| Destruction | libère 5 vers actifs (dans la limite du plafond de la mine, §8.2), aucun drop ; effet et son de `fx_egg_splash`, joués en **copie locale sans `ZNetView`** chez le propriétaire du nid, comme le sang des vers (décision 195) ; le ZDO du nid est retiré par `ZNetScene.Destroy` | `Nest.ReleaseOnDestroy` |
| Taille visuelle | monticule ≈ 0,9 m de large, 0,5 m de haut (boîte de collision 0,9 × 0,5 m), pulsation lente de ±3 % ; maillage `nest_idle` dessiné par le moteur de rendu des vers, aucun rendu vanilla ; nom au survol `$mw_nest` par un `HoverText` ajouté au clone de `SeekerEgg` (`Destructible` n'est pas `Hoverable` ; ni ZDO ni RPC en plus, décision 240) | — |
| Réveil | §5.1 ; rayon de réveil 10 m | `Nest.WakeRadius` |

- La ponte n'a lieu que lorsque la mine est chargée et qu'un joueur est dans la boîte de la mine (côté propriétaire de la colonie) : pas de rattrapage du temps écoulé hors ligne. « Si on laisse faire » = rester ou revenir souvent : une mine de 8 nids explorée 5 minutes gagne 80 vers (1 ver toutes les 30 s par nid), de la population initiale d'environ 120 jusqu'au plafond de 200, sans perte (décision 197).
- Un nid ne pond pas pendant qu'il est attaqué (`Nest.HitPause` = 3 s après le dernier coup, lu dans le ZDO du nid : le pair qui possède la colonie le voit même s'il ne possède pas le nid) ni pendant la dormance de son groupe (§5.1 ; un groupe qui dort ne pond rien, un nid ne pond donc qu'après avoir été réveillé par un joueur). Un ver pondu naît Sleeping et le reste jusqu'au réveil du groupe (une perception dormante ou une alerte le réveille).
- Un nid n'est jamais une cible pour les vers ni pour les créatures vanilla (pas de `Character`, un `Destructible` que l'IA vanilla ne choisit pas comme cible, décision 14 ; il peut subir des dégâts collatéraux, et n'est pas dans la liste des `Destructible` dvergr cibles de §5.3).

### 8.2 Plafonds

| Plafond | Valeur | Clé | Comportement au plafond |
| --- | --- | --- | --- |
| Vers vivants par nid | 25 | `Nest.MaxAlivePerNest` | le nid arrête de pondre (il reprend dès qu'il y a de la place) |
| Vers par amas | 200 | `Mass.MaxCount` | l'excédent forme ou garde un amas séparé (§4.1) |
| Vers vivants par mine (libres + absorbés) | 200 | `Colony.MaxAlivePerMine` | aucune ponte, aucune libération, aucun ver créé sans `force` ; le harnais peut forcer au-delà uniquement en Debug pour les tests de perf |
| Nids par mine | 8 | `Nest.MaxNestsPerMine` | on ne pose pas d'autre nid ; les autres salles n'ont que des vers isolés |

Le compte par nid est tenu via l'origine de chaque ver (`nestIndex`, §11), y compris dans les amas : **chaque amas porte ses `nestCounts`** (ZDO `mw_nests`, un compte par nid plus un compte « sans nid » pour les isolés et les vers d'origine inconnue, écrits par le propriétaire de l'amas), dont la somme vaut toujours son nombre de vers `n` : une absorption ajoute le ver à son nid, une fusion transfère au prorata (plus grand reste, le reliquat à la source), une perte retire au prorata, une dissolution rend à chaque ver libéré un nid selon les comptes (décision 198). Le compte d'un nid est donc ses vers libres (isolés exclus) plus la somme des `nestCounts` des amas de la colonie, fusion et dissolution comprises. Le plafond de la mine compte les vers libres **et** ceux des amas, calculés à l'instant demandé. Un amas de 200 vers est donc possible, mais il absorbe alors toute la population de la mine (décision 191).

### 8.3 Spawn dans les mines infestées

- **Où** : uniquement dans les donjons des mines infestées des Terres brumeuses, identifiés par le thème du générateur (`DungeonGenerator.m_themes` contient `Room.Theme.DvergerTown` et pas `DvergerBoss`, décision 194). Jamais dans la citadelle de la Reine, jamais à l'extérieur, jamais dans un autre biome ou un autre donjon, jamais sur un objet `SW_*` de SkyWhale.
- **Quand** (décision 194) : l'ensemencement est **paresseux**, côté propriétaire de la colonie, la première fois qu'un joueur est dans la boîte de la mine alors que ses `Room` sont chargées (deux sondes à 0,5 s voient le même nombre de salles), qu'elle ait été générée avant ou après l'installation du mod. Pas de hook sur `DungeonGenerator.Generate` : `m_placedRooms` est vide en postfix. Un postfix sur `DungeonGenerator.Load` (appelé par `Awake`, donc aussi à la génération) ne fait que retenir le générateur ; la colonie est créée par la sonde du plugin (`MineSeeder.Pump`, 2 Hz, seulement quand le joueur local est dans un intérieur) par n'importe quel pair dont un joueur est dans la boîte, s'il n'y en a pas déjà une (registre par boîte ; si deux pairs en créent une en même temps, la plus petite `ZDOID` reste et les autres sont détruites par leurs propriétaires, `mw_seeded` hérité) ; si le propriétaire de la colonie n'a aucun joueur dans la boîte (resté dehors, près de l'entrée), le joueur qui est dedans en prend la propriété (décision 199), puis l'ensemencement a lieu **une seule fois** : le drapeau `mw_seeded = 1` du ZDO de la colonie est écrit juste avant la pose des objets. Une colonie de mine ne se détruit jamais d'elle-même (elle porte ce drapeau) ; une colonie de test se détruit vide, sans amas ni nid, après 10 s. Les objets créés sont des ZDO persistants, donc visibles par tous et sauvegardés.
- **Comment** (décisions 192 et 193) : une salle est **éligible** si sa surface au sol (`m_size.x × m_size.z`) ≥ `Nest.MinRoomArea` (25 m²), si elle n'est ni `m_entrance` ni `m_divider`, et si un point de sol valide y est trouvé (12 essais : plus bas niveau plat de la colonne, pente < 20°, tête dégagée, à ≥ 1,5 m des murs et ≥ 2 m des `CreatureSpawner`, rien de solide sur 0,45 m de rayon, dans la boîte de la colonie). On choisit au plus `Nest.MaxNestsPerMine` (8) de ces salles par **échantillonnage du point le plus éloigné** (`NestMath.ChooseNests`) : la première est la plus grande à ≥ 20 m de l'entrée, chacune des suivantes est la plus éloignée des nids déjà choisis (la plus grande l'emporte à égalité à 0,5 m près, puis une empreinte de la graine), à ≥ 20 m de l'entrée et ≥ 6 m des autres nids ; moins de salles éligibles donnent moins de nids. Puis la couvée : `Nest.InitialWorms` (6) vers Sleeping à 1–3 m de chaque nid. Puis les vers isolés : le budget `Nest.LoneWormsPerMine` (70) est réparti par `SeedMath.LoneWormsPerRoom` (round-robin, `Nest.MaxLoneWormsPerRoom` = 2 au plus par salle) sur **toutes** les salles qui ne sont ni l'entrée ni un séparateur et où un point de sol valide existe (couloirs et petites salles compris, quelle que soit leur surface ; point de sol à ≥ 0,6 m des murs au lieu de 1,5 m, 24 essais), rangées de la plus grande à la plus petite, à ≥ 4 m de tout nid et ≥ 1,2 m les uns des autres ; ce qu'une salle ne peut pas accueillir est reporté sur les salles qui ont encore de la place (décision 207) ; une petite mine en reçoit au plus 2 × le nombre de ces salles. Tout est tiré de `System.Random(SeedMath.MineSeed(graine du monde, position de la racine du donjon))` : le même monde donne la même mine ensemencée. Population initiale visée : `6 × nids + min(70, 2 × salles à vers isolés)`, soit environ 120 pour 8 nids. Rien n'est posé hors de la boîte. Le plan est échantillonné par tranches de 3 ms sur plusieurs images ; les objets sont posés en une image.
- Un seul `MW_WormColony` par mine, posé à la racine du donjon, qui porte la simulation de tous les vers de la mine (§10). Chaque nid porte l'identifiant de sa colonie et son index stable (ordre de choix, 0 à 7) dans son ZDO et s'y enregistre dès que les deux existent chez le pair propriétaire de la colonie.
- Les spawns vanilla de la mine (fouineurs, tiques, couvains, soldats) sont **inchangés** : les vers s'ajoutent.

### 8.4 Fin de vie et nettoyage

- Mine propre = tous les nids détruits ; les vers restants vivent jusqu'à leur mort, aucun nouveau n'apparaît. La colonie d'une **mine** est conservée même vide (elle porte `mw_seeded`, la supprimer ferait ré-ensemencer une mine propre, décision 194) ; seule une colonie de test se supprime quand elle est vide (après 10 s, sans amas ni nid).
- Aucun despawn par distance ou par temps ; aucun événement de raid ; aucune migration hors de la mine (les vers sont clampés à la boîte englobante du donjon + 2 m, et un ver qui tombe hors de la boîte est détruit sans drop, et son pool est perdu : cas rare et accepté ; la dormance, elle, conserve le pool tel quel).

## 9. Design — feu, résistances et interactions diverses

Le feu est la contre-mesure voulue : il fait fuir les vers et les brûle deux fois plus, si bien qu'une torche en main suffit à traverser une salle, au prix d'un bouclier qu'on ne tient pas.

### 9.1 Feu

- **Peur** (`Fire.AfraidOfFire = true`) : un ver ou un amas actif fuit toute source de feu. Source de l'amas : une `EffectArea` de type feu (feu de camp, brasero, torches murales allumées) à moins de `Fire.FearRadius` (4 m) du **bord** de l'amas (distance du centre au point le plus proche du collider de l'`EffectArea`, moins `r(n)`), **ou** un `Humanoid`, joueur compris, qui **tient une torche allumée** (objet équipé de type `Torch`, ou l'arme vanilla des torches, identifiée au décompilé) à moins de `Fire.FearRadius` du bord de l'amas. C'est un critère du mod, la vanilla n'ayant pas de critère « torche tenue » (décision 1.1 ligne 38). Vérification toutes les 0,25 s (pas par frame), par la liste statique des `EffectArea` et des joueurs ou `Humanoid` à moins de 15 m. Les vers libres reçoivent le même critère « torche tenue » dans `WormSense.RefreshFire`.
- Un amas englouti qui subit la peur se détache puis fuit pendant `Fire.FearDuration` (3 s) à l'opposé de la source, à `Mass.MoveSpeed`, puis repasse en Hunt ; un amas qui reçoit des dégâts de feu fuit aussi pendant `Fire.FearDuration` même sans source persistante ; un ver, lui, fuit `Worm.FleeDuration` (4 s, §3.3).
- Fuir n'empêche pas de brûler : la zone de feu continue d'infliger ses dégâts vanilla, multipliés par la faiblesse (× 2).
- Le Wisplight n'est pas une source de feu. Les lampes dvergr des mines non plus.

### 9.2 Interactions

| Situation | Comportement |
| --- | --- |
| Furtivité | Un joueur accroupi non détecté passe : vue et ouïe vanilla (§3.1), bruit du joueur pris en compte ; les vers dormants ne voient qu'à 4 m |
| Lumière | Aucun effet (ni le Wisplight ni une torche n'attirent) |
| Apprivoisés | Cibles valides ; un loup englouti prend des dégâts PV perçants, et riposte (vanilla) |
| Autres joueurs | Un joueur peut frapper l'amas collé à un coéquipier ; les dégâts de zone du joueur suivent la règle JcJ vanilla |
| Mort du joueur | Mort vanilla ; la pierre tombale n'est jamais ciblée ; l'amas se détache (§4.9) et choisit une nouvelle cible au tick suivant |
| Déconnexion / changement de propriétaire | La colonie et les amas changent de propriétaire selon les règles ZDO vanilla ; l'état est repris du dernier instantané (§11) |
| Modificateurs de monde | Les coups agrégés passent par `HitData` vanilla : les curseurs de difficulté de combat s'appliquent aux PV ; `Engulf.ScaleWithWorldModifiers` (défaut `true`) les applique aussi aux points de durabilité |
| Sommeil, repos, abri | Aucune interaction spécifique |
| Portails, bateaux, charrettes | Sans objet dans une mine |
| Sauvegarde / chargement | Tout l'état (vers, amas, pools, nids, compteurs) survit à une sauvegarde, un redémarrage de serveur et un changement de propriétaire (§11.4) |
| Mods tiers (CLLC, EpicLoot…) | Non supportés en v1 ; le mod ne doit pas planter s'ils sont présents : l'amas est créé au niveau 1 à chaque spawn, les drops de pool sont des `ItemDrop` vanilla |
| Console `killall` / `removedrops` | `killall` tue les amas (créatures) ; la commande Debug `mw_kill` tue aussi les vers virtuels et les nids |

### 9.3 Garde-fous

- Un ver ou un amas ne quitte jamais la boîte du donjon (§8.4).
- Un amas dont la cible devient invalide (téléportée, déconnectée, morte) se détache au tick suivant (§4.9 : Infesté retiré, état Hunt) et choisit une nouvelle cible.
- Toute erreur dans la simulation d'une colonie est journalisée et désactive cette colonie (vers figés, aucun drop) plutôt que de faire planter le client ; le harnais traite tout `Exception` du mod comme un échec de scénario.
- Journalisation (décision 238) : une exception attrapée dans un patch, une RPC, un `Update` ou un `Tick` est écrite **une seule fois par contexte et par type** (`Plugin.LogException`) ; les répétitions sont comptées en silence. Les logs par événement (formation, fusion, dissolution, cibles, drops, pools) ne sont émis que par les builds `MW_DEV` ; avertissements et erreurs restent en Release.
- Le gel de dev (`WritheMassState.DevFrozen`, `WormColony.Frozen`) n'existe que sous `MW_DEV` ; la fusion a son propre drapeau interne (décision 239).

## 10. Architecture technique

Le principe qui porte toute l'architecture : **un ver n'est pas une créature**. Une mine entière tient dans un seul objet réseau, la colonie, qui simule ses 400 vers en tableaux ; seuls les amas sont de vraies créatures vanilla, et seuls les nids sont de vrais destructibles. Les vers ne possèdent qu'un collider de proxy, sans `ZNetView` ni `Character`, pour être frappés.

&#91;embedded content: architecture · 1 colonie par mine, amas = créatures, vers = tableaux + proxies\]

Le pair propriétaire simule la colonie et ses amas ; chaque morsure voyage comme une `HitData` vanilla jusqu'au client de la cible, qui renvoie ce qui a été dévoré ; tous les clients ne font que dessiner, jouer les sons et tenir les colliders des vers.

### 10.1 Entités et composants

| Entité | Prefab / objet | Composants | Rôle |
| --- | --- | --- | --- |
| Colonie | `MW_WormColony` (un par mine, à la racine du donjon, ZDO persistant) | `ZNetView`, `WormColony` | Tableaux SoA des vers virtuels ; simulation côté propriétaire ; registre des nids ; état de réveil par groupe ; plafonds ; instantanés et RPC ; fournit les instances à rendre |
| Ver virtuel | aucun GameObject | entrée `i` des tableaux `pos[]`, `heading[]`, `state[]`, `timer[]`, `hp[]`, `nestIndex[]`, `target[]`, `poolIndex[]` | Toute la logique de §3 ; capacité fixe 400 + marge Debug (réservée au démarrage, aucune allocation en jeu) |
| Proxy de ver | GameObject poolé (pool de 420), sans `ZNetView` | `SphereCollider` non trigger r = 0,18 m sur la couche `hitbox` (décisions 47, 49), `WormProxy : IDestructible` | Reçoit `Damage(HitData)` → route vers la colonie (local si propriétaire, sinon RPC) ; attaché aux vers rendus à moins de 40 m d'un joueur, suit leur position ; les sphères de coup de l'amas (dôme et coulée, §4.5, §4.7) suivent le même principe de routage |
| Amas | `MW_WritheMass` cloné de `SeekerBrood` (repli `Tick`, décision 18) via Jötunn `PrefabManager.CreateClonedPrefab` + `AddPrefab` dans `OnVanillaPrefabsAvailable`, sans `CreatureManager` (décision 101) | `ZNetView`, `ZSyncTransform`, `Character`, `Humanoid` (composant `Character` hérité du clone, armes vidées), `WritheMassAI : BaseAI`, `WritheMassState`, `WritheMassVisual`, `Rigidbody`, `CapsuleCollider` (r ≤ 0,5, `Mass.MaxColliderRadius`, appliqué à l'instance par `WritheMassState`, pas dans le prefab), `Animator` + `ZSyncAnimation` vanilla conservés mais renderers désactivés (évite les `NullReferenceException` de `Character`) | Créature vanilla à part entière : visée par les IA, barre de PV, mort, messages ; logique de §4–§6 ; zone de coup = sphères sur la couche `hitbox` routées vers l'amas (§4.5) |
| Nid | `MW_WormNest` cloné de `SeekerEgg` (décision 195) via Jötunn `PrefabManager.CreateClonedPrefab` + `AddPrefab` dans `OnVanillaPrefabsAvailable` | `ZNetView` (persistant), `Destructible` (150 PV, résistances §3.2, aucun effet vanilla), `BoxCollider` 0,9 × 0,5 m, `WormNest` ; `EggHatch`, `DropOnDestroyed`, `LODGroup`, enfants et rendus vanilla retirés | Index et colonie dans le ZDO, instant du dernier coup, minuterie de ponte ; se dessine lui-même (`IWormRenderSource`, maillage `nest_idle`) ; la ponte, le compte par nid et la libération sont conduits par la colonie |
| Effet Infesté | `MW_SE_Infested` (Jötunn `CustomStatusEffect`) | `StatusEffect` avec `ModifySpeed` | §6.2 |
| Rendu | `WormRenderer` (singleton client) ; `GpuWormHost` et `GpuMassBridge` (colonie GPU des amas) | `Graphics.RenderMeshInstanced` / repli (§12) ; compute shader et rendu indirect (§12.1) | `WormRenderer` dessine les vers libres et les amas du repli CPU ; la colonie GPU dessine les amas à moins de 85 m, toutes colonies confondues |
| Assets | `AssetCatalog`, `GltfLoader`, `TextureLoader`, `GpuWormBundle` | — | Chargement des `.glb` et `.png` du dossier du plugin au démarrage (§13), et du bundle de shaders (§13.5) |
| Patches | `DamagePatches`, `DropPatches`, `SpawnPatches`, `HudPatches`, `AiPatches`, `SavePatches` | HarmonyX | §10.3 |
| Dev | `QuickStart`, `DevBridge`, `Scenarios`, `PerfProbe` (`#if MW_DEV`) | — | §15 |

### 10.2 Qui décide quoi

- **Propriétaire de la colonie** (pair qui possède le ZDO, règle vanilla du plus proche) : simule les vers, crée et détruit les amas et les nids, applique les morts de vers, gère les pools des vers libres, émet les instantanés (`mw_snap`) et les deltas (`MW_Delta`, §11.3) aux joueurs distants proches.
- **Propriétaire d'un amas** (son ZDO) : IA, morsures (émet les `HitData`), compteur `n`, pool de l'amas, fusion. En pratique le même pair que la colonie la plupart du temps ; le code ne le suppose jamais (toute écriture passe par « si propriétaire, écrire, sinon RPC au propriétaire »).
- **Propriétaire de la cible** (le client du joueur mordu) : blocage, choix des pièces, durabilité, calcul de ce qui est entré dans le pool → `MW_Consumed` vers l'amas.
- **Tous les clients** : rendu, sons, effets, interpolation, `Physics.IgnoreCollision` entre amas et cible engloutie.
- **Pair qui n'est pas propriétaire de la colonie** : ne simule rien ; il montre les vers libres d'après `MW_Delta` (le miroir, §11.3), pose des proxies de coup dessus et envoie ses coups au propriétaire (`MW_WormHit`, par identifiant réseau).
- **Serveur dédié** : n'est propriétaire de rien de tout cela (comme en vanilla pour les créatures) : il ne crée, n'ensemence, ne revendique ni ne simule aucune colonie (garde `ZNet.IsDedicated()` : `ColonyRegistry.ServerMayHost`, lue par `WormColony.Create`, `MineSeeder.Pump` et `WormColony.Update` ; seul le mode de dev `serverown` (`MW_DEV`, passe 8-2) la lève) et ne fait que router les RPC ; il impose la config (§14) et refuse les clients sans le mod (Jötunn `NetworkCompatibility`, `CompatibilityLevel.EveryoneMustHaveMod`, `VersionStrictness.Minor`).

### 10.3 Patches Harmony

| Cible | Type | Rôle |
| --- | --- | --- |
| `DungeonGenerator.Load` | postfix | Retient le générateur d'une mine infestée pour la sonde de l'ensemencement (§8.3, décision 194) ; inerte sur tout autre donjon et sur les objets `SW_*` ; `Generate` n'est pas patché (`Load` est appelé par `Awake`, avant `Generate`) |
| `HitData.ApplyArmor` | prefix (skip) | L'armure n'atténue jamais une morsure, sauf si `Engulf.ArmorReducesBites` (§6.4) |
| `Player.DamageArmorDurability` | prefix (skip) | Pas d'usure d'armure sur une morsure tant que `Engulf.ArmorReducesBites` est faux (décision 54) |
| `Humanoid.BlockAttack` | postfix | Remonte « parade parfaite » au contexte de morsure : tick annulé, suivant à 1,0 s (§4.8, §6.4) |
| RPC `MW_MassBite(ZDOID mass, float P, float hp, Vector3 point)` | du propriétaire de l'amas vers celui de la cible | Ouvre le `WormBiteContext` puis `target.Damage(hit)` en local, comme le cobra (§6.1) |
| RPC `MW_Infest(bool)` | du propriétaire de l'amas vers celui de la cible | Applique ou retire `MW_SE_Infested` (§6.2) |
| `Character.ApplyDamage` | prefix | Si l'attaquant est un amas ou un ver et la cible un joueur avec des objets éligibles : convertir les dégâts restants en points de durabilité, afficher le texte « Équipement −P », notifier l'amas, et ne pas appliquer aux PV |
| `Character.Stagger`, `Character.AddStaggerDamage` | prefix (skip) pour `MW_WritheMass` | Jamais d'étourdissement (§4.8) |
| `HitData.m_pushForce` (point exact choisi au dump) | prefix | Recul de l'amas × `clamp(10/n, KnockbackMinFactor, 1)` (§4.8) |
| `Character.GetHoverName` | postfix pour `MW_WritheMass` | Nom avec compteur (§4.3) |
| `CharacterDrop.GenerateDropList` (instance) | postfix | Si `m_lastHit.m_attacker` est un amas, ou si le registre « dernier coup de ver » désigne la créature (§7.4) : capture la liste dans le pool et **vide** la liste retournée (le ragdoll stocke une liste vide) ; l'effet de mort de l'amas n'a pas de `Ragdoll` |
| `Tameable.OnDeath` | prefix | Si la mort nous est attribuée (comme `GenerateDropList`), la selle va dans le pool au lieu d'être créée au sol ; inerte sinon |
| Registre « dernier coup de ver » (pas un patch) | écrit au coup de cobra | `ZDOID` de la créature → colonie + heure, expire en 2 s (§7.4, §11.1) |
| `WearNTear.Damage` + `WearNTear.Destroy` + `Piece.DropResources` | prefix/postfix | Mémoriser le dernier attaquant par pièce ; capturer les ressources d'une pièce détruite par l'amas ou un ver, règle vanilla ÷3 comprise ; une pièce qui s'effondre sans attaquant n'est pas capturée (§7.4) |
| `Destructible` dvergr (`DropOnDestroyed`) | prefix/postfix | Capturer les drops d'un `Destructible` dvergr (préfixes `dvergrtown_`, `dvergrprops_`) détruit par l'amas ou un ver ; inerte sur tout autre `Destructible`, nids compris (§5.3, §7.4) |
| `BaseAI.IsEnemy` | aucun pour l'amas (faction `PlayerSpawned`, §5.4) | Le harnais imprime la matrice réelle ; S19 la compare à la matrice attendue de §5.4 |
| `ZNet.WorldSaveStarted` | abonnement à l'événement statique (pas un patch Harmony ; appelé avant `PrepareSave`) | Forcer un instantané des colonies avant la sauvegarde (§11.4) |
| `Game.Start` / `FejdStartup` | postfix (`#if MW_DEV`) | QuickStart (§15) |

Aucun transpiler : des préfixes et postfixes uniquement, chacun protégé par un `try/catch` qui journalise et laisse passer le comportement vanilla en cas d'erreur. Chaque patch a un test dans le harnais qui vérifie qu'il s'applique (Harmony `GetPatchedMethods`) et qu'il produit l'effet attendu.

### 10.4 Compatibilité avec SkyWhale

L'utilisateur demande que MistWorms et SkyWhale tournent ensemble sur le même serveur. Deux règles suffisent, aucune `Priority` ni `Before`/`After` Harmony n'est nécessaire :

1. un patch ne modifie le résultat que si une créature ou un objet du mod est en jeu ;
2. un prefix ne renvoie jamais `false` sur un objet qui n'est pas au mod.

- **Patchs communs** : `Character.ApplyDamage` et `BaseAI.IsEnemy`. SkyWhale patche la surcharge statique `BaseAI.IsEnemy(Character a, Character b)` : `false` si `a` est la baleine, ou si `b` est la baleine et `a` n'est pas un `Player`. Nos vers ne sont pas des `Player` ; l'exclusion par prefab (ci-dessous) tient. Ce postfix s'ajoute à la matrice attendue de §5.4 (vérifiée par S19) : contre la baleine, `IsEnemy` vaut false dans les deux sens pour l'amas, et `Targets.ExcludedPrefabs` couvre la baleine.
- **Noms** : préfixes de prefab `MW_` pour nous, `SW_` pour SkyWhale ; clés ZDO `mw_*` pour nous, `sw_*` pour SkyWhale.
- **Couches** : la coque, les nageoires et la capsule racine de la baleine sont sur `hitbox` (18), partagée avec nos proxys ; son pont est sur `vehicle` (28), ses triggers sur `character_trigger`. Nos requêtes physiques n'utilisent pas `hitbox` (`SolidMask` et `ViewBlockMask` sont les masques vanilla de `BaseAI`) ; toute requête future sur `hitbox` doit filtrer sur `WormProxy`. Le pont `vehicle` est dans `SolidMask` et `ViewBlockMask`, comme un bateau pour les créatures vanilla : accepté. De son côté, SkyWhale filtre aussi sur ses propres composants ses requêtes sur `hitbox`.
- **Exclusion de cible** : `Targets.ExcludedPrefabs` (défaut `SW_DivineWhale`, §14) ; ces prefabs ne sont jamais ciblés par les vers ni par les amas (§5.3). Phase 6, plus la sélection de cibles de l'amas en 4B.
- **Crash de la baleine** : il passe par le `Damage()` vanilla et touche donc nos proxys normalement.
- **Copies de dev** : `C:\ValheimDev\` et le serveur local sont partagés entre les deux projets ; on les prend chacun son tour.

## 11. Simulation, réseau et persistance

Budget sur le pair propriétaire, mine entière à 400 vers actifs : **≤ 1,0 ms par frame** de simulation en moyenne, 3 ms au pire, zéro allocation GC en régime stable ; sur les autres clients ≤ 0,3 ms ; ≤ 6 Ko/s de réseau par colonie active, ≈ 0 en dormance. Ces chiffres sont mesurés par le harnais (scénario S11, §15) et font partie des critères d'acceptation.

### 11.1 Simulation des vers (propriétaire)

- Tableaux SoA préalloués (capacité `Colony.MaxAlivePerMine` + 50 en Debug) ; liste libre des slots ; aucun `new` dans la boucle, aucune LINQ, aucune `string` ; `Physics.RaycastNonAlloc` / `OverlapSphereNonAlloc` avec buffers statiques.
- **Cadence par niveau de détail** (`Performance.SimRateNear` 20 Hz à moins de 30 m d'un joueur, `Performance.SimRateFar` 5 Hz au-delà, 2 Hz pour les dormants, qui à ce pas guettent un joueur : portée testée avant tout raycast, §5.1) ; le coût est lissé par une **phase propre** à chaque ver (son index modulo 20, fois l'intervalle proche divisé par 20, ajoutée à son premier pas à la création, à la restauration et au réveil) et par un **plafond de pas par frame** : `MaxStepsPerFrame` = 160 pas et 2 ms de boucle (au moins 8 pas) ; les vers dus et non traités sont reportés à la frame suivante, la boucle repartant du premier d'entre eux (aucun ver n'est affamé, décision 227). Ce sont les cadences du **propriétaire** de la colonie qui s'appliquent, quelle que soit la configuration locale des autres pairs.
- **Déplacement** : direction désirée = somme pondérée de (objectif, évitement d'obstacles par 3 raycasts horizontaux avant/±35° réévalués tous les 0,25 s, séparation, cohésion et alignement avec les voisins (grille spatiale 2 m, détail ci-dessous), places d'approche et file indienne (§3.3), bruit d'errance) ; projection au sol par raycast vers le bas tous les 0,1–0,25 s ; pas de `Rigidbody`, pas de `CharacterController`.
- **Séparation, cohésion, alignement** (constantes nommées, pas des clés de config) : la séparation prend pour point de référence le milieu du corps (0,15 m derrière la tête), rayon 0,3 m, 6 voisins, poids 1,0 (cible : plus de chevauchement visible, S18) ; cohésion légère en errance, poids 0,2 vers le centre des voisins à moins de 3 m ; alignement en Gathering et en Hunting, poids 0,3 vers le cap moyen des voisins de même état.
- **Pathfinding** : pas de chemin par défaut (les salles sont ouvertes). Un ver qui n'a pas progressé de 0,5 m en 3 s demande un chemin vanilla (`Pathfinding.instance.GetPath`, agent le plus petit) ; budget global 8 requêtes/s par colonie, cache 10 s, suivi par points de passage. Les amas utilisent le pathfinding vanilla de `MonsterAI` en permanence.
- **Perception** : un test de visibilité par ver et par 0,5 s au plus (raycast vers le joueur le plus proche), cible choisie selon §5.3 ; les joueurs sont lus une fois par tick depuis `Player.GetAllPlayers()`, les créatures depuis `Character.GetAllCharacters()` filtrées par distance via la grille.
- **Formation/absorption** : la grille spatiale sert aussi à trouver les groupes (§4.1) ; une passe toutes les 0,25 s.
- **Coup de cobra** : sphère de 0,5 m devant la tête, cible = `Character`, `WearNTear` ou `Destructible` dvergr le plus proche ; émet une `HitData` vanilla **sans attaquant** (`m_attacker` vide, décision 20 : `Character.RPC_Damage` jette un coup dont l'attaquant est un ZDOID sans `Character`), avec un marqueur « ver » porté par `m_hitType` ou un champ réservé. L'attribution du butin passe par le registre « dernier coup de ver » (§7.4), qui remplace l'identification par ZDOID.

### 11.2 IA de l'amas

`WritheMassAI : BaseAI`, `UpdateAI` surchargé : (0) à 1 Hz, bit de son groupe (§5.1) : à 0, l'amas passe Dormant (ou se dissout sur place s'il n'a pas de groupe et que son propre délai de calme est écoulé) ; (1) si dormant → marche vers le nid de son groupe et se dissout en vers Sleeping (§5.2), sans rien cibler ; (2) peur du feu selon le critère du mod (§9.1) ; (3) cible selon §5.3 (réutilise `FindEnemy`, `FindClosestStaticPriority`-équivalents et `CanSeeTarget`) ; (4) si cible à portée d'engloutissement → état Engulf (§4.9, §4.10), sinon `MoveTo(cible)` via pathfinding ; (5) en Engulf, marche vers la cible à `Mass.MoveSpeed` (arrêt à 0 m, sans téléport ; le retard de 0,15 s ne concerne que l'adhérence visuelle), tick de morsure (§6.1), conditions de détachement (§4.9) ; (6) hors cible, errance / retour au nid (§4.6) ; (7) sondage de largeur pour la coulée (§4.7). Jamais `DoAttack`, jamais d'`Attack` vanilla, jamais d'animation vanilla.

### 11.3 Réseau

| Canal | Contenu | Cadence |
| --- | --- | --- |
| ZDO colonie `mw_key` (long), `mw_created` (long) ; ZDO nid et amas `mw_ckey` (long) | **identité stable de la colonie** (décision 211). La clé d'une mine est un hachage (FNV-1a 64 bits, bit de signe à 0, jamais 0) de la position de la racine de son donjon quantifiée au demi-mètre (`MineKey.FromPosition`) : identique à chaque session, sur chaque pair, et pour deux colonies créées en même temps pour la même mine. Une colonie de test (hors donjon) reçoit une clé aléatoire. `mw_created` : temps réseau de la création (0 = inconnu). Les nids et les amas portent la clé de leur colonie dans `mw_ckey`, **jamais un ZDOID** : Valheim renumérote les ZDO qu'il charge (`ZDO.Load` : `m_uid.SetID(++ZDOID.m_loadID)`, et seules ses propres « connections » Portal / Spawned / SyncTransform sont remappées, par comparaison de hachages, un seul lien par objet), donc un ZDOID écrit dans un ZDO est faux, voire celui d'un autre objet, après le redémarrage suivant | à la création |
| ZDO colonie `mw_awake` (long) | un bit par groupe de nid (1 = éveillé), écrit par le propriétaire de la colonie quand un groupe s'éveille ou se rendort ; lu par le propriétaire de chaque amas (§5.1) | au changement |
| ZDO nid `mw_nest_pl` (long) et amas `mw_phit` (long) | temps réseau (ticks) du dernier coup infligé par un joueur, écrit par le propriétaire du nid ou de l'amas, lu par celui de la colonie qui alerte le groupe (§5.1) | à chaque coup d'un joueur |
| ZDO colonie `mw_snap` (byte\[\]) | instantané des vers libres, en little-endian (décision 209) : un octet de version (**2** : la version 1, PV en int8 × 10 bornés à 127, n'est plus lue et suit la règle de version inconnue) et le nombre de vers (`ushort`) ; par ver 11 octets : `pos` (3 × int16, au cm, relatif au **centre de la boîte** de la colonie, donc ± 327 m), `heading` (int8, 256 pas par tour), `state` (int8 : bit 7 = `lone`, bits 0 à 6 = `WormState`), `hp` (`ushort` = PV × 100 arrondis, 0 à 655,35 : couvre le maximum de `Worm.Health`, 200), `nestIndex` (int8, -1 à 63) ; puis les pools des vers libres (`ushort` nombre, puis par entrée l'indice du ver dans l'ordre de l'instantané (`ushort`) et son pool au format de `mw_pool`) ; puis la réserve de la colonie (un pool au même format). Pas de nids dedans (ils sont des ZDO) ni de groupes (`mw_awake`) ; le minuteur de ponte des nids n'est pas conservé (il repart de 0) | toutes les 5 s (`Network.SnapshotInterval`) si le contenu a changé, à `ZNet.WorldSaveStarted`, à la destruction de l'objet colonie (déchargement de zone), et **juste avant une passation planifiée** (préfixe de `ZDO.SetOwner`, décision 216) |
| RPC colonie `MW_Delta(ZPackage)` (routé, `ZNetView.InvokeRPC(peerUid, …)`, du propriétaire vers **chaque joueur distant** à moins de `Network.SyncRadius` (120 m) de la boîte de la colonie ; le serveur dédié ne fait que router) | format `DeltaFormat` (décision 213), little-endian : en-tête de 7 octets (version = 1, flags : bit 0 `reset` = premier paquet d'une keyframe, le pair oublie tout, bit 1 `end` = dernier paquet, le pair retire ce que la keyframe n'a pas cité ; `seq` `ushort` ; nombre d'entrées `ushort` ; nombre d'événements `byte`), puis des **entrées de 11 octets** : `netId` (`ushort`), position (3 × int16 au cm, relative au centre de la boîte), cap (int8, 256 pas), vitesse (uint8, pas de 0,05 m/s), état visuel (uint8 : bits 0 à 3 = `WormState`, bit 6 = `lone`, bit 7 = **retiré** : mort, absorbé ou sorti), puis des **événements de 15 octets** : type (coup, mort, nid détruit), position (3 × int16), uid du pair qui l'a déjà joué (`long`) ; au plus 1 200 octets par paquet, 10 événements par seconde et par colonie (§12.4) ; un pair qui reçoit un événement dont il est l'auteur l'ignore ; un paquet de version inconnue, tronqué ou trop long est refusé | à `Network.SyncRate` (5 Hz) ; **rien** s'il n'y a pas de destinataire (solo, joueur seul, dormance : coût nul) ; un ver n'est envoyé que s'il change d'état visuel, s'écarte de plus de 0,25 m de la prédiction du pair (dernière position envoyée + cap × vitesse × temps), tourne de plus de 20°, ou, éveillé, n'a pas été envoyé depuis 2 s ; les dormants ne sont envoyés qu'à leur changement d'état ; un ver à plus de 30 m de **tous** les destinataires n'est examiné qu'à 2 Hz ; keyframe complète (`reset`, en paquets de 1 200 octets au plus) à un destinataire qui entre dans l'ensemble et à tous quand le propriétaire change |
| RPC colonie `MW_WormHit(netId, ZPackage HitData)` (`ZNetView.InvokeRPC` vers le propriétaire) | coup reçu par un proxy du miroir sur un client non propriétaire ; le ver est désigné par son **identifiant réseau** (un slot est privé à chaque pair) ; le propriétaire retrouve le slot par la table `netId → slot` (tableau indexé, sans allocation) et ignore un id qui n'est plus vivant | à l'événement |
| ZDO amas | `mw_n` (int), `mw_partial` (float), `mw_pool` (byte\[\] versionné, ci-dessous), `mw_nests` (byte\[\]), `mw_state` (byte : Idle/Hunt/Engulf/Dormant, §4.10) + un bit `Stream` séparé (forme, pas état), `mw_ckey` (clé de la colonie), `mw_target` (ZDOID de la cible, valable seulement dans la session qui l'a écrit : un amas instancié depuis un objet que la session n'a pas créé l'oublie à son `Start`) + `ZSyncTransform` vanilla | à la modification (ZDO vanilla) |
| RPC amas `MW_Consumed(ZDOID mass, ZPackage)` | du propriétaire de la cible vers le propriétaire de l'amas : ce qui est entré dans le pool, un `int` prefabHash et un `float` quantité par entrée | à chaque tick de morsure ayant mangé quelque chose |
| RPC amas `MW_MassAdd(k, pool)` / colonie `MW_Dissolve(mass, n, position, pool, sleep)` (noms `MW_*` du code, décision 218) | transferts ver ↔ amas ; le pool voyage dans un `ZPackage` (`ZRoutedRpc` ne sait pas sérialiser `byte[]`) | à l'événement |

- **Validation de `MW_Consumed`** côté amas : prefab connu dans l'`ObjectDB`, quantité entre 0 et 50 par entrée et par tick, au plus 16 entrées. Le reste est ignoré, avec un log de dev.
- **Stockage du pool** : tables fixes sans allocation. Un amas : au plus 16 ressources (`int hash[16]`, `float qty[16]`, `float carry[16]`). Les vers libres : une table de pools partagée par colonie, slots préalloués, `poolIndex` dans `WormStore` (-1 tant que le ver n'a rien mangé).
- **Format de `mw_pool`** (`byte[]` dans la ZDO, permis : seul le RPC est limité) : un octet de version, le nombre d'entrées, puis (hash, qty, carry) par entrée.
- **Cible non joueur** : `WormStore` reçoit `targetZdoUser` (`long`) et `targetZdoId` (`uint`) ; `TargetKind` vaut `Player`, `Tamed`, `Creature` ou `Structure`.
- **Identité** (décision 214) : chaque ver du `WormStore` porte un `netId` (`ushort`, jamais 0), attribué par le propriétaire de façon monotone avec bouclage (en sautant 0 et les ids vivants) ; la table `netId → slot` est un tableau indexé de 65 536 entrées (`NetIdTable`), sans allocation. Il n'est pas persisté dans `mw_snap` : un nouveau propriétaire renumérote et envoie une keyframe à tous.
- **Miroir** (décisions 213 et 214) : sur un pair qui n'est pas propriétaire, `WormMirror` remplit et déplace les slots du **même `WormStore`** que celui du propriétaire (`CollectInstances`, `WormAudio`, `ProxyManager` le lisent donc sans second chemin : une seule source, et le rendu GPU des vers libres, qui pose chaque corde d'après la matrice et l'image de `CollectInstances`, suit les têtes du miroir sans historique). Les vers continuent à cap et vitesse constants entre deux deltas (dead reckoning) puis sont recalés par interpolation sur 0,2 s ; un écart de plus de 3 m est un saut ; au sol, les vers à moins de 40 m du joueur local sont posés sur le sol par un raycast **à la réception d'une entrée** (jamais par image) qui donne aussi la pente suivie par le dead reckoning ; un ver éveillé sans entrée depuis 3 s est retiré (un dormant reste : il n'est envoyé qu'à son changement) ; à plus de `Network.SyncRadius` + 30 m de la boîte le miroir est vidé. Les animations (phase du flipbook) sont locales et jamais synchronisées. Seul le propriétaire de la ZDO est écouté (l'émetteur du RPC doit être `GetOwner()`).
- **Événements** (spec §12.4) : le coup, la mort d'un ver et la destruction d'un nid sont envoyés avec l'uid du pair qui les a déjà joués (le frappeur, le propriétaire, le propriétaire du nid) : il les ignore, les autres pairs proches les jouent. Les décisions 53, 62 et 195 (« les autres pairs ne voient pas l'effet ») sont ainsi levées.
- Un pair qui devient propriétaire repart de `mw_snap` (jusqu'à `Network.SnapshotInterval` de retard lors d'une déconnexion brutale ou d'une passation décidée par le serveur pour un propriétaire distant, voir décision 216 : un ver absorbé entre-temps peut être compté deux fois, un ver né manquer) ; avant cela il vide le miroir sans effet ; l'ancien propriétaire vide son magasin sans effet puis reçoit la keyframe du nouveau ; une **passation planifiée** (la libération vanilla de `ZDOMan.ReleaseNearbyZDOS` quand le propriétaire est le serveur ou l'hôte) écrit `mw_snap` tout de suite, avant la bascule ; les deltas sont idempotents (état absolu, pas de différentiel).
- Taille : une entrée de 11 octets ; 400 vers actifs envoyés à chaque tick = 22 Ko/s au pire théorique, par destinataire ; en pratique seuls les vers que le pair ne peut pas deviner sont envoyés (filtre ci-dessus), objectif ≤ 6 Ko/s ; le harnais mesure (S11, S17). Mesure : la colonie compte les octets envoyés (taille du paquet × nombre de destinataires, moyenne glissante sur 10 s) ou reçus, et, sur un non-propriétaire, le temps CPU du miroir par image ; exposés par `GET /perf` et `GET /status` du pont de dev (`net`). Repli si la mesure dépasse 6 Ko/s : §18 (quantification à 1 octet par axe au loin, pas encore faite).

### 11.4 Persistance

- Tout vit dans des ZDO persistants : colonie (`mw_snap`, `mw_awake`, `mw_seeded`), amas, nids. Une sauvegarde serveur, un déchargement de zone ou un redémarrage restitue le même nombre de vers libres (± 1), leurs PV (au centième), leurs pools et la réserve, les mêmes amas (n, pool, origines), les mêmes nids (vivants, PV ; le minuteur de ponte repart de 0).
- **Écriture** (propriétaire de la colonie seulement, jamais un non-propriétaire) : toutes les `Network.SnapshotInterval` si le contenu a changé (comparaison octet à octet avec le dernier tableau écrit, dans un tampon réutilisé ; une allocation exacte par écriture, car `ZDO.Set(int, byte[])` garde la référence et ne marque rien de changé si la même référence revient), sur l'événement `ZNet.WorldSaveStarted` (avant `PrepareSave`), dans un préfixe de `ZDO.SetOwner` quand le propriétaire de la colonie va la céder (passation planifiée, décision 216 ; inerte pour tout autre ZDO), et dans `OnDestroy` de la colonie (le déchargement d'une zone remet le `ZNetView` à zéro avant la destruction, la colonie écrit donc sur le ZDO qu'elle garde ; le ZDO persistant survit).
- **Lecture** : au premier tick du propriétaire d'une colonie dont le magasin est vide et dont `mw_snap` existe, avant tout le reste (ni ponte, ni ensemencement, ni simulation : `SnapshotSettled`) ; la restauration attend un sol sous le premier ver (les salles d'une mine rechargée peuvent n'être pas encore là), 20 s au plus. Les vers sont recréés sans plafond (`force`) : `Sleeping` reste `Sleeping`, `Calming` devient `Sleeping` sur place, tout autre état actif devient `Idle` (`home` et `den` = la position lue), `Absorbed` et `Dead` ne sont pas écrits. Un ver hors de la boîte (§8.4) est détruit sans butin, son pool va à la réserve. Une version inconnue ou un tableau tronqué laisse la colonie vide (avertissement) ; la mine n'est ré-ensemencée que si `mw_seeded` est absent.
- **Identité et rechargement du monde** (décision 211). Aucun ZDOID n'est écrit dans un ZDO comme référence durable : à chaque chargement du monde Valheim donne de nouveaux identifiants aux ZDO (le `UserID` des ZDO chargés vaut `UnknownFormerUser`, l'`ID` est un compteur), donc la colonie, ses nids et ses amas se retrouvent par la **clé de la mine** (`WormColony.Key`, `ColonyRegistry.FindByKey`). Un nid ou un amas sans `mw_ckey` mais avec l'ancien `mw_colony` (ZDOID, écrit par les versions d'avant) est rattaché à la colonie de mine dont la **boîte** contient sa position (une colonie par mine), et son propriétaire écrit alors `mw_ckey` ; une colonie sans `mw_key` reçoit la clé de la position de son ZDO (la colonie est créée à la racine du donjon). Les connexions de Valheim (`ZDO.SetConnection`) ne conviennent pas : un seul lien par objet, trois types fixes.
- **Doublon de colonie pour une même mine** (deux pairs l'ont créée en même temps, ou un monde sauvé par une version d'avant) : celle qui reste est choisie sur ce que contiennent les ZDO (`MineOwnership.CompareKeeper`) : la colonie ensemencée d'abord, puis celle qui a le plus de vers dans son `mw_snap`, puis la plus ancienne (`mw_created`, 0 = inconnu = la plus jeune), et le plus petit ZDOID seulement pour départager une égalité parfaite. Chaque doublon que le pair possède remet son `mw_snap` vivant (vers, pools, réserve) à la colonie qui reste (`HandOverTo` : appel direct si ce pair la possède, sinon RPC `MW_SnapAbsorb`) avant d'être détruit ; ses nids et ses amas portent la même clé et la suivent seuls. Un nid dont l'indice est déjà pris par un autre nid de la même mine (second ensemencement) est supprimé par son propriétaire, le plus grand ZDOID perdant : le plafond de nids par mine tient.
- **Sauvegarde à la fermeture** : fermer le jeu écrit une dernière sauvegarde qui remplace celle qu'on vient de faire ; ce que les vers font entre-temps (amas, coups) y est. S15 `step=save` fige donc les colonies (`mw_freeze on`) après la sauvegarde.
- Désinstallation du mod : les ZDO orphelins (`MW_*`) sont supprimés par le serveur (prefab inconnu → le ZDO est détruit, comportement vanilla acceptable) ; aucune corruption.
- Changement de version du format `mw_snap` : un octet de version en tête ; une version inconnue → colonie vide avec avertissement dans le log (ensemencée de nouveau seulement si `mw_seeded` est absent).

### 11.5 Thread de travail du rendu de l'amas

`MassWorker` est l'unique thread de fond du **chemin de repli CPU** : il évalue et compose les amas qui n'ont pas de jumeau GPU (mise en page analytique, primitives d'environnement, LOD, §12.1). Les amas rendus par la colonie GPU ne passent pas par lui : leur simulation tourne sur la carte graphique (décision 181). Il travaille sur un double tampon et range les instances dans des seaux (lod, anim, image) ; le thread principal ne fait que des copies en bloc (`Sink.AddWormsBulk`) vers `WormRenderer`. Un retard d'une frame est accepté. Le thread ne touche jamais l'API Unity hors données pures. Repli : la clé locale `Performance.MassWorkerThread` (défaut `true`) le désactive et ramène l'évaluation sur le thread principal.

## 12. Rendu, matériaux, audio et effets

Les vers libres d'une mine sont dessinés en quelques appels instanciés à partir de maillages pré-animés (flipbook), sans `Animator`, sans `SkinnedMeshRenderer`, sans GameObject par ver. **Les vers d'un amas sont des cordes simulées et dessinées par le GPU** (§12.1, décisions 181 à 188), avec un shader propre livré dans un AssetBundle (décision 179) ; sans compute shader, l'amas retombe sur le chemin CPU (flipbooks et mise en page analytique, décision 186). Les vers libres gardent le shader vanilla `Custom/Creature` cloné (§12.2). Budget client : voir les tables de §12.1 (GPU : ≤ 0,25 ms de main thread, ≤ 16 appels de dessin du mod ; repli CPU : ≤ 0,3 ms et ≤ 64 appels).

### 12.1 Rendu des vers : colonie GPU (amas et vers libres), flipbooks en repli

- Chaque animation du ver libre est une série de maillages statiques exportés par Blender (§13) : `worm_crawl_00..15` (boucle de reptation, 16 images à 12 i/s), `worm_idle_00..07` (au repos, 8 images à 6 i/s, boucle), `worm_rear_00..05` (se dresser), `worm_strike_00..03` (frappe), `worm_sleep_00..03` (pulsation lente, 4 i/s), `worm_coil_00..11` (ver lové qui se tord sur lui-même, 12 images à 10 i/s, boucle), versions LOD1 de `crawl`, `coil` et `idle` (LOD0 : 282 triangles, LOD1 : 84, pour le ver de 30 cm), `nest_idle` (1 image + pulsation d'échelle en code). Les nœuds `knot_*` et brins `strand_*` de la passe V2-A restent dans `assets/build`, mais ne servent plus à l'amas (décision 180) : ils sont réservés à un LOD lointain optionnel, non fait.
- Reptation : le cycle `crawl` avance avec la distance parcourue : 12 images par `Worm.MoveSpeed` mètre. L'ondulation est serpentine et péristaltique, toutes deux bien marquées (valeurs en §13.1).
- Repos : un ver immobile depuis plus de 0,3 s joue `worm_idle` (la tête balaie ±15°, le corps se tortille faiblement) ; plus de pose figée au repos.
- Par frame : les vers visibles sont regroupés par (animation, image) et chaque groupe est dessiné par un `Graphics.RenderMeshInstanced` (`DrawMeshInstanced` est le précédent vanilla, non utilisé ; Unity 6000.0.75f1 pour Valheim 1.0.20, instancing disponible, d'après le dump d'exécution `docs/vanilla-reference-dump.md`) avec une matrice TRS par instance (position, cap, roulis ±8°, échelle 1 ± 0,1). Pas de teinte par instance : `RenderMeshInstanced` n'a pas de `MaterialPropertyBlock` par instance (décision 30).
- Culling : frustum de la caméra (boîte englobante par ver) et distance `Performance.RenderDistance` (60 m) ; les vers dormants hors champ ne coûtent rien.
- Ombres : projetées (`ShadowCastingMode.On`) jusqu'à 25 m, désactivées au-delà ; reçues toujours.
- **Les vers libres sont dessinés par la colonie GPU** (plan A7, fait en passe B2, `GpuFreeWorms`, décision 242) : chaque ver libre est une corde qui prend la pose de l'image de flipbook calculée par le CPU (crawl, idle, sommeil, cabré, frappe) ; le flipbook instancié reste le repli (`Performance.GpuWorms = Off`, pas de compute, pool plein). S11 mesure 2 à 3 appels de dessin au total.
- **La colonie GPU : un ver = une corde de 8 nœuds** (décisions 181 à 188). Chaque ver visuel d'un amas est une corde de K = 8 nœuds (30 cm, 4,29 cm entre nœuds, rayon de collision 0,9 cm) simulée en PBD dans un compute shader (`WormSim`) à pas fixe de 1/60 s (3 sous-pas au plus par frame). Les nœuds sont la vérité du rendu : un vertex shader (`MistWorms/WormGPU`) construit le tube le long de la spline Catmull-Rom des 8 nœuds. Code : `src/MistWorms/Render/Gpu/` (hôte `GpuWormHost`, capture `GpuEnvCapture`, ancres `GpuSkin*`, tube `GpuWormTube`, référence `WormSimRef`) et `Mass/GpuMassBridge.cs` (lien avec les amas du jeu). Les constantes sont nommées dans `WormSimConst` (§14).
  - **Le CPU garde le jeu** : `n`, `r(n)`, santé, volumes de coup, engloutissement, flux, fusion et dissolution ne changent pas de propriétaire. Le GPU reçoit de petits paramètres par amas (centre, vitesse, cap, rayon affiché, mode, voies de flux, cible et `engulf`) et l'environnement ; **aucun retour GPU vers CPU** hors `AsyncGPUReadback` de dev. Simulation locale à chaque client, purement visuelle, rien à synchroniser.
  - **Pas de simulation** : Verlet avec amortissement ; contraintes de longueur entre nœuds voisins et raidissement de courbure ; attraction vers la **forme cible** (dôme bas, décalé vers l'avant en marche, tapis roulant, ruban entre deux murs en coulée §4.7, filet de pseudopode vers la cible) ; **séparation** entre nœuds de vers différents par une grille de hachage uniforme (cellules de 2,5 cm, 8 identifiants par cellule, les plus petits, pour rester déterministe), qui donne l'enchevêtrement serré et la pression du fluide ; collisions contre l'environnement ; **reptation** (chaque ver avance le long de son axe, phase propre). Naissance d'un ver par expulsion d'un ver existant, mort par absorption sur 0,5 s, sans saut.
  - **Pseudopode GPU** (décision 236) : le filet de vers vers la cible existe aussi sur le GPU, sous forme d'un **filet cinématique de vers du noyau de cordes** (pas un rendu à part). Des vers du noyau sont tirés (gain 0,15, plafond 0,03 par pas) vers des places d'un convoyeur dirigé vers la cible, de longueur au plus `ArmMax`, avec ondulation, rétrécissement vers la pointe et quelques vers cabrés à la pointe ; il n'est actif que hors engloutissement (désactivé dès que l'amas enveloppe sa cible) et sa rétraction est lissée à 0,7 par seconde au plus. `GpuMassParams` gagne le champ `Pseudo` : **`MassStride` passe de 192 à 208 octets** (décision 187 complétée ; `GpuMassDev` ne lit que les 32 premiers floats, donc sans effet) ; la struct `MassParams` est partagée par l'include `WormMassParams.hlsl`.
  - **Déterminisme et parité** : pas d'atomique dont l'ordre change le résultat. `WormSimRef` est le portage C# exact d'un pas de la simulation ; `SimCheck` (éditeur) et V14 (en jeu, graine de 64 vers, tolérance de 1 mm) comparent le GPU lu par `AsyncGPUReadback` à cette référence ; les tests xunit couvrent la conservation des longueurs, l'absence de nœud sous la grille de sol, l'adhérence sur une capsule et l'écoulement.
  - **Effectifs** : **10 vers visuels par unité de jeu**, **2 400 au plus par amas**, 8 192 au plus pour tous les amas. Un `WritheMassVisual` à moins de **85 m** de la caméra reçoit un jumeau GPU (relâché à 110 m), **8 au plus** ; les autres gardent le chemin CPU. Au-delà de 35 m la simulation de l'amas est gelée ; rien n'est dessiné au-delà de 80 m.
  - **Environnement** (`GpuEnvCapture`, main thread, 8 Hz par amas, seulement visible et proche, décision 182) : une **grille de hauteur 16×16 du terrain nu** ; pour les **solides** (rochers, arbres, pièces), **5 cartes de profondeur** de 16×16 cellules ; jusqu'à **24 primitives** (boîtes orientées, capsules des `Character`, sphères). Tout passe par `RaycastCommand` en lot et des `GraphicsBuffer` préalloués ; aucun appel `Physics` depuis un thread de fond.
  - **Règle des obstacles** (décision 183) : un obstacle **bas** (couvrable, 1 m au plus) est recouvert ; un obstacle **haut** est contourné par l'IA de l'amas, et la simulation pousse les nœuds contre sa face puis les fait glisser le long de la face dans le sens de la marche ; aucune pénétration.
  - **Engloutissement** (décision 184, §6.6) : jusqu'à 512 ancres sur le maillage de la cible, skinnées sur le GPU (128 os au plus) ; effectif de la gaine = surface éligible × 2 couches de vers, plafonné à 60 % des vers ; remplissage de bas en haut le long d'un chemin de plus proches voisins ; plaquage sur le plan tangent ; grappes pendantes (8 %), chutes (2,5 s), bordure irrégulière ; 80 % de la gaine formés en 3,5 s en jeu. La capsule d'un être simplement traversé reste une primitive adhésive faible.
  - **LOD de rendu** : trois maillages de tube (LOD0 8 côtés × 20 anneaux, LOD1 6 × 10, LOD2 4 × 6) choisis par amas selon la distance au volume de l'amas : moins de **8 m**, **8 à 25 m**, au-delà. Ombres en LOD0 seulement, et pour un ver sur deux. Culling par amas (sphère englobante contre le frustum). Un appel de dessin par LOD et par passe, tous amas confondus.
  - **Repli CPU** (`Performance.GpuWorms` = `Off`, ou sans compute shader, ou sans bundle, ou shader non supporté) : l'amas garde la mise en page analytique `MassLayout` et les flipbooks du ver, avec un seul avertissement (décision 186). Ce chemin n'est plus développé et garde les anciens budgets.
  - **Budgets** (scène de test P4 : 2 amas de 200 et 100 vers libres dans le champ ; build `Profile`, §15.7 ; décision 188) :

    | Poste | Chemin GPU | Repli CPU (décision 175) |
    | --- | --- | --- |
    | Thread principal de rendu, moyenne (sondes, téléversement et soumission compris) | ≤ 0,25 ms | ≤ 0,3 ms |
    | Worker, par amas de 200 | sans objet | ≤ 1,0 ms |
    | Appels de dessin du mod | ≤ 16 (4 pour la colonie, 2 à 3 au total pour la colonie, vers libres compris, mesurés par S11, décision 242) | ≤ 64 (instanciés) |
    | Triangles vus par la caméra | ≤ 1,5 M | ≤ 450 k |
    | GC en régime établi | 0 | 0 |
    | Écart de temps d'image avec et sans amas | ≤ 2 ms | ≤ 2 ms |

- Interpolation : toutes les positions rendues sont lissées entre deux ticks de simulation (propriétaire) ou deux deltas (clients).

### 12.2 Matériaux et shaders

- Les matériaux sont construits en code avec le shader `Custom/Creature` **récupéré en premier sur le matériau d'un prefab vanilla** (`Tick`, puis `SeekerBrood`, décisions 20, 28) ; `Shader.Find` et le scan de `Performance.ShaderCandidates` ne sont que des replis de configuration, journalisés ; les propriétés (`_MainTex`, `_BumpMap`, lustre) sont lues sur ce matériau au dump, avec nos textures (§13). Densité de texels, filtrage et lustre du ver : §13.4.
- Décision à l'exécution, journalisée : (1) si le shader supporte l'instancing (`material.enableInstancing = true` accepté et un rendu de test sans erreur) → instancing ; (2) sinon, essai de la liste `Performance.ShaderCandidates` (`Custom/Creature`, `Custom/Piece`, `Standard`, `Legacy Shaders/Diffuse`) ; (3) sinon **repli** : fusion CPU des instances par groupe (`Mesh.CombineMeshes` dans des maillages dynamiques réutilisés, ≤ 8 appels) — plus coûteux mais fonctionnel, et le harnais vérifie que le budget tient.
- Caméra dans l'amas : quand la caméra est à moins de `r + 0,7 m` du centre, les vers à moins de 0,5 m de la caméra ne sont pas rendus (§6.6, phase 4B). Un dither d'alpha n'est envisagé que si un shader vanilla le permet (à vérifier ; sinon abandonné, §18).
- Un seul shader custom, `MistWorms/WormGPU` (et `WormRope`), embarqué dans l'AssetBundle `mistworms_shaders` (§13.5, décision 179) et utilisé par la colonie GPU des amas : éclairage Standard, rendu Deferred, ombres, brouillard, espace Linear, même peau « pâle de caverne » et même lustre (§13.4). Tous les autres rendus gardent `Custom/Creature` vanilla. Aucune texture au-delà de 1024², format PNG chargé via `ImageConversion.LoadImage`, mipmaps activées, compression à l'exécution (`Texture2D.Compress`).

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

- Coup sur un ver (phase 4A) : petit jet de sang vert-jaune acide, jet court d'environ 0,3 m, 6 à 12 particules, durée ≤ 0,5 s (c'est un petit mob, l'effet reste petit). Joué chez le pair qui porte le coup (là où le proxy est frappé), immédiatement ; les autres pairs à moins de `Network.SyncRadius` de la colonie le reçoivent par un événement de `MW_Delta` (§11.3) avec l'uid du frappeur, qui l'ignore (phase 8, décision 213).
- Mort d'un ver : le même sang vert en effet de mort, plafonné à 10 instances par seconde par colonie (plafond commun aux coups et aux morts, inchangé) ; joué chez le propriétaire, et chez les autres pairs proches par un événement de `MW_Delta`.
- Source des effets verts : d'abord un effet vanilla déjà vert, à chercher au dump (fouineur, couvain, blob, ooze…) ; sinon un clone unique, créé au chargement, de `fx_TickBloodHit` / `fx_tick_death`, recoloré (`ParticleSystem.main.startColor`, couleur du matériau des particules), sans `ZNetView` (effet local, purement visuel). Le template est mis en cache : jamais de recoloration par coup.
- Transitions de l'amas (rendu seulement, la logique reste celle de §4) :
  - rayon lissé : le rayon affiché suit `r(n)` avec un lissage de 0,3 s, jamais de saut (§4.5) ;
  - formation : les vers rassemblés **deviennent** les premiers vers du tas ; leurs positions de départ servent de point initial à leurs places, d'où un glissement continu vers le tas en 0,4 s ; le tas pousse de 0 à r(n) en 0,5 s (§4.1) ;
  - absorption : état Absorbing, 0,5 s (§3.3, §4.1) ; le ver grimpe sur le tas et devient un ver « flux » (rien ne s'enfonce) ; `n` augmente à la fin ;
  - fusion : les vers du petit tas coulent vers le gros, chacun vers une place libre, en 0,5 s (§4.1) ;
  - dissolution : les `n` vers libres partent des positions des vers visibles du tas (bond de 0,3 à 0,6 m), puis fuient ; le tas se résorbe en 0,4 s (§4.4) ;
  - mort d'un ver du tas : un ver visible se détache, glisse et tombe, et le sang vert gicle (plafond 10/s inchangé, §4.2) ;
  - caméra dans le tas (< r + 0,7 m) : les vers à moins de 0,5 m de la caméra ne sont pas rendus (§6.6) ;
  - coulée : transition tas ↔ ruban en 0,5 s, vers « flux » en file serrée dans l'axe (§4.7).
- Effets et sons en file : ils sont mis en file et joués depuis un `Update`. Construire un template pendant un événement d'animation (coup de mêlée) interdit `DestroyImmediate` et annulait le coup.
- Engloutissement : aucun effet écran ; l'icône de statut et les vers du tas qui recouvrent la cible suffisent (§12.1).
- Nid détruit : effet de destruction vanilla réutilisé + 5 vers qui sortent ; l'effet est joué chez le propriétaire du nid, chez le propriétaire de la colonie, et chez les pairs proches par un événement de `MW_Delta`.

## 13. Pipeline d'assets (Blender headless → glTF → runtime)

Claude Code fabrique les assets de maillage et de texture avec des scripts Blender versionnés, lancés en mode `--background` ; le jeu les charge à l'exécution avec un petit loader glTF maison. Les shaders et compute shaders de la colonie GPU sont l'exception : ils sont écrits dans un projet Unity 6000.0.75f1 (`assets/unity/MWShaders`), construits en `batchmode` par `tools/build-shaders.ps1` dans un AssetBundle, et chargés à l'exécution (§13.5, décision 179). Pas de fichier binaire fait à la main : `tools/build-assets.ps1` et `tools/build-shaders.ps1` doivent pouvoir tout régénérer depuis zéro.

### 13.1 Scripts (`assets/blender/`)

| Script | Produit | Détails |
| --- | --- | --- |
| `common.py` | utilitaires | export glTF binaire (`.glb`, triangles, normales, UV, sans squelette, sans animation), bake de textures procédurales en PNG, seed fixe pour un résultat déterministe |
| `make_worm.py` | `worm_crawl_00..15.glb`, `worm_coil_00..11.glb`, LOD1 de `crawl`, `coil` et `idle`, `worm_idle_00..07.glb`, `worm_rear_00..05.glb`, `worm_strike_00..03.glb`, `worm_sleep_00..03.glb`, `worm_albedo.png`, `worm_normal.png` (128 × 64 ou 128², filtrage `Point`, §13.4), `worm_gloss.png` (carte de lustre) | Corps en segments le long de +Z, longueur 0,30 m, diamètre du corps 1,8 cm (tête 2,1 cm, queue plus fine), LOD0 282 triangles, LOD1 84 ; origine au centre de masse au sol. Tous les absolus (ondes, amplitudes, mandibules, insets, plis) sont mis à l'échelle de la nouvelle longueur (décisions 169 et 178). Reptation (16 images) : ondulation latérale serpentine + onde péristaltique avec étirement longitudinal en phase. Coil (12 images, 10 i/s, boucle) : le ver lové en C et en S qui se tord sur lui-même. Repos (8 images) : la tête balaie ± 15°, le corps se tortille faiblement. « Se dresser » et frappe : mêmes mouvements, mis à l'échelle ; sommeil : pulsation ± 5 %. Topologie identique par animation pour une boucle sans couture. Texture « pâle de caverne » (§13.4), aucune référence à un asset existant. Bornes de `validate_assets.py` à jour |
| `make_knots.py` (conservé, hors de l'amas : LOD lointain optionnel, décision 180) | nœuds de vers : 3 tailles (S : 8 vers, M : 20, L : 40) × 2 variantes, `knot_*_00..11.glb` + LOD1 | Vers générés par des courbes entrelacées (tores déformés, tresses, pelotes) autour d'un volume d'environ 12, 20 et 30 cm, avec des vers qui dépassent ; flipbook de grouillement en boucle de 12 images (chaque ver ondule le long de sa courbe et glisse d'un cran) ; LOD1 simplifié par nœud. Triangles par nœud : S ≤ 1 k, M ≤ 2,5 k, L ≤ 4,5 k en LOD0, environ un quart en LOD1 |
| `make_strands.py` (conservé, hors de l'amas, décision 180) | brins, `strand_00..11.glb` + LOD1 | 4 à 6 vers torsadés sur environ 40 cm, flipbook de 12 images, LOD0 et LOD1 |
| `make_mass.py` (retiré) | — | Plus de noyau (décision 105) : `mass_core` et `mass_boil` ne sont plus produits ni exportés, et sont retirés du pipeline et du manifeste (décision 176) ; la mise en page analytique (`MASS_LAYOUT`, `mass_layout.py`, fichier doré de parité) disparaît au profit de la simulation de particules en C# (§12.1) |
| `make_mass_preview.py` | aperçus PNG hors jeu (`assets/build/previews`) | Ver seul à côté de l'ancien, nœuds S/M/L, brin, plus une composition statique de nœuds en dôme, pour juger le rendu avant le C#. Pas de mise en page analytique ni de parité avec le code. La direction artistique s'itère hors jeu, puis se vérifie en jeu |
| `make_nest.py` | `nest_idle.glb`, `nest_albedo.png` 1024², `nest_normal.png` | Monticule de mucus 0,9 × 0,9 × 0,5 m avec grappes d'œufs translucides, ≈ 2 000 triangles |
| `render_icon.py` | `thunderstore/icon.png` 256², `thunderstore/banner.png` 1024×256 | Rendu Eevee du tas de vers et de quelques vers sur fond sombre brumeux |
| `validate_assets.py` | `assets/build/manifest.json` | Vérifie dimensions, nombre de triangles, présence des images attendues ; écrit le manifeste lu par `AssetCatalog` |

Lancement : `& "$env:ProgramFiles\Blender Foundation\Blender 4.x\blender.exe" --background --python assets/blender/make_worm.py` (le script d'orchestration `tools/build-assets.ps1` enchaîne les cinq). Blender n'est jamais piloté par MCP pour le build final ; un MCP Blender peut servir à l'exploration, mais tout ce qui compte est dans les scripts.

### 13.2 Conventions d'échelle et d'axes

- Unités Blender = mètres ; export glTF avec `+Y up` (par défaut) ; le loader convertit vers Unity (main gauche) : `x → −x` et inversion de l'ordre des sommets de chaque triangle.
- Avant du ver = `+Z` glTF (devient `+Z` Unity, « forward ») ; origine au sol pour le ver et le nid, l'amas n'a plus de maillage propre (pas de noyau, décision 105) ; les nœuds et les brins de Blender (conservés hors de l'amas, décision 180) ont leur origine au centre de masse au sol ; les cordes GPU n'ont pas de maillage de Blender (§13.5).
- Les images d'un flipbook ont exactement la même topologie (même nombre de sommets et d'indices) : cela permet au loader de ne charger les indices qu'une fois et d'interpoler deux images adjacentes côté CPU si le budget le permet (option `Performance.FlipbookBlend`, défaut `false`).
- **Échelle** (décisions 169 et 178) : ver 0,30 m de long et 1,8 cm de diamètre (tête 2,1 cm) ; nœuds de Blender d'environ 12, 20 et 30 cm (S, M, L) et brins d'environ 40 cm, conservés mais hors de l'amas (décision 180). Un flipbook garde la même topologie sur toutes ses images, comme celui du ver. Les cordes GPU n'ont pas de maillage de Blender : le tube est généré en code (§13.5).

### 13.3 Loader runtime (`src/MistWorms/Assets/`)

- `GltfLoader` : lit un `.glb` (en-tête 12 octets, chunk JSON, chunk BIN), supporte uniquement : un ou plusieurs `meshes[].primitives[]` en mode triangles, accesseurs `POSITION` (float3), `NORMAL` (float3), `TEXCOORD_0` (float2), indices `UNSIGNED_SHORT` ou `UNSIGNED_INT`, `byteStride` optionnel ; tout le reste (skins, animations, matériaux glTF, Draco, textures intégrées) est ignoré ou refusé avec un message clair. ≈ 300 lignes, pur C# + `UnityEngine.Mesh` à la fin ; la partie parsing est testée hors du jeu (xunit, §15.6) avec les `.glb` du dépôt.
- `TextureLoader` : PNG → `Texture2D` (sRGB pour l'albédo, linéaire pour les normales, `linear = true`), mipmaps, `Compress(false)`.
- `AssetCatalog` : charge tout au démarrage du plugin (`Awake`, avant Jötunn `OnVanillaPrefabsAvailable`) depuis le dossier du plugin (`Path.GetDirectoryName(Info.Location)/assets`), construit les `Mesh` (`MarkDynamic` non, `UploadMeshData(true)` pour libérer la copie CPU sauf en mode blend), expose `Mesh[] WormCrawl`, etc., et loggue le temps de chargement (objectif < 300 ms, mesuré à 371 ms en phase 2) ; les nouvelles images (crawl 16, idle 8) ne doivent pas l'aggraver, à remesurer en phase 4A.
- Un asset manquant ou invalide désactive le mod proprement (log d'erreur, pas de spawn, pas de plantage).

### 13.4 Direction artistique (en une ligne par asset)

- **Ver** (« pâle de caverne ») : larve-asticot musculeuse plutôt que ver de terre. Peau blanc-jaunâtre un peu translucide (simulée dans l'albédo), veines vert acide visibles sous la peau, plis des anneaux plus sombres (gris-olive), tête et mandibules ambre-brun. Très luisant et gluant : le corps renvoie franchement la lumière des torches et des lampes dvergr. Style Valheim : silhouette low-poly lisible (anneaux nets), texture d'aspect peint à la main, grandes formes plutôt que bruit fin. **Même densité de texels et même mode de filtrage que les créatures vanilla** (dump du 24 oct. 2026) : `_MainTex` en 64² pour `SeekerBrood` (`babyseeker_d`) et `Tick` (`Feasting_d`), en 128² pour `Leech` (`swampfish_d`) et le blob, en 256² seulement pour `Seeker` et `SeekerBrute` ; toutes en filtrage `Point`, avec mipmaps, compressées DXT5 / BC7. Le ver prend donc une texture de 128 × 64 ou 128², `filterMode = Point`, mipmaps, comme `Leech` et le blob ; pas de bruit fin. Lustre : texture séparée `worm_gloss.png` (`_MetallicGlossMap` : RGB 0 = non métallique, A = lustre) avec `_UseGlossmap` 1 et le mot-clé `_USEGLOSSMAP_ON`, comme `Tick` (`Feasting_m`, alpha moyen 150/255, max 238) et `SeekerBrood`. Valeurs de départ : lustre ≈ 0,9 sur le corps, ≈ 0,5 dans les plis, `_Glossiness` 0,85 (repères vanilla : 0,54 `Leech`, 0,55 `Seeker`, 0,76 `SeekerBrood`, 0,75 à 0,93 pour les blobs sous `Custom/Blob`), à ajuster d'après V07 ; si la carte n'a aucun effet en jeu, lustre constant 0,85 et reflets peints dans l'albédo (plus discrets qu'aujourd'hui). Captures Blender (preview) : rugosité cohérente avec le lustre réel en jeu.
- **Amas** (cordes de vers simulées par le GPU, refonte V2 du 25 oct. 2026, décisions 100, 105 et 168 à 188) : des centaines de vers entremêlés, serrés et grouillants, qui se tortillent, rampent les uns sur les autres et plongent dans la masse ; on ne voit jamais une surface continue, jamais une « boule » lisse ni un noyau plein. Il coule comme un fluide, épouse le décor et enveloppe ce qu'il traverse en habillant sa vraie surface ; sa bordure est tirée vers l'avant en marche. Même ver « pâle de caverne » que les vers libres ; textures en `Point`, 128 px ; silhouette lisible et un peu chaotique.
- **Nid** : mucus brunâtre, grappes d'œufs ambrés translucides, quelque chose entre un sac d'œufs et une flaque de glu.
- Le harnais (§15) prend des captures de chaque asset à l'échelle du joueur ; Claude Code les regarde et itère sur les scripts jusqu'à ce que l'échelle (ver 0,30 m, amas aux rayons de §4.5) et la lisibilité soient bonnes.

### 13.5 Shaders et AssetBundle (décision 179)

- **Projet Unity** : `assets/unity/MWShaders`, Unity 6000.0.75f1 installé et licencié. Il contient les shaders `MistWorms/WormGPU` (tube procédural instancié, courbé en vertex shader le long de la spline des 8 nœuds d'une corde, Standard, Deferred, ombres, brouillard exponentiel, Linear) et `WormRope`, les compute shaders (`WormSim` et un noyau de test `MWTest`), des matériaux et une collection de variantes de shaders.
- **Build** : `tools/build-shaders.ps1` lance l'éditeur en `batchmode`, sur ce projet seulement (jamais en même temps qu'une autre session sur la même machine), et écrit `assets/build/mistworms_shaders`, versionné avec les autres assets. `tools/build.ps1` ne lance pas Unity : il copie le bundle avec le reste de `assets/build/`.
- **Chargement** : `GpuWormBundle` charge le bundle au démarrage ; s'il manque, si les compute shaders ne sont pas pris en charge ou si le shader n'est pas supporté, le mod garde le chemin CPU avec un avertissement (décision 186).
- **Rendu du jeu constaté** : D3D11, compute OK, Deferred, HDR, Linear, brouillard exponentiel.

## 14. Configuration BepInEx

Chaque nombre de ce document est une clé du fichier `BepInEx/config/dev.pointvirgule.mistworms.cfg`, sauf les constantes nommées documentées (voir la fin de cette section). Toutes les clés de jeu sont synchronisées depuis le serveur et verrouillées pour les clients (Jötunn `ConfigurationManagerAttributes { IsAdminOnly = true }`) ; seules les sections `Performance` (rendu, sons) et `Dev` restent locales. Chaque clé porte une description en anglais et une plage (`AcceptableValueRange`) ; une valeur hors plage est ramenée dans la plage avec un avertissement.

| Section | Clé | Défaut | Plage / type | Référence |
| --- | --- | --- | --- | --- |
| General | `Enabled` | true | bool | désactive tout spawn ; les entités existantes restent inertes |
| General | `ConfigVersion` | 5 | entier, géré par le mod | migrations de config (valeur lue égale à l'ancien défaut seulement) : v1 `MaxVisibleSurfaceWorms` 40 → 400 ; v2 400 → 600 ; v3 `Mass.BaseRadius` 0,83 → 0,26 et `Mass.MaxColliderRadius` 0,6 → 0,5 ; v4 `Mass.DetachDistance` 1,5 → 2,5 (décision 185) ; v5 (décision 191) `Colony.MaxAlivePerMine` 400 → 200, `Nest.MaxNestsPerMine` 24 → 8, `Nest.InitialWorms` 8 → 6, `Nest.MaxAlivePerNest` 50 → 25, `Nest.SpawnInterval` 20 → 30 |
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
| Mass | `BaseRadius` / `RadiusExponent` / `MaxColliderRadius` | 0.26 / 0.36 / 0.5 | 0.1–2 / 0.2–1 / 0.2–2 | §4.5 (défauts de `BaseRadius` et `MaxColliderRadius` : décisions 178 et 185, migration v3) |
| Mass | `MoveSpeed` / `TargetRange` / `EngulfRange` / `DetachDistance` / `DetachDelay` | 2.4 / 20 / 0.6 / 2.5 / 0.75 | — | §4.6, §4.9 (`DetachDistance` : décision 185, migration v4) |
| Mass | `KnockbackMinFactor` | 0.1 | 0–1 | §4.8 |
| Mass | `StreamHeightFactor` / `StreamLengthPerWorm` / `StreamMaxLength` / `RegroupRadius` | 0.4 / 0.08 / 6 / 30 | 0.1–1 / 0.01–0.5 m / 1–20 m / 5–100 m | §4.7, §4.4 |
| Engulf | `SlowFactor` / `WisplightLast` / `IncludeUnequippedInventory` / `ItemsPerTenWorms` | 0.5 / true / true / 1 | 0.1–1 / bool / bool / 1–10 | §6.2, §6.3 |
| Engulf | `BlockReducesBites` / `ArmorReducesBites` / `ScaleWithWorldModifiers` / `ShowDurabilityText` | true / false / true / true | bool | §6.4, §9.2 |
| Engulf | `WisplightVirtualDurability` | 100 | 1–10000 | §6.3 |
| Engulf | `WisplightPrefab` | `Demister` | string | §6.3 (tranché, décisions 8 et 9) |
| Targets | `AttackPlayers` / `AttackTamed` / `AttackCreatures` / `AttackStructures` | true ×4 | bool | §5.3 |
| Targets | `PlayerWeight` / `TamedWeight` / `CreatureWeight` / `StructureWeight` | 0.6 / 0.8 / 1.0 / 1.5 | 0.1–5 | §5.3 |
| Targets | `ExcludedPrefabs` | `SW_DivineWhale` | liste de prefabs séparés par des virgules | §5.3, §10.4 |
| Targets | `WakeOnlyByPlayers` / `ActiveRadius` / `CalmDelay` | true / 45 / 60 | bool / 10–200 m / 5–600 s | §5.1, §5.2 |
| Fire | `AfraidOfFire` / `FearRadius` / `FearDuration` | true / 4.0 / 3.0 | bool / 0–15 / 0–30 | §9.1 |
| Drops | `EquipmentDropRatio` / `IncludeUpgradeMaterials` | 1.0 / true | 0–1 / bool | §7.2 |
| Drops | `BaitChance` / `EntrailsChance` / `BloodbagChance` | 0.08 / 0.08 / 0.04 | 0–1 | §7.5 |
| Drops | `BaitWeights` | généré au premier lancement | `prefab:poids,…` | §7.5 |
| Drops | `MaxItemsPerDropEvent` | 60 | 1–500 | §7.3 |
| Nest | `Health` / `InitialWorms` / `LoneWormsPerMine` / `MaxLoneWormsPerRoom` / `SpawnInterval` / `MaxAlivePerNest` / `ReleaseOnDestroy` | 150 / 6 / 70 / 2 / 30 / 25 / 5 | — | §8.1 |
| Nest | `WakeRadius` / `MinRoomArea` / `MaxNestsPerMine` / `HitPause` | 10 / 25 / 8 / 3 | m / m² / 1–64 / 0–60 s | §8.1, §8.3 |
| Colony | `MaxAlivePerMine` / `AlertRadius` | 200 / 25 | 10–2000 / 5–100 | §8.2, §5.1 |
| Network | `SyncRate` / `SnapshotInterval` / `SyncRadius` | 5 / 5 / 120 | 1–20 Hz / 1–60 s / 30–300 m | §11.3 |
| Performance (local) | `RenderDistance` / `MaxVisibleSurfaceWorms` / `MaxSoundSources` / `ShadowDistance` | 60 / 600 / 4 / 25 | — | §12.1 (`MaxVisibleSurfaceWorms` = plafond de vers du tas en LOD0) |
| Performance (local) | `MassWorkerThread` | true | bool | §11.5 (chemin de repli CPU) |
| Performance (local) | `GpuWorms` | Auto | `Auto` / `Off` | §12.1, décision 186 : `Auto` dessine et simule les vers des amas sur le GPU (compute shaders et bundle requis, sinon repli CPU avec un avertissement) ; `Off` force le repli CPU |
| Performance (local) | `SimRateNear` / `SimRateFar` / `FlipbookBlend` / `ShaderCandidates` | 20 / 5 / false / `Custom/Creature,Custom/Piece,Standard,Legacy Shaders/Diffuse` | — | §11.1, §12.2 |
| Dev (builds `MW_DEV` seulement : section compilée et liée sous `MW_DEV` ; absente du `.cfg` d'une Release, les clés Dev d'un ancien `.cfg` restent inertes ; décision 235) | `BridgePort` / `AutoLoadWorld` / `AutoLoadCharacter` / `WorldSeed` / `AutoDevCommands` | 27781 / `MWTest` / `MWTester` / `MistWorms` / true | — | §15 |

Règles d'implémentation : une classe `MWConfig` statique expose chaque clé en `ConfigEntry<T>` typé, lue à chaud (pas de cache) sauf pour les capacités des tableaux (`Colony.MaxAlivePerMine`, lues au chargement de la colonie) ; un changement de config synchronisé en cours de partie est appliqué au tick suivant. Le fichier généré au premier lancement est commité dans `thunderstore/` comme référence (`default.cfg`).

Cadences de simulation (`Performance.SimRateNear` / `SimRateFar`) : bien que locales, ce sont celles du **propriétaire** de la colonie qui s'appliquent (§11.1).

Constantes nommées (documentées dans la spec, pas des clés de config) : poids et rayons du comportement de groupe des vers libres (§3.3, §11.1), mis à l'échelle du ver de 30 cm (décisions 173 et 178 ; `BodyMidOffset` 0,15 m) ; paramètres de coulée autres que ceux du tableau (§4.7) ; paramètres d'ondulation, de variation par instance et de transitions visuelles (§12.1, §12.4, §13.1) ; zone de coup de l'amas (§4.5) ; contact de fusion (1,3 m, §4.1) ; constantes de la colonie GPU (`WormSimConst` : 8 nœuds par corde, pas de 1/60 s, 3 sous-pas, 4 itérations, cellules de 2,5 cm, 24 primitives, grille 16×16, 512 ancres, 8 jumeaux, 8 192 vers) et de l'hôte GPU (`GpuMassBridge` : 10 vers par unité, 2 400 par amas, jumeau à 85 m, relâché à 110 m ; `GpuWormHost` : gel à 35 m, dessin jusqu'à 80 m, LOD à 8 m et 25 m ; `GpuEnvCapture` : 8 Hz) ; paramètre `engulf` ; aide de marche (§4.6, §12.1).

## 15. Harnais de dev et auto-tests in-game

Claude Code doit pouvoir, seul et sans écran, lancer Valheim moddé, entrer dans une mine, créer des vers, mesurer, prendre des captures, exécuter des scénarios à assertions et lire les résultats. Tout passe par un pont HTTP local embarqué dans la build Debug du mod et un outil en ligne de commande `tools/vh.py`.

### 15.1 Mise en place (`tools/setup-game.ps1`, `tools/setup-server.ps1`)

1. Localiser Valheim via le registre Steam et `libraryfolders.vdf` ; `robocopy` vers `C:\ValheimDev\` (une fois, puis `/MIR` pour suivre les mises à jour Steam).
2. Télécharger BepInExPack\_Valheim et Jötunn depuis Thunderstore, les extraire dans `C:\ValheimDev\` ; vérifier `BepInEx/LogOutput.log` au premier lancement (`Chainloader startup complete`).
3. SteamCMD : `+login anonymous +app_update 896660 validate` dans `C:\ValheimDevServer\` + BepInEx + Jötunn + le mod ; script `tools/run-server.ps1` (monde `MWServer`, mot de passe, port 2456, `-crossplay` off ; `-ConfigFile <chemin>` démarre avec un `.cfg` MistWorms de test, sauvegardé à part puis rétabli par `-Stop`, décision 224).
4. Copier `assembly_valheim.dll` & co dans `reference/game-dlls/` et décompiler avec `ilspycmd` dans `reference/decompiled/` (phase 0).

### 15.2 QuickStart (`Dev/QuickStart.cs`, `#if MW_DEV`)

- Arguments de ligne de commande lus par le mod : `-mwdev autoload=<monde>/<perso> bridge=<port> seed=<seed>` ; `tools/run-game.ps1` lance `C:\ValheimDev\valheim.exe -console -window-mode windowed -screen-width 1280 -screen-height 720 -mwdev autoload=MWTest/MWTester bridge=27781` (Steam doit tourner).
- Au `FejdStartup`, le mod crée s'ils manquent le profil `MWTester` (`PlayerProfile`) et le monde `MWTest` (`World` avec le seed `Dev.WorldSeed`), puis démarre une partie solo locale en reproduisant le chemin vanilla du bouton « Démarrer » (pas de serveur, pas de mot de passe) ; enchaînement tranché par l'analyse du code vanilla (`OnStartGame`, `OnCharacterStart`, `OnWorldStart` avec open=false et public=false, `TransitionToMainScene`), implémenté dans `Dev/QuickStart.cs` et validé à chaque `vh launch` (arrivée en jeu sans clic). Alternative `-mwdev join=127.0.0.1:2456` pour rejoindre le serveur local.
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
| `GET /perf` | FPS, ms de simulation (dernier, moyenne, max) et compteurs réseau par colonie chargée : octets envoyés/s (taille du paquet × destinataires) et reçus/s en moyenne glissante sur 10 s, paquets, keyframes, événements, CPU du miroir (dernier, moyenne, max) ; GC (collections, mémoire). Les ms de rendu et les appels de dessin sont mesurés par P4_MassPerf (phase 8-1 : la route existe, les compteurs de rendu n'y sont pas). Le bloc `net` (sommes) est aussi dans `GET /status` |
| `POST /camera` \`{mode: free | follow, pos, lookAt}`+`GET /screenshot\` |
| `POST /scenario` `{name, params}` → `GET /scenario/{id}` | lance un scénario (coroutine) ; résultat : `{status, assertions:[{name, ok, expected, actual}], shots:[…], log:[…]}` |
| `POST /time` `{scale}` / `POST /wait` `{seconds}` | accélération (`Time.timeScale` ≤ 4) et attente |
| `POST /quit` | quitte proprement |

Tout est exécuté sur le thread principal via une file ; les requêtes sont séquentielles ; le pont n'écoute jamais ailleurs que sur l'interface locale et n'existe pas en Release.

### 15.4 Outil en ligne de commande (`tools/vh.py`)

`vh launch [--join h:p [--no-mod]] | wait-ready | status | cmd "…" | tp-mine | spawn N | mass N | state | perf | shot [nom] | clip [nom] | assemble | scenario NOM | scenario-all | s17 | log-tail | server start|stop | quit`. Chaque commande imprime du JSON ; `scenario-all` écrit `docs/test-report.md` (tableau scénario → OK/KO, chiffres de perf, liens vers les captures) et retourne un code de sortie non nul si un scénario échoue (un scénario `skipped`, par exemple S17 hors serveur dédié, n'est pas un échec ; la check-list à 2 joueurs, après la ligne `<!-- manual-checklist -->`, est conservée à chaque régénération). `vh s17` : serveur dédié avec un `.cfg` de test, client joint, S17, puis un client **sans mod** (`DOORSTOP_DISABLE=1` pour ce seul processus) laissé 60 s et la trace de son refus lue dans le journal du serveur ; il écrit `docs/s17-summary.json` (décision 224).

### 15.5 Scénarios (`Dev/Scenarios/*.cs`, chacun une classe `IDevScenario` avec `IEnumerator Run(ScenarioContext)`)

| Id | Scénario | Assertions principales |
| --- | --- | --- |
| S00 | S00_Smoke : démarrage, pont de dev, scénario trivial (outillage de la phase 1) | plugin chargé (GUID, version), config liée au plugin (`Worm.Health`), jeton `$mw_mass` traduit, joueur local présent en jeu |
| S01 | Ver seul, une épée en bronze | mort en 1 coup ; aucun drop de pool ; drop natif selon les probabilités sur 200 répétitions (± 3 pts) |
| S02 | 40 vers spawnés, joueur visible (le spawn garantit la ligne de vue ver → joueur) | un seul amas de 40 en ≤ 10 s ; `hp = 40 × Worm.Health` ; nom « Amas grouillant (40) » |
| S03 | Coup injecté de 55 contondant sur un amas de 40 | n = 35, partial = 5 ; 22 perçant → n = 34 (11 effectifs, partial 6) ; 30 feu → ×2 = 60 → n = 28, partial 6 |
| S02b | 240 vers spawnés, joueur visible (variante de S02) | fusion jusqu'à 200, jamais au-delà : un amas de 200 et un de 40 |
| S03b | Parade sans stagger (variante de S03, phase 5 : elle demande le blocage des morsures) | une parade parfaite contre un tick d'amas en Engulf : tick annulé (durabilité et PV inchangés), écart jusqu'au tick suivant de 1,0 s ± 0,1, aucun stagger de l'amas ; plus un blocage simple de comparaison : perte réduite |
| S04 | Engloutissement, set rembourré complet q1 + Wisplight + 10 objets divers, mode dieu off | (amas de 40, terrain ouvert puis mine) effet Infesté actif ; vitesse × 0,5 mesurée sur 2 s de marche forcée ; PV inchangés tant qu'un éligible subsiste ; perte de durabilité totale ≈ 80/s ± 25 % ; Wisplight intact tant qu'il reste autre chose ; puis Wisplight ; puis PV baissent ; texte « Équipement −P » affiché ; roulade : tick annulé et pas de libération ; blocage simple : perte réduite ; détachement par sprint (téléport de dev du joueur à `r + 2,5 m`, puis 1 s) ; Infesté retiré ensuite ; clip de l'engloutissement |
| S05 | Pool : inventaire = cuirasse rembourrée seule ; l'engloutissement est arrêté (`mw_kill` du lien, ou détachement forcé) quand la cuirasse a perdu 50 % de sa durabilité ; puis deux amas identiques tués par coups injectés, l'un en une fois, l'autre ver par ver | pour **chaque** ressource de la recette q1 (10 fer, 20 fil de lin, et toute autre), butin = floor(coût × d / Dmax), où d = durabilité réellement perdue par la cuirasse (lue avant les coups) ; même résultat dans les deux cas ; aucun reste positif après le dernier ver |
| S06 | Torche en main | torche tenue : aucun engloutissement pendant 10 s, amas à ≥ 4 m **du bord** ; épée équipée → engloutissement en ≤ 5 s ; un feu de camp posé à 3 m d'un amas en Engulf le détache et le fait fuir 3 s ; un ver libre près d'un joueur qui tient une torche fuit |
| S07 | Amas de 8 ; 4 tués | amas dissous, 4 vers libres en fuite ; regroupement en ≤ 15 s si 3+ proches ; hauteur de bond vérifiée sur le sommet analytique |
| S08 | Coulée : amas de 200 dans un couloir de mine de 2 m (site plat, recherche jusqu'à environ 300 m ; tolérance de pente essayée à 0,5, 0,8 puis 1,2 m, la retenue est journalisée, et l'intérieur du couloir est échantillonné comme le reste de la bande ; à confirmer en jeu) | progression ≥ 10 m en ≤ 20 s ; mode Stream observé ; proxies frappables pendant la coulée |
| S09 | S09_NestSpawn : un nid posé en terrain ouvert, `SpawnInterval` forcé à 2 s (dev) | l'objet est conforme (PV, boîte 0,9 × 0,5, sans pièce d'œuf vanilla, persistant, sans effet vanilla réseau) ; +3 vers en 6 s (± 1), endormis, à 1–2 m, rattachés au nid ; arrêt à `MaxAlivePerNest` (abaissé pour le test) et reprise quand on le relève ; un coup réduit les PV et aucune ponte pendant 3 s (`Nest.HitPause`), puis elle reprend ; un coup mortel retire le nid et son ZDO, libère `ReleaseOnDestroy` vers actifs et ne laisse aucun objet |
| S10 | S10_MineCap : 250 vers demandés sans `force` | vivants ≤ 200 (`Colony.MaxAlivePerMine`) et exactement 200 atteints, une mine pleine refuse davantage ; `force` dépasse (Debug) ; un nid d'une mine pleine ne pond rien, puis pond de nouveau quand il y a de la place, sans jamais dépasser 200 |
| S11 | Perf : 400 vers actifs + 2 amas de 200, 30 s, caméra sur la mêlée. **400 vers forcés** (`force`, en dev) : un essai de charge à 2 × le plafond de jeu de 200 par mine (décision 191, qui reste) ; budget de rendu ≤ 0,5 ms (décision 218) | sim moyenne ≤ 1,0 ms, max ≤ 3 ms ; rendu ≤ 0,5 ms ; appels instanciés ≤ 64 au total ; GC ≤ 0,5 Mo sur 30 s ; réseau ≤ 6 Ko/s (en multijoueur S17) ; passe 8-2, décision 222 : mesures de P4_MassPerf (rendu = renderer à plans + hôte GPU, appels des deux), `Colony.MaxAlivePerMine` porté à 400 pendant le scénario (la capacité est lue à la création de la colonie), GC = hausse du tas autour de la mise à jour du propriétaire (`WormColony.DevMeasureAlloc`), bloc `render` de `GET /perf` |
| S12 | Fouineur spawné près d'un amas actif dans la mine, **sans retrait des créatures** (exception à la règle de retrait ci-dessous) | le fouineur prend des dégâts et riposte ; à sa mort, aucun objet au sol et pool augmenté exactement de la liste produite par `GenerateDropList`, capturée par le patch et journalisée ; partie 2 : le Seeker a la moitié du dégât réel d'un coup de cobra après résistance, un seul coup le tue (à confirmer en jeu) |
| S13 | Porte en bois construite près d'un amas, **en terrain ouvert** (la mine peut interdire la construction) | porte détruite ; ressources (÷3 vanilla selon le cas) dans le pool, rien au sol |
| S14 | S14_CalmSleep (phase 7-2) : vraie mine (`/tp-mine`, ensemencée par le plugin), `CalmDelay` forcé à 5 s (restauré), plus aucune cible attaquée (clés `Targets.Attack*` coupées, restaurées). Le joueur frappe un nid par la voie `HitData` réelle (attaquant = le joueur local) ; un amas est formé dans le groupe réveillé (poignée de dev qui lui donne ses `nestCounts`) et quelques vers reçoivent un pool ; le joueur sort de la mine (la porte, à la surface), puis revient | groupe touché éveillé en ≤ 0,3 s ; les groupes à moins de `Colony.AlertRadius` éveillés un saut après l'autre, à 0,5 s par saut comme la règle pure le prédit (`AlertMath.Schedule`), un groupe hors chaîne reste endormi ; en 5 + 30 + 20 s : tous les vers Sleeping, aucun amas, aucun groupe éveillé, même nombre de vers (libres + amas), même pool à 1e-3 près (vers, amas, réserve), chaque dormant issu de l'amas à ≤ 2,5 m de son nid, aucune ponte pendant le calme ; au retour près du nid : groupe éveillé en < 1 s, même nombre de vers, même pool |
| S14b | S14b_WakeRules (phase 7-2) : terrain ouvert, colonie de dev à 4 nids (3 alignés à 15 m, le quatrième à 60 m du premier), 5 dormants par nid et un ver isolé ; personne ne chasse | un coup sans attaquant et un coup d'une créature (Boar, si le prefab existe) ne réveillent rien (les PV baissent) ; un coup du joueur sur le nid 1 réveille le groupe 1 tout de suite, le 2 à environ 0,5 s, le 3 à environ 1 s, et le 4 reste endormi avec ses vers ; un joueur en ligne de vue à 9, 8 ou 7 m (horizontal) du nid 4, dont le centre est à au plus `WakeRadius` − 0,5 m en 3D de l'œil du nid, le réveille en ≤ 1,2 s (sans point convenable sur le site : étape sautée, avec une note, pas un échec), pas derrière un mur de dev (`BoxCollider`) ; un joueur immobile ou accroupi (aucun bruit de pas) à 5 m du ver isolé ne le réveille pas, le bruit d'un joueur qui marche (`AddNoise(15)` répété) le réveille |
| S15 | S15_Persist (phase 7-3) : vraie mine ensemencée, attaques et rassemblement coupés ; paramètre `step`. `reload` : un groupe réveillé, pools sur 4 vers, réserve, quelques vers blessés, 10 s de vie, instantané forcé, relevé ; le joueur va au temple de départ (≥ 1 000 m), on attend le déchargement de la colonie, retour par la porte. `save` : même préparation, relevé écrit dans `BepInEx/config/mistworms_s15_expect.json`, sauvegarde du monde ; le coordinateur redémarre le jeu. `check` : après relance, retour par la porte et comparaison au fichier | l'instantané est dans le ZDO après le déchargement ; même ZDO de colonie ; nouvel objet colonie restauré depuis `mw_snap` ; les vers se rassemblent en amas dans la seconde qui suit la restauration, donc les vers libres, leurs PV et leurs pools ne sont plus ceux de la sauvegarde : total des vers (libres + amas) ± 1 ; PV lus par la restauration (compteur de la colonie) à 0,006 par ver près ; butin lu par la restauration (pools des vers + réserve) à 1e-3 près ; butin total conservé (vers + amas + réserve) à 1e-3 près ; mêmes nids (index, position à 5 cm, PV) ; rien hors de la boîte ; aucun second ensemencement (`check` : aucun du tout dans la session) |
| S16 | S16_MineSeed : ensemencement paresseux d'une vraie mine (`/tp-mine`, colonie créée sans outil de dev) ; le test d'application du postfix de `DungeonGenerator.Load` | 1 à 8 nids, distincts, à ≥ 6 m les uns des autres et ≥ 20 m de l'entrée, à pleine santé ; couvée de 6 autour de chaque nid ; au plus 2 vers isolés par salle, à ≥ 4 m d'un nid ; tous endormis ; total initial = `6 × nids + min(70, 2 × salles éligibles)` à ± 5 % ; rien hors de la boîte ; salles vanilla intactes ; sortir de la mine puis y rentrer ne ré-ensemence pas (un seul ensemencement, mêmes nids, `mw_seeded` conservé) |
| S17 | Multijoueur local : **serveur dédié + 1 client** (décision 218 : une seconde instance de Valheim ne démarre pas sur le même compte Steam, §18) ; un test réel à 2 joueurs est fait par l'utilisateur avec la check-list de `docs/test-report.md`. Le mode de dev `serverown` (`MW_DEV`, passe 8-2) fait revendiquer et simuler la colonie de dev par le serveur dédié, ce qui fait du client un vrai non-propriétaire | config imposée (une clé admin-only modifiée dans le `.cfg` du serveur est lue côté client) ; client sans mod refusé (lecture du refus dans le journal du serveur) ; en non-propriétaire, l'écart moyen entre le miroir et le serveur ≤ 0,5 m pendant 20 s d'errance et de chasse, avec un écart de compte nul ; un coup du client sur un ver baisse ses PV chez le serveur, un coup sur un amas baisse ses PV ; des événements distants sont reçus ; bande passante ≤ 6 Ko/s avec 200 vers éveillés ; CPU du miroir ≤ 0,3 ms. **Passe 8-2** (décisions 220 à 224 ; code, outillage et tests hors jeu faits, **première exécution en jeu à faire**) : `serverown` par les RPC de dev `MW_DevServer` / `MW_DevState` (position de référence du serveur posée sur la colonie pour qu'il l'instancie, `ZDO.SetOwner`, verrou de dev contre la libération vanilla) ; statut `skipped` hors serveur dédié ; écart de position pris à l'instant médian de la requête ; coup du client par `HitData` réel, sang joué une fois, événement revenu non rejoué (`NetEventsEchoed`) ; amas formé sur le serveur (`mass`) ; orchestration `vh.py s17` (serveur avec `.cfg` de test, client, S17, client sans mod refusé, JSON de synthèse `docs/s17-summary.json`) |
| S18 | S18_GroupCoherence : a) 30 vers chassent un joueur immobile en terrain ouvert ; b) 20 vers en mine, spawnés en passes autour du joueur (comme P3_Wander) avec ligne de vue (phase 4A) | a) en régime établi, ≥ 95 % des paires de voisins ont leurs milieux de corps à plus de 0,2 m (les paires dont les deux vers sont dans la détente de 0,25 s sont exclues du ratio ; la séparation est prioritaire pendant la récupération, repositionnement 0,5×), et les chasseurs arrivés couvrent ≥ 240° autour de la cible (secteurs de 30° occupés) ; b) aucun ver sans progrès (moins de 0,5 m) pendant plus de 6 s alors que son but est atteignable (compatible avec le chemin demandé à 3 s, §11.1 ; un ver à moins de 2,5 m du joueur ou en Striking n'est pas compté comme bloqué), et aucune violation mur/sol (mêmes contrôles que P3_Wander). Le couloir de 2,5 m n'est pas une précondition : la largeur du passage le plus étroit et le taux de chemins de chasse sont informatifs |
| S19 | S19_Factions : une créature spawnée par faction pertinente (`MistlandsMonsters`, `Dverger`, `ForestMonsters`, etc.), l'amas à portée, ligne de vue dégagée | la matrice `IsEnemy` réelle (amas contre chaque créature, dans les deux sens, imprimée par `/state`) est identique à la matrice attendue de §5.4 |
| V01 | Visuel : le ver de près, à l'échelle du joueur (refait en phase 4A : look « pâle de caverne », crawl et idle) | captures relues par Claude Code : rien d'invisible ou de noir, échelle 0,30 m (ver de Ø 1,8 cm, décision 178), ombres présentes |
| V02–V05 | Visuels : V02 amas de 10, V03 amas de 200, V04 amas de 50 (comme le code), V05 engloutissement, à côté du joueur ; V02 à V04 refaits en phase 4B (à l'arrêt, avec séquences de forme : la silhouette change en continu) avec un vrai `MW_WritheMass` en tas de vers, dans une vraie mine avec repli en extérieur | captures relues par Claude Code : rien d'invisible ou de noir, échelles conformes à §4.5, ombres présentes, tas lisible comme des vers entremêlés ; le repère « 20 ≈ couvain » se vérifie en V02/V04 par la table, sans capture dédiée |
| V06 | V06_Nest (phase 7-1) : un vrai `MW_WormNest` dans la mine (repli en extérieur), captures à 1 m et à 3 m, puis de sa destruction à +0,05, +0,2 et +0,5 s | le moteur de rendu dessine le monticule (≥ 1 instance), l'effet de destruction est une copie locale sans `ZNetView` avec un son, le nid et son ZDO disparaissent, les vers sont libérés, rien ne tombe ; l'aspect se juge aux captures |
| V07 | V07_WormLight : ver de près sous une torche, puis sous une lampe dvergr ; captures à 0,5 m et 2 m, et séquence de 4 captures de reptation (phase 4A) | reflets nets, ondulation en S lisible |
| V08 | V08_BloodHit : coup sur un ver, capture au moment du jet, puis à la mort (phase 4A) | jet vert, petit (≤ 0,6 m ; le jet varie de 0,25 à 0,58 m, la valeur nominale restant ≈ 0,3 m) |
| V09 | V09_MassTransitions : captures de la formation (3 vers puis tas), d'une absorption, d'une fusion, d'une dissolution et d'une mort de ver du tas (phase 4B-4, adapté au modèle en tas de vers ; dans une vraie mine avec repli en extérieur, clip cadré à 2 m ; le nettoyage et la garde des créatures sont centrés sur l'ancre du site, et le joueur est téléporté près de l'ancre si le site est à plus de 20 m ; à confirmer en jeu) | transitions lisibles, jamais de saut de rayon |
| V10 | V10_MassMoving : un amas de 50, puis de 200, chasse le joueur sur terrain ouvert (course de 25 m ou plus) ; séquence de captures de côté et de face (grouillement, filet de vers tiré vers la cible (pseudopode), bordure tirée vers l'avant) ; puis coulée dans le couloir artificiel de S08 (phase 4B-3) | avance lisible comme une colonie, pas un tas inerte qui glisse ; filet vers la cible visible ; écoulement naturel et brins en file en coulée |
| V11 | V11_MassEngulf : captures et clip d'un amas de 50 puis de 200 qui englobe un joueur immobile (`engulf` forcé en dev avant la phase 5) (phase 4B-3 ; dans une vraie mine avec repli en extérieur) | vers adhérents qui montent autour du joueur et le recouvrent, jamais de forme pleine au centre |
| V12 | V12_MassEnvelop (passe V2-B2, décisions 183 et 184) : trois actes sur terrain ouvert en Prairies, sans créature à 40 m, joueur en mode dieu, un amas de 100 piloté par l'IA du jeu : (1) il contourne un rocher vanilla pour atteindre le joueur caché derrière ; (2) il franchit une pierre basse (environ la moitié de la hauteur de son dôme) ; (3) il engloutit le joueur ; captures à 1, 3 et 10 m et clip par acte | moins de 2 % des nœuds dans le rocher (boîte de ses bornes et collider à 4 mm, décision 261) pendant le contournement ; rien dans la pierre basse, des nœuds par-dessus ; la gaine se forme sur le vrai corps du joueur, le temps pour atteindre 80 % de son régime est journalisé (3,5 s en jeu) ; deux mesures visuelles de contact avec le rocher sont **en cours** (§16) |
| V13 | V13_GpuWorms (passe V2-A, prototype) : 1 000, 5 000 puis 10 000 vers GPU (shader du bundle, instanciation procédurale) autour du joueur ; trois captures chacune, à 1, 3 et 10 m | bundle chargé, shader supporté, noyau de calcul correct ; coût du main thread ≤ 0,2 ms avec 10 000 vers (mesuré : 0,03 ms) ; aucune allocation gérée en régime établi ; l'absence d'allocation est jugée contre un témoin (attente active de même durée), le tas du processus montant d'un bloc de 4 Ko environ par seconde (décision 252) |
| V14 | V14_GpuMassSandbox (passe V2-B1, décisions 181 à 184) : bac à sable de la colonie GPU avec l'amas fantôme `mw_gpumass` (aucune logique de jeu), terrain ouvert, créatures retirées à 40 m, joueur en mode dieu ; d'abord la parité de la simulation GPU avec la référence CPU (64 vers, 30 pas, 1 mm) ; puis, pour un amas de 50 et un de 200 unités : à l'arrêt, en marche sur 6 m, au contact d'un rocher haut (1,8 à 3,5 m, règle de V12, décision 262) ou d'une pièce en pierre, enveloppant le joueur ; captures `close` / `fit` / `wide` (sujet à 60 / 50 / 40 % de la hauteur de l'image, décision 244), clip de la marche (50) ; reproductible (décision 245) : site fixe depuis le temple de départ, bloc orienté vers l'approche et masse arrêtée à son milieu, attentes en pas de simulation (`StepIndex`) et, après `Clear()`, en frames, médianes sur 5 instantanés, pic de sélection = médiane de 3 passes après une passe de warm-up | main thread ≤ 0,25 ms par image en moyenne sur 300 images (capture, téléversement et soumission compris) ; aucune allocation gérée ; aucun nœud sous le sol de la simulation ni dans l'obstacle ; parité à 1 mm ; capsule enveloppée couverte à 60 % au moins de sa hauteur × `engulf`. Le jugement visuel se fait sur les captures. À la fin de la passe B1 : 144 réussis, 9 échecs (commit `08440cd`) ; l'amas fantôme marche droit dans les rochers hauts, il n'a pas d'IA (V12 teste le contournement) ; parité GPU/référence : 10 pas sous 0,05 mm, p99 à 30 pas sous 1 mm, pire nœud sous 10 mm ; flancs jugés sur une masse qui traverse le bloc (décisions 250 et 252) |
| P4_MassPerf | Budgets de rendu de l'amas (§12.1, décisions 175 et 188) : 2 amas de 200 et 100 vers libres dans le champ, build `Profile` (phase 4B-3, refait en passes V2-B1 et B2) | **Chemin GPU** (décision 188) : thread principal ≤ 0,25 ms en moyenne (vers libres, sondes, téléversement et soumission compris), triangles ≤ 1,5 M, appels de dessin du mod ≤ 16 (4 pour la colonie, 2 à 3 mesurés pour les vers libres en flipbooks, décision 242), 0 GC en régime établi, écart de temps d'image ≤ 2 ms. **Repli CPU** (décision 175) : thread principal ≤ 0,3 ms, worker ≤ 1,0 ms par amas de 200, appels instanciés ≤ 64, ≤ 450 k triangles, allocation contrôlée en différentiel (avec/sans amas), garde-fou en jeu ≤ 256 Ko par fenêtre de 300 frames, médiane (bruit de la sonde 20 à 80 Ko ; la preuve du zéro allocation est faite par les tests unitaires). À la fin de la passe B2, en jeu : les appels de dessin des vers libres (23 à 24 pour 20) dépassent leur part, car ils sont encore en flipbooks (**en cours**, §16) |

Les scénarios en mine retirent les créatures vanilla à moins de 40 m du joueur, ou de l'ancre quand l'action en est éloignée (V09) (elles tuent les amas, §5.4), sauf S12, qui garde son fouineur ; un journal de dev donne la cause de chaque disparition d'amas (`DestroyReason`, dernier attaquant).

**Scénarios de terrain ouvert (décision 189).** Un patch réservé au dev (`DevSpawnBlock`, `MW_DEV`) bloque `SpawnSystem.UpdateSpawning` et `CreatureSpawner.UpdateSpawner` pendant le scénario, pour que les créatures retirées ne reviennent pas : le scénario l'active sur son site, le lanceur le coupe à la fin de chacun. Les sites sont cherchés dans les Prairies d'abord (les Plaines ont des blobs de goudron et des fulings qui tuent les vers, la forêt noire des arbres qui bloquent fuite et chasse), un autre biome seulement si aucune Prairie n'a de terrain plat dégagé à portée (journalisé). Le patch n'existe pas en Release.

Refonte visuelle V2-B (décisions 178 à 190) : les assertions géométriques de V01 à V11 sont recalculées avec le ver de 30 cm et `R_{10}` = 0,26 m ; S01 à S19 restent la non-régression de gameplay (S03, S04 et S08 avec des seuils ajustés par calcul, décision 185). S08 : le couloir artificiel est dimensionné à partir de `r(n)` et c'est lui qui juge S08 ; l'escalier vanilla est une étape d'environnement non bloquante (log et capture, aucune assertion ; `stairs=1` par défaut, `S08b_MassStairs` la lance seule, décision 243).

**Chemin de référence et cadrage des scénarios visuels (décisions 241 et 244).** Les scénarios visuels tournent sur le chemin GPU (`ForceCpu` n'est plus imposé ; `force_cpu=1` rétablit le repli CPU pour tout scénario). Les assertions propres à la mise en page CPU (rôles, lobes, caps, sondes de flux et de bord, tread, poured, arm) ne sont jugées que sur le chemin CPU ; sur GPU elles sont remplacées par des équivalents mesurés sur les nœuds relus : `nodes_follow_the_root` (V10), `gpu_mass_centred_on_target` et `gpu_column_height` (V11), `stream_nothing_outside_the_walls` (S08), `drawn_count` GPU (V02 à V04). Les captures de V01 à V11 et V14 sont cadrées sur le sujet : il occupe 40 à 60 % de la hauteur de l'image, la distance venant du rayon du sujet et du FOV vertical ; noms de captures : `side_fit` / `close` (V02 à V04), `nest_close` / `nest_wide` (V06), `torch_near` / `torch_far` / `dvergr_near` / `dvergr_far` (V07), `*_{close,fit,wide}` (V14). V12, V13, V15 et S08 gardent des distances fixes.

Les clips (`vh.py clip` / `assemble`, en GIF ou WebP) font partie des livrables visuels des scénarios V : cadrage à 3 m (2 m pour V09) avec une lumière d'appoint de dev ; `ResetPlayerState` est appelé avant chaque scénario.

Scénarios propres à la phase 3, hors numérotation S/V : `P3_Wander` (100 vers errent en mine réelle sans traverser sol ni murs) et `P3_Cobra` (coup de cobra et règle d'équipement) ; ils sont rejoués à chaque phase.

Dans les scénarios de combat de groupe (S18), le joueur est épinglé toutes les 0,25 s (le recul des morsures est voulu) et les créatures vanilla à moins de 40 m sont retirées avant la partie b). La frame de la première frappe de la session (premier `Damage()` vanilla et JIT, de 1,5 à 5,4 ms) est exclue du max de simulation : ce n'est pas un coût récurrent.

Chaque scénario remet l'état à zéro (`mw_kill`, inventaire vidé, mode dieu) avant et après ; un scénario qui dépasse 120 s échoue ; une exception du mod dans le log pendant un scénario le fait échouer.
**Remise à zéro centrale (décision 253).** Avant chaque scénario le lanceur (`ScenarioReset.Begin`) oublie le dernier bon site, ramène le joueur au temple de départ (sauf un scénario marqué `IKeepsPlayerPosition`), exécute un `mw_kill` complet (vers, nids, réserves de butin, masses, colonies de test, objets posés), lève l'environnement forcé, fixe l'heure à 0,5, dégèle la simulation, remet les drapeaux à leur base, sauvegarde `MWConfig` (restauré après, chaque écart journalisé) et ensemence `WormColony`, `ColonyDev` et `UnityEngine.Random` à partir du nom du scénario : un passage isolé et un passage en série partent du même état. Le paramètre `site=x,z` impose le site d'un scénario de terrain ouvert (reproduction d'un échec). Les budgets de simulation de S18 jugent la trame sans le temps passé dans l'appel de dégâts vanilla d'un coup (décision 255), rapporté à part ; V10 filtre ses sites avec la mesure de son assertion (254) ; S12 retire les créatures vanilla à 15 m du site et vérifie que la victime est son fouineur (256) ; V12 choisit son rocher par critère (257).


### 15.6 Tests hors jeu (`src/MistWorms.Tests`, xunit, `dotnet test`)

Parseur glTF (sur les `.glb` du dépôt), maths du pool (§7.3, cas des 40 vers et de la fraction résiduelle), formule du rayon, sélection des pièces à mordre (distribution uniforme, Wisplight en dernier, report d'excédent), encodage/décodage des instantanés réseau, génération des poids d'appâts. Ces tests tournent sans le jeu et sont la première chose exécutée par `tools/build.ps1`.

### 15.7 Boucle de travail attendue de Claude Code

Toute mesure de budget (P4_MassPerf, S11) se fait avec la configuration de build `Profile` (optimisée, avec `MW_DEV`), jamais en Debug.

`build.ps1 -Debug` → `vh launch` → `vh wait-ready` → `vh tp-mine` (ou spawn en plaine de test) → scénario(s) → captures → lecture du log BepInEx (`BepInEx/LogOutput.log`, préfixe `[MistWorms]`) → correction → `vh quit`. Tout échec de scénario est corrigé avant de passer à la phase suivante (§16) ; le rapport final `docs/test-report.md` fait partie de la livraison.

## 16. Plan de réalisation

La livraison est un seul bloc, mais le travail suit dix phases (0 à 9) dans cet ordre, plus la passe V2 insérée avant la phase 7, chacune fermée par ses scénarios verts avant d'ouvrir la suivante : c'est ce qui permet à Claude Code de tester au fur et à mesure et d'isoler les régressions. Chaque phase se termine par un commit dont le message cite la phase.

1. **Phase 0 — Reconnaissance et outillage** (rien de visible en jeu). Installer les outils (§1.3), créer la copie de jeu et le serveur local (§15.1), décompiler le jeu, épingler les versions (`docs/versions.md`). Écrire un plugin « dump » temporaire qui exporte dans `docs/vanilla-reference.md` : stats des créatures de référence (`Tick`, `Seeker`, `SeekerBrood`, `SeekerBrute` : PV, dégâts, vitesses, portées de perception, dimensions de collider), noms des prefabs de rampants, des destructibles candidats, des `sfx_*` / `vfx_*`, du Wisplight, des appâts et leurs recettes, des entrailles et sac de sang, de l'énumération `Room.Theme`, des emplacements de mines, des shaders (avec support de l'instancing testé), de la couche des attaques, des durabilités et recettes des armures. Trancher tous les **\[À VÉRIFIER\]** et consigner les décisions dans `docs/decisions.md`. Fini quand : le dump est complet et la copie de jeu moddée démarre avec Jötunn sans erreur.
2. **Phase 1 — Squelette du mod et harnais**. Plugin, config (§14), localisation, QuickStart, DevBridge, `vh.py`, tests xunit vides, `build.ps1`. Fini quand : `vh launch` arrive en jeu dans `MWTest` sans clic, `/status` répond, `vh scenario` exécute un scénario vide, `dotnet test` passe.
3. **Phase 2 — Assets**. Scripts Blender, loader glTF, `AssetCatalog`, rendu instancié d'un ver et d'un dôme statiques devant le joueur, choix du shader. Fini quand : V01–V03 (ver, amas 10 et 200 immobiles) sont validés visuellement et le chargement prend < 300 ms.
4. **Phase 3 — Vers virtuels**. Colonie, SoA, états Idle/Hunting/Striking/Fleeing, proxies et routage des coups, coup de cobra avec règle d'équipement, drops natifs, mort. Fini quand : S01 passe, 100 vers errent sans tomber dans le sol ni traverser les murs (vérifié en mine réelle).
5. **Phase 4 — Finition du ver, puis amas** (**terminée**), en deux temps.
    - **Phase 4A — Finition du ver** (**terminée**). Asset « pâle de caverne » (§13.4), crawl 16 images et idle 8 images (§12.1), variation par instance (échelle, roulis), sang vert (§12.4), sons du ver seul (§12.3 : idle/reptation 4 sources max à < 12 m, cobra, coup reçu, mort), comportement de groupe des vers libres (§3.3, §11.1, sans formation d'amas), remesure du chargement des assets (< 300 ms, §13.3). Fini quand : V01 (refait), V07 et V08 sont validés visuellement ; S18 passe ; S00, S01, P3_Cobra et P3_Wander (mine) passent toujours.
    - **Phase 4B — Amas**, en cinq passes (**terminée**). Fini quand : S02, S03, S07 et S08 passent ; V02 à V04 sont refaits avec un vrai `MW_WritheMass` en tas de vers ; V09, V10 et V11 sont validés visuellement ; P4_MassPerf passe. Code dans `src/MistWorms/Mass/`.
        - **4B-1 — Entité** : prefab créature (avec `Humanoid`), PV, nom au survol, recul, pas de stagger, zone de coup (sphères `hitbox`, §4.5).
        - **4B-2 — IA et coulée** : `WritheMassAI` (états Idle, Hunt, Dormant en stub, §4.10), coulée, aide de marche et filet anti-chute (§4.6).
        - **4B-3 — Rendu « tas de vers »** : Blender (`coil`, LOD1, preview ; plus de noyau), code (remplacé par la passe V2-B : lobes, rôles, remplissage en volume, mouvement analytique, culling, LOD, rendu de l'englobement piloté par `engulf`, transitions de base), vers proches de la caméra masqués caméra dedans (§6.6), P4_MassPerf, V02 à V04, V10, V11.
        - **4B-4 — Côté colonie** : Gathering, formation, absorption, fusion, dissolution (après une perte seulement) avec les transitions de §12.4 ; S02, S02b, S07, V09.
        - **4B-5 — Sons de l'amas**, sang sur un coup sans perte, non-régression complète.
6. **Phase 5 — Engloutissement et équipement**. Effet Infesté, tick agrégé, patches `ApplyArmor` / `ApplyDamage`, sélection des pièces, Wisplight, phase PV, blocage, texte flottant, peur du feu (durée de fuite de l'amas = `Fire.FearDuration`), état Engulf de l'amas (§4.10), règle de parade du mod (§4.8, §6.4). Passes : 5-1 (état Engulf, tick, Infesté, choix des pièces, S04), 5-2 (feu et parade, S06, S03b), 5-3 (non-régression complète et documentation, captures et clips). Fini quand : S04, S06 et S03b passent.
7. **Phase 6 — Pool et drops**. Pool, `MW_Consumed`, parts entières par reste fractionnaire `carry` (§7.3), reliquat, capture des drops de créatures (patch `CharacterDrop.GenerateDropList`) et de structures, ciblage « tout », factions. Deux passes : 6-1 (pool : structure fixe, coût cumulé, `MW_Consumed`, partage, fusion, dissolution, S05) et 6-2 (ciblage « tout » et captures : créatures, structures, registre, S12, S13, S19). Fini quand : S05, S12, S13 et S19 passent (S19 = matrice `IsEnemy` conforme).
**Passe V2 — Refonte visuelle : vers fins, amas fluide et enchevêtré** (entre les phases 6 et 7 ; décisions 168 à 190). Demande de l'utilisateur du 25 oct. 2026. Le ver passe à **30 cm de long et Ø 1,8 cm** (après avoir vu 15 cm × 0,9 cm en jeu, décision 178), l'amas est fait de **vers individuels, cordes de 8 nœuds simulées et dessinées par le GPU** (Unity 6000.0.75f1 et AssetBundle de shaders, décisions 179 à 181), il épouse le décor et habille la vraie surface des êtres engloutis (décisions 182 à 184), à la taille de jeu `R_{10}` = 0,26 m (décision 185). Les nœuds de Blender ne sont plus la base de l'amas (décision 180). Ordre : spec et décisions en parallèle ; revue du diff, relecture contradictoire et tests en jeu entre chaque passe.
    - **V2-A — Assets Blender et prototype GPU** (**terminée**, commit `5997da1`). `make_worm.py` (30 cm, Ø 1,8 cm, LOD0 282 triangles, LOD1 84, flipbooks crawl 16, idle 8, coil 12), nœuds `make_knots.py` et brins `make_strands.py` (conservés hors de l'amas), retrait de `mass_core` et `mass_boil`, projet Unity `assets/unity/MWShaders`, shader `MistWorms/WormGPU`, noyau de calcul de test, build du bundle (`tools/build-shaders.ps1`), prototype de dev (`mw_gpuworms`, `/gpuworms`, V13_GpuWorms) : 10 000 vers à 0,03 ms de main thread par image en jeu. Une petite allocation par frame subsiste dans le chemin du prototype.
    - **V2-B1 — Noyau GPU et bac à sable** (**terminée**, commits `6947708` et `08440cd`). Colonie de cordes de 8 nœuds en compute shader, tubes en spline avec 3 LOD et ombres LOD0, capture de l'environnement par `RaycastCommand` (grille 16×16 du terrain nu, 5 cartes de profondeur, 24 primitives), engloutissement par ancres skinnées sur le GPU, référence CPU `WormSimRef` avec parité (`SimCheck`, V14) et tests xunit, bac à sable V14 avec l'amas fantôme `mw_gpumass`. En jeu (Profile) : 2 000 vers à environ 0,015 ms de main thread par image ; V14 : 144 réussis, 9 échecs à la fin du polish ; gaine formée à 80 % en 3,5 s en jeu.
    - **V2-B2 — Intégration au jeu** (**terminée avec réserves**, commit `4ef61f8`). `GpuMassBridge` : jumeau GPU pour chaque `WritheMassVisual` à moins de 85 m (8 au plus), 10 vers par unité (2 400 par amas), naissance et mort sur 0,5 s ; `R_{10}` 0,83 → 0,26 m, `MaxColliderRadius` 0,5, `DetachDistance` 2,5, contact de fusion 1,3 m, portée d'engloutissement (décision 185) ; migrations de config v3 et v4 ; `Performance.GpuWorms` et repli CPU (décision 186) ; ancres mises en cache par maillage et choisies sur un thread de fond ; sources de feu de l'amas ignorées ; V12_MassEnvelop ; blocage du spawn vanilla en dev (décision 189) ; budgets de P4 en GPU (décision 188).
    - **Ouvert à la date de la passe B2** (décision 190), tranché ensuite : vers libres déjà sur la colonie GPU depuis B2 (`GpuFreeWorms`, décision 242) ; pseudopode porté sur le GPU (D4, décision 236, à valider en jeu) ; S08 jugé sur le couloir construit, escalier non bloquant (D11, décision 243) ; S02b (6 vers sur 240 non conservés) et deux mesures visuelles de contact avec un rocher (V12), ainsi que le coût unique de 40 ms au choix des ancres, restent à relire au passage final.
    - **V2-B3 — Scénarios et finition** (**en cours**). V01 à V11 revus et recadrés (décisions 241 et 244), P4 aux budgets, non-régression S01 à S19, captures et clips pour l'utilisateur. Fini quand : `vh scenario-all` est vert avec V12, V13 et V14. **Reste à faire en jeu** : recompiler le bundle de shaders (`WormSim.compute` modifié, struct `MassParams` de 208 octets) et régénérer le manifest d'assets ; V10 et V11 (pseudopode GPU, rétraction, engloutissement) ; coût GPU du filet (P4) ; V14 deux fois de suite en Profile (même site, aucune assertion rouge, décision 245) ; V01 à V04, V06 à V09 recadrés ; V10, V11 et S08 sur GPU (seuils de `gpu_column_height` à recaler) et avec `force_cpu=1` ; hover du nid en jeu, y compris en multijoueur ; fusion annulée et gel de dev en Profile ; Release : `.cfg` sans section `[Dev]`, console réduite à `mw_version`, journal sans lignes par événement.
8. **Phase 7 — Nids, spawn, dormance, persistance**. Nid, ponte, plafonds, ensemencement des mines, alerte et retour au calme, instantanés, sauvegarde. Fini quand : S09, S10, S14, S15, S16 passent.
9. **Phase 8 — Performance et multijoueur** (**terminée**). Profilage, cadences LOD, deltas réseau, dead reckoning, serveur local, tests non-propriétaire, effets de coup et de mort des vers chez les pairs distants (§12.4), coût des amas en tant que créatures (≈ 650 octets par frame pour 2 amas : `Character`, IA, physique ; P4, G(a)). Fini quand : S11 et S17 passent avec les budgets de §11. **Passe 8-1** (faite, décisions 213 à 218) : `MW_Delta`, identité réseau, miroir, coups par `netId`, passation planifiée, garde du serveur dédié, compteurs ; tests hors jeu (`DeltaFormatTests`, `DeltaFilterTests`, `NetIdTests`, `WormMirrorTests`, `RateWindowTests`). **Passe 8-2** (faite, décisions 220 à 229 : mode `serverown`, S17, `vh.py s17` avec le client sans mod refusé, S11 à 400 vers, check-list à 2 joueurs dans `docs/test-report.md`). **S11 et S17 ont tourné en jeu** (décisions 226 à 229) : S17, serveur dédié + 1 client : deux causes corrigées (position de référence du serveur dédié, refus du client sans mod, décision 226) ; bande passante mesurée à 9 629 o/s pour 200 vers en chasse, ramenée par le format `MW_Delta` v2 (décision 228) ; S11 : pic de simulation de 7,15 ms ramené par l'étalement des pas (160 par frame au plus, décision 227), allocations de 2,6 Mo sur 30 s dont la part du code vanilla de dégâts est désormais mesurée à part (décision 229, budget de 512 Ko pour le seul code du mod). **Passage final vert en jeu (commit `2407cbc`)** : S17 36/36 (3 989 o/s envoyés pour 200 vers en chasse et 1 destinataire, erreur moyenne du miroir 0,146 m, p95 0,428 m, CPU du miroir 0,013 ms, client sans mod refusé) ; S11 vert (simulation moyenne 0,456 ms, pire frame 2,01 ms, rendu 0,24 ms, 2 appels, 152 Ko alloués par le code du mod sur 30 s, 2,67 Mo par les appels vanilla de dégâts, rapportés à part). Non-régression S01–S19 verte (S16 et P4 verts seuls ; dans la série, S16 souffre de l'ordre après S14 et P4 du bruit du pont de dev) ; V14 instable, rendu reproductible par la décision 245, à rejouer.
10. **Phase 9 — Packaging et livraison** (**en cours**). Version 1.0.0 (décision 230), build Release sans `Dev/`, manifest (`website_url` = `https://github.com/clementplancon/MistWorms`), README, CHANGELOG, icône, zip Thunderstore vérifié par `tools/verify-zip.ps1` (décision 231), guide serveur générique, `docs/test-report.md` final, revue du code (patches protégés, aucune allocation dans les boucles chaudes, aucun `Debug.Log` oublié ; décisions 237 à 239). Fait hors ligne : version, config `[Dev]` sous `MW_DEV`, logs et commandes de dev réduits, golden supprimé, `ReadmeDefaultsTests`, `package.ps1` + `verify-zip.ps1`, README Thunderstore et guide serveur. **Reste** : build Release, `dotnet test`, `tools/package.ps1` puis `verify-zip.ps1` (jamais exécutés ensemble), validation du zip sur `https://thunderstore.io/tools/package-validation/`, les vérifications en jeu de V2-B3 ci-dessus, `docs/test-report.md` final. Fini quand : §19 est entièrement coché.

Règles de conduite pendant les phases : jamais plus d'un **\[À VÉRIFIER\]** ouvert dans une phase en cours ; toute déviation de la spec (valeur, règle, nom) est notée dans `docs/decisions.md` avec sa raison ; un scénario rouge n'est jamais contourné en changeant ses assertions sans l'inscrire dans ce fichier ; les scénarios des phases précédentes sont rejoués (`vh scenario-all`) à la fin de chaque phase.

## 17. Packaging Thunderstore et installation

Le mod est publié sur Thunderstore sous le nom `MistWorms`, s'installe en un clic côté joueurs via r2modman ou Thunderstore Mod Manager, et se dépose sur un serveur dédié quelconque dans `BepInEx/plugins/`. Espace de noms Thunderstore : `kplir`, page `https://thunderstore.io/c/valheim/p/kplir/MistWorms/` (décision 233).

### 17.1 Package (`tools/package.ps1` → `MistWorms-<version>.zip`)

| Fichier | Contenu |
| --- | --- |
| `manifest.json` | `name: "MistWorms"`, `version_number` (SemVer, `1.0.0` à la première publication), `website_url` (dépôt GitHub), `description` (≤ 250 caractères, en anglais), `dependencies: ["denikson-BepInExPack_Valheim-<ver>", "ValheimModding-Jotunn-<ver>"]` aux versions épinglées en phase 0 |
| `README.md` | en anglais avec une section française : ce que fait le mod (résumé §0 en 10 lignes), contre-mesures (feu, zone), configuration (tableau §14 abrégé), installation serveur/client, compatibilité (Valheim 1.0.x, aucun autre mod testé), crédits (assets originaux générés avec Blender ; sons du jeu), licence (MIT pour le code, assets CC BY 4.0) |
| `CHANGELOG.md` | une entrée par version |
| `icon.png` | 256×256 rendu par `render_icon.py` |
| `plugins/MistWorms/MistWorms.dll` + `plugins/MistWorms/assets/**` + `plugins/MistWorms/Localization/**` | la build Release et ses assets ; le dossier est préservé tel quel par les gestionnaires de mods |

Le zip **exclut** `assets/build/previews/`, `mass_layout_golden.json` (supprimé, décision 234), les `.pdb` et les `.blend`, et **inclut** le bundle `mistworms_shaders` et le manifest d'assets (`assets/build/manifest.json`), que le runtime charge. `tools/package.ps1` garantit ces règles à l'assemblage et appelle `tools/verify-zip.ps1`, la porte de sortie : il revérifie le zip final (aucun dossier `Dev`, aucune preview, aucun golden, aucun `.pdb`, aucune DLL Profile ou Debug, fichiers Thunderstore requis présents) et fait échouer le package au moindre écart (décision 231). La version est `1.0.0` dans `manifest.json` et dans `Plugin.ModVersion`.

Publication : via le site Thunderstore (compte de l'utilisateur, à lui de cliquer) ou `tcli publish` avec son jeton ; Claude Code prépare le zip et valide le manifest avec l'outil en ligne de Thunderstore. Versionnage : patch pour un correctif, mineur pour un changement de règle ou de config, majeur pour un changement de format `mw_snap` ou de dépendance majeure.

### 17.2 Serveur dédié (guide générique)

Le guide complet est `docs/server-guide.md` (installation, configuration imposée, mot de passe, compatibilité, dépannage). Le mod s'installe sur un serveur comme n'importe quel autre mod, sans hébergeur particulier (décision 233) :

1. Sauvegarder le monde (dossier `worlds_local` ou sauvegarde du panel s'il y en a un).
2. Installer BepInExPack\_Valheim (dépôt du contenu du zip à la racine du serveur, ou option du panel si l'hébergeur en propose une) ; sur Linux le démarrage doit passer par `start_server_bepinex.sh` (ou la ligne de commande équivalente exportant `DOORSTOP_*` / `LD_PRELOAD`).
3. Déposer `BepInEx/plugins/Jotunn/` et `BepInEx/plugins/MistWorms/` ; démarrer ; vérifier dans `BepInEx/LogOutput.log` : `Loading [Jotunn …]`, `Loading [MistWorms …]`, aucune erreur.
4. Ajuster `BepInEx/config/dev.pointvirgule.mistworms.cfg` sur le serveur : ce sont ces valeurs qui s'imposent à tous (les clés administrateur sont synchronisées vers les clients par Jötunn ; S17 le vérifie avec `Colony.AlertRadius`). Le serveur dédié **ne simule aucune colonie** : ce sont les clients qui en sont propriétaires, il route (décision 215).
5. Le mod doit être installé sur le serveur **et** sur chaque client. Un client sans le mod (ou avec une version mineure différente) est refusé à la connexion avec un message Jötunn explicite. Côté serveur la trace se lit dans `BepInEx/LogOutput.log` (textes de Jötunn : « … is not installed on the client. Server has mandatory mods, cancelling connection », ou « Disconnecting modded client with incompatible version message ») ; `vh s17` la cherche (décision 224).

### 17.3 Joueurs

- r2modman ou Thunderstore Mod Manager → profil Valheim → installer `MistWorms` (BepInExPack et Jötunn viennent automatiquement) → lancer le jeu depuis le gestionnaire. L'utilisateur partage le **code de profil** exporté du gestionnaire pour que tout le monde ait exactement les mêmes versions.
- Le README donne aussi la méthode manuelle (zip BepInEx dans le dossier du jeu, puis les deux dossiers dans `BepInEx/plugins/`).
- Mise à jour : le serveur d'abord, puis les clients ; une mise à jour mineure du mod impose la mise à jour de tous.

## 18. Risques et solutions de repli

Les trois risques qui pourraient bloquer sont le shader (instancing), le démarrage automatique du jeu et le réseau à 400 vers ; chacun a un repli déjà décidé, pour que Claude Code n'ait jamais à s'arrêter pour demander.

| Risque | Probabilité | Repli décidé |
| --- | --- | --- |
| Aucun shader vanilla n'accepte l'instancing | moyenne | Fusion CPU des instances par groupe (§12.2) ; si le budget rendu dépasse 0,5 ms, abaisser les plafonds des LOD (LOD0 à 60 et LOD1 à 30, puis distances de LOD réduites) et le flipbook à 8 images ; en dernier recours, `Graphics.DrawMesh` par ver (≤ 400 appels) |
| Compute shaders ou bundle de shaders indisponibles sur la machine du joueur (GPU ancien, bundle absent ou refusé) | faible | Repli CPU : mise en page analytique et flipbooks, un seul avertissement ; clé `Performance.GpuWorms` = `Off` pour le forcer (§12.1, décision 186) |
| Pas de shader vanilla transparent utilisable pour le dôme | moyenne | Masquer les vers à moins de 0,5 m de la caméra quand elle est à l'intérieur ; pas de dither |
| QuickStart impossible à reproduire en 1.0 (`FejdStartup` changé) | faible | Rejoindre le serveur dédié local par `-mwdev join=` (le chemin de connexion est plus stable) ; les scénarios tournent alors en multijoueur 1 client |
| Une seconde instance de Valheim refuse de démarrer sur le même compte Steam | élevée | S17 se limite à serveur + 1 client ; les tests à 2+ joueurs sont faits par l'utilisateur et ses amis avec une check-list fournie dans `docs/test-report.md` |
| `HitData.m_attacker` sans `Character` (coup de cobra émis par la colonie) casse une attente vanilla | moyenne | Donner à la colonie un `Character` fantôme désactivé (pas d'IA, pas de collider, invisible) qui sert d'attaquant nominal ; nom localisé « Ver des brumes » pour les messages de mort |
| Un trigger sur la couche `character` n'est pas touché par certaines attaques (projectiles, `Aoe`) | moyenne | Collider non-trigger sur une couche dédiée aux hitbox **\[décidé en phase 0 par test\]** + `Physics.IgnoreCollision` systématique avec les joueurs |
| Réseau > 6 Ko/s avec 400 vers actifs | moyenne | (Déjà en place depuis la passe 8-1 : filtre par prédiction, 2 Hz au-delà de 30 m de tous les destinataires, rien hors de `Network.SyncRadius`.) Si la mesure dépasse encore 6 Ko/s : quantifier la position sur 1 octet par axe relatif au dernier envoi, ne jamais envoyer les vers hors de 120 m d'un pair |
| Pathfinding vanilla trop lent ou absent pour les vers libres | faible | Les vers libres n'en ont pas besoin dans 95 % des cas (salles ouvertes) ; un ver bloqué 10 s est téléporté de 1 m vers l'amas le plus proche, invisible sous la pénombre des mines ; un ver qui court vers un amas et ne progresse plus depuis 4 s est tiré dans l'amas à ≤ 10 m même si un obstacle (mur, roche trop raide) est sur la ligne (décision 212) |
| Les fouineurs attaquent les nids ou les vers dormants | faible | Nid sur une couche ignorée par `BaseAI.FindClosestStaticPriority` ; les dormants n'ont pas de `Character` donc n'existent pas pour l'IA |
| Hook de génération des donjons absent ou tardif | faible | Ensemencer au premier `Update` de la colonie créée par un postfix sur `Location`/`DungeonGenerator.Awake`, avec le même drapeau `mw_seeded` |
| Le budget de simulation dépasse 1 ms | faible | Cadence des vers « loin » à 2 Hz, perception à 1 Hz, découpage en 4 tranches ; mesure par `PerfProbe` avant toute optimisation |
| Mise à jour de Valheim qui casse un patch | certaine à terme | Patches protégés par `try/catch`, chacun vérifié au démarrage par un test d'applicabilité qui loggue un avertissement clair ; versions épinglées ; le harnais rejoue tout en 10 minutes |

Un repli activé est consigné dans `docs/decisions.md` et, s'il change une règle visible, dans le README.

## 19. Critères d'acceptation

Le mod est livrable quand chaque case est cochée avec, pour les règles de jeu, le scénario correspondant vert dans `docs/test-report.md`.

- [ ] Le jeu moddé (copie de dev) démarre avec BepInEx + Jötunn + MistWorms sans erreur ni avertissement dans `LogOutput.log`
- [ ] `vh launch` arrive en jeu sans intervention ; `vh scenario-all` termine en vert (S01–S19, S02b, S03b, V01–V12, P4_MassPerf)
- [ ] Un ver seul meurt d'un coup d'arme en bronze ; son coup de cobra ne touche que l'équipement (S01, S04)
- [ ] Le ver est « pâle de caverne », luisant sous les torches, ondule en S (serpentine + péristaltique) en marche et au repos, et jette un petit sang vert à chaque coup et à sa mort (V01, V07, V08)
- [ ] Des vers libres en groupe ne se chevauchent pas, avancent en file dans les couloirs et encerclent leur cible (S18)
- [ ] 3 vers proches forment un amas ; 40 vers forment un amas de 40 dont les PV et le nom reflètent le compte (S02)
- [ ] Un coup de D dégâts tue floor(D / PV d'un ver) vers, résistances comprises ; le feu compte double, le perçant moitié (S03)
- [ ] Sous 5 vers, après une perte, l'amas se dissout en fuyards qui se regroupent (S07) ; deux amas fusionnent jusqu'à 200 et jamais au-delà (S02b)
- [ ] Taille visuelle : amas aux rayons de §4.5 (`R_{10}` = 0,26 m ; r(200) = 0,76 m), ver de 30 cm (V01 à V04, décisions 178 et 185) ; un amas de 200 traverse un couloir de 2 m en coulée (S08)
- [ ] Aucun stagger ; recul décroissant ; parade sans stagger, qui annule le tick de morsure (S03b)
- [ ] L'amas est fait de vers individuels simulés par le GPU, sans noyau, coule comme un fluide, épouse le décor, avance comme une colonie, habille le corps d'un joueur et ne lague pas à 200 vers (V10, V11, V12, V14, P4_MassPerf) ; sans GPU compatible, le repli CPU tient (`Performance.GpuWorms` = `Off`)
- [ ] Les transitions de l'amas (formation, absorption, fusion, dissolution, mort d'un ver de surface) sont visibles et sans saut de rayon (V09)
- [ ] Engloutissement : Infesté, vitesse ÷ 2, attaques et roulade possibles, roulade sans libération (tick perdu), fuite par sprint, feu (torche tenue comprise) ou dissolution (S04, S06)
- [ ] Morsures : 1 point de durabilité par ver toutes les 0,5 s ; tout l'inventaire au hasard, équipé ou non (seules les pièces équipées si `IncludeUnequippedInventory` = false) ; Wisplight en dernier ; PV seulement après (le reste d'un tick va aux PV) ; blocage réduit, armure non ; parade parfaite : tick annulé, suivant à 1,0 s (S03b, S04)
- [ ] Pool : parts entières par ver tué, reliquat au dernier, arrondi vers le bas, recette de base + améliorations, rien pour les objets sans recette (S05)
- [ ] Tout est cible une fois éveillé (joueurs, apprivoisés, créatures, dvergar, structures) ; rien ne s'éveille sans joueur ; ce qui est dévoré va dans le pool (S12, S13) ; « rien ne s'éveille sans joueur » relève de S14, donc du critère de la phase 7
- [ ] Drops natifs : appâts pondérés par rareté, entrailles, sac de sang ; aucun trophée ; aucun asset nouveau pour les items (S01)
- [ ] Un nid par salle éligible + vers isolés ; ponte toutes les 30 s ; plafonds 25 par nid, 200 par mine, 200 par amas (et non les 50 / 400 de la phase 1) ; mine propre une fois les nids détruits ; jamais hors mine (S09, S10, S16)
- [ ] Factions : l'amas est ennemi de toutes les factions vanilla dans les deux sens, sauf `PlayerSpawned` et la baleine SkyWhale ; contre le mannequin, vrai de l'amas vers lui et faux dans l'autre sens ; contre un apprivoisé, vrai dans les deux sens (S19)
- [ ] Dormance et réveil conservent compteurs et pools ; sauvegarde/rechargement idem (S14, S15)
- [ ] Performance : 400 vers actifs + 2 amas de 200 ≤ 1,0 ms de simulation, ≤ 0,5 ms de rendu (aligné sur S11, décision 218), ≤ 64 appels instanciés au total, GC stable, ≤ 6 Ko/s (S11, S17)
- [ ] Multijoueur : config imposée par le serveur, client sans mod refusé, vers libres vus et frappables depuis un non-propriétaire, amas frappable depuis un non-propriétaire (S17 : serveur + 1 client ; le test à 2 joueurs réels passe par la check-list de `docs/test-report.md`)
- [ ] Assets originaux générés par les scripts Blender du dépôt, régénérables depuis zéro ; icône et bannière Thunderstore produites
- [ ] Textes FR et EN complets ; aucun texte en dur (exceptions assumées : descriptions de clés de config et commandes de dev en anglais, décisions 237)
- [ ] `ReadmeDefaultsTests` vert (les valeurs par défaut du README Thunderstore correspondent à `MWConfig`) et `tools/verify-zip.ps1` OK sur le zip final (décisions 231 et 232)
- [ ] Build Release sans pont de dev, sans section `[Dev]` dans le `.cfg` généré, ni commandes `mw_*` autres que `mw_version` ; zip Thunderstore validé ; README, CHANGELOG, guide serveur, `docs/decisions.md`, `docs/vanilla-reference.md`, `docs/test-report.md` à jour
- [ ] Tous les **\[À VÉRIFIER\]** de ce document ont une réponse consignée

## 20. Annexes

### 20.1 Données vanilla à vérifier en phase 0

Chaque ligne est une hypothèse de ce document ; Claude Code la confirme ou la corrige dans `docs/vanilla-reference.md` et reporte le résultat dans `VanillaNames` et `docs/decisions.md`.

| Hypothèse | Où vérifier | Si faux |
| --- | --- | --- |
| PV de la tique ≈ 100 (d'où 10 PV par ver) — **tranché** : 50 PV en 1.0.20, `Worm.Health` reste 10 par décision | `Tick` : `Character.m_health` | — |
| Perception de la tique : vue ≈ 10–12 m, ouïe ≈ 15 m — **tranché** : tique 30 m / demi-angle 90° ; le ver retient 20 m / 360° par décision | `Tick` : `MonsterAI.m_viewRange`, `m_viewAngle`, `m_hearRange` | — |
| Taille : tique ≈ 0,6–0,7 m, fouineur ≈ 1,5 m, soldat ≈ 3 m — **tranché** : tique ≈ 1,2 × 0,9 m, couvain ≈ 1,8 × 1,0 m, fouineur ≈ 4,9 × 2,5 m, soldat ≈ 4,6 × 2,5 m (emprise × hauteur) ; recalibré en `R_{10}` = 0,83 m, `e` = 0,36 (§4.5), puis ramené à `R_{10}` = 0,35 m (décision 172) et enfin à 0,26 m par la refonte visuelle V2-B (décision 185) | colliders / `Renderer.bounds` des prefabs | — |
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
&pool[r] \mathrel{+}= \frac{d}{D_{max}(q)}\,\rho\,C_r(q) \qquad share_r = pool[r] \cdot \frac{k}{n},\ carry_r \mathrel{+}= share_r,\ part_r = \lfloor carry_r \rfloor
\end{aligned}
```

Avec `H_w` = `Worm.Health`, `D` = dégâts effectifs après résistances et blocage, `b` = `Worm.DurabilityPerBite`, `f_block` = facteur de blocage vanilla (1 sans bouclier), `ρ` = `Drops.EquipmentDropRatio`, `C_r(q)` = coût vanilla cumulé de la ressource `r` jusqu'à la qualité `q` (§7.2), `k` = vers tués par ce coup, `n` = compte avant le coup, `ι` = `Engulf.ItemsPerTenWorms`, `carry_r` = reste fractionnaire de la ressource `r` (§7.3), diminué de `part_r` après le drop.

### 20.3 Déroulé de référence (pour calibrer et pour tester)

1. Un joueur en cuirasse rembourrée complète (≈ 4 pièces × 1000) + cape + arme + bouclier + Wisplight + divers (≈ 9 000 points de durabilité au total) entre dans une salle : un nid, 6 dormants, 2 isolés au plus.
2. À 10 m du nid, le groupe s'éveille ; en 4 s, 11 vers forment un amas de 11 (r ≈ 0,86 m) et le chargent à 2,4 m/s.
3. Sans torche, engloutissement : 22 points/s, deux pièces mordues par tick ; le joueur perd en moyenne 1 % de durabilité totale toutes les 4 s — il a plusieurs minutes pour réagir, et chaque coup d'arme de zone retire 3 à 6 vers.
4. Avec 40 vers (deux salles alertées) : 80 points/s, quatre pièces par tick, l'équipement complet est dévoré en ≈ 2 minutes, le Wisplight en 2 s de plus, puis 80 PV/s : la mort suit en 2 s.
5. Avec une torche : l'amas recule à 4 m et tourne autour ; sans bouclier le joueur affronte les fouineurs de la salle avec une arme à une main.
6. Deux salles nettoyées sans détruire les nids : après 20 minutes de présence dans la mine, chaque nid a de nouveau pondu jusqu'à 25 vers endormis (1 toutes les 30 s), sans jamais dépasser 200 vers dans la mine.

### 20.4 Ce qui est volontairement hors périmètre (v1)

Trophée, item nouveau, compatibilité testée avec d'autres mods, événement de raid, vers hors des mines, étoiles, apprivoisement, bestiaire, effets d'écran, et tout asset Unity autre que le bundle de shaders et de compute shaders de la colonie GPU (§13.5).
