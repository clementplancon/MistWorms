# MistWorms

The infested mines of the Mistlands gain a parasite: the **Mist Worm**. One worm is nothing; a few hundred are a problem.

- Worms sleep around small nests in the mine rooms and wake up when a player comes close.
- Awake worms look for each other first: three or more merge into a **Writhing Mass**, a single creature whose health, damage and size are the sum of its worms (up to 200). Its name shows the count, e.g. "Writhing Mass (40)".
- A mass that reaches you **engulfs** you: you are slowed, and its worms bite through your **equipment first** — every item in your inventory, picked at random, your Wisplight last — and only then your health.
- Every worm you kill gives back its share of what the mass has eaten (base recipe materials, plus upgrade materials for higher quality items), rounded down.
- Worms also drop fishing bait, entrails and blood bags now and then. No trophies, no stars, no corpses.
- Nests keep laying worms up to a cap; destroy them and the mine stays clean.
- Worms never leave the mines and only wake up for players, but once awake they eat everything: tamed animals, Seekers, Dvergr and buildings.

## How to fight back

- **Fire**: worms take double fire damage and flee from held torches and campfires.
- **Area weapons**: one hit on a mass kills `damage / worm health` worms, so wide swings and AoE (Frostner, sledges, staffs) are the answer. Pierce is resisted; poison and spirit do nothing.
- **Run**: a sprinting player outpaces any mass. Dodging does not free you from an engulf, killing enough worms or using fire does.
- A raised shield reduces bites and loses durability as usual; armor does not reduce bites.

## Configuration

All values live in `BepInEx/config/dev.pointvirgule.mistworms.cfg`. Gameplay keys are **enforced by the server** (clients cannot change them); only `Performance` stays local.

| Key | Default | Meaning |
| --- | --- | --- |
| `General.Enabled` | true | Turns all spawning off |
| `Worm.Health` | 10 | Health of one worm (mass health = count × this) |
| `Worm.DurabilityPerBite` / `Worm.BiteInterval` | 1 / 0.5 | Gear eaten per worm per bite; seconds between bites |
| `Mass.FormThreshold` / `Mass.MaxCount` / `Mass.DissolveThreshold` | 3 / 200 / 5 | Mass formation, size cap, break-up |
| `Engulf.SlowFactor` | 0.5 | Speed multiplier while engulfed |
| `Targets.AttackPlayers` / `Targets.AttackTamed` / `Targets.AttackCreatures` / `Targets.AttackStructures` | true | What an awake colony eats |
| `Fire.AfraidOfFire` / `Fire.FearRadius` | true / 4 | Fire repels worms (radius in metres) |
| `Nest.MaxAlivePerNest` / `Colony.MaxAlivePerMine` | 25 / 200 | Population caps per nest and per mine |
| `Nest.MaxNestsPerMine` / `Nest.SpawnInterval` | 8 / 30 | Nests per mine; seconds between two eggs of a nest |
| `Performance.RenderDistance` | 60 | Metres, local only |

## Installation

Source code, issues and documentation: https://github.com/clementplancon/MistWorms. This page: https://thunderstore.io/c/valheim/p/kplir/MistWorms/.

**With a mod manager (recommended)**: install `MistWorms` with r2modman or Thunderstore Mod Manager (BepInExPack and Jotunn come with it), then launch the game from the manager. Share your profile code so everyone runs the exact same versions.

**Manual install**: extract BepInExPack_Valheim into the game folder, then copy the `Jotunn` and `MistWorms` folders into `BepInEx/plugins/`. Keep the `MistWorms` folder as is (DLL, `Localization/` and `assets/` together).

## Dedicated server

Any dedicated Valheim server works; no particular host is required.

1. Back up the world first.
2. Install BepInExPack_Valheim on the server (host panel option, or upload over SFTP; Linux servers must start through `start_server_bepinex.sh`).
3. Put `BepInEx/plugins/Jotunn/` and `BepInEx/plugins/MistWorms/` on the server, start it, and check `BepInEx/LogOutput.log` for `Loading [MistWorms ...]`.
4. The mod must be installed on the server **and** on every client. The server's config file (`BepInEx/config/dev.pointvirgule.mistworms.cfg`) is enforced on all clients, except the local `Performance` section.
5. A client without the mod, or with a different minor version, is refused with a Jotunn message. Update the server first, then the clients.

The server does not simulate any colony: clients own them, the server only relays messages and enforces its config.

## Compatibility

Valheim 1.0.x (tested on 1.0.20). Requires BepInExPack_Valheim and Jotunn. No other mod has been tested.

## Credits and license

- Models and textures are original, generated with Blender scripts that ship with the source. Sounds and effects are the game's own.
- Code: MIT License. Assets: CC BY 4.0.

---

## Français

Les mines infestées des Terres brumeuses accueillent un parasite : le **Ver des brumes**, insignifiant seul, redoutable en nombre.

- Les vers dorment autour de petits nids dans les salles des mines et se réveillent à l'approche d'un joueur.
- Éveillés, ils se cherchent : dès trois, ils forment un **Amas grouillant**, une seule créature dont les PV, les dégâts et la taille sont la somme de ses vers (jusqu'à 200) ; son nom affiche le compte, « Amas grouillant (40) ».
- L'amas qui vous atteint vous **engloutit** : vous êtes ralenti et ses vers dévorent **d'abord votre équipement** (tout l'inventaire, pièce au hasard, la Wisplight en dernier), puis vos points de vie.
- Chaque ver tué rend sa part de ce que l'amas a mangé (matériaux de la recette, plus ceux des améliorations), arrondie vers le bas.
- Les vers lâchent parfois des appâts de pêche, des entrailles ou un sac de sang. Pas de trophée, pas d'étoiles, pas de cadavre.
- Les nids pondent jusqu'à un plafond ; détruisez-les et la mine reste propre. Les vers ne sortent jamais des mines.

**Contre-mesures** : le feu (dégâts ×2, ils fuient torches et feux de camp), les armes de zone (un coup tue `dégâts ÷ PV d'un ver` vers), la course. Le perçant est résisté, poison et spirituel sans effet. Le bouclier réduit les morsures ; l'armure non.

**Configuration** : `BepInEx/config/dev.pointvirgule.mistworms.cfg`, imposée par le serveur (sauf la section `Performance`). Voir le tableau ci-dessus.

**Installation** : r2modman ou Thunderstore Mod Manager (partagez le code de profil), ou copie manuelle de `Jotunn` et `MistWorms` dans `BepInEx/plugins/`. **Serveur dédié** (n'importe lequel) : sauvegarder le monde, installer BepInExPack_Valheim, déposer `BepInEx/plugins/Jotunn/` et `BepInEx/plugins/MistWorms/`, vérifier `BepInEx/LogOutput.log`. Le mod doit être sur le serveur et sur chaque client ; la config du serveur s'impose, un client sans le mod est refusé. Mettre à jour le serveur d'abord, puis les clients. Code et documentation : https://github.com/clementplancon/MistWorms

**Compatibilité** : Valheim 1.0.x (testé en 1.0.20), aucun autre mod testé.

**Crédits et licence** : modèles et textures originaux générés avec Blender ; sons du jeu. Code sous licence MIT, assets sous CC BY 4.0.
