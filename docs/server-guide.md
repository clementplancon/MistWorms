# Guide du serveur dédié

Pour l'administrateur d'un serveur Valheim dédié qui héberge MistWorms (spec §17.2). Il est générique : le mod s'installe comme n'importe quel autre mod, sans hébergeur particulier. Les remarques propres aux hébergeurs sont dans l'annexe facultative en fin de document.

## 1. Avant de commencer

- **Sauvegardez le monde** : téléchargez le dossier `worlds_local` (fichiers `.db` et `.fwl`) ou utilisez la sauvegarde du panel.
- MistWorms enregistre ses colonies et ses nids dans le monde. Gardez la sauvegarde d'avant l'installation si vous voulez pouvoir revenir en arrière (le retrait du mod sur un monde déjà peuplé n'a pas été testé).
- Version testée : Valheim 1.0.20 (série 1.0.x), BepInExPack_Valheim 5.4.2351, Jötunn 2.30.2.

## 2. Installation

1. **BepInExPack_Valheim** : option du panel de l'hébergeur si elle existe ; sinon dépôt du contenu du zip à la racine du serveur par SFTP. Sous Linux, le démarrage doit passer par `start_server_bepinex.sh` (ou la ligne de commande équivalente qui exporte les variables `DOORSTOP_*` et `LD_PRELOAD`), à adapter au panel.
2. Déposez `BepInEx/plugins/Jotunn/` et `BepInEx/plugins/MistWorms/` (le zip du mod contient `plugins/MistWorms/` ; Jötunn est un paquet à part de Thunderstore). Le dossier `MistWorms` contient la DLL, `Localization/` et `assets/` (dont le bundle `mistworms_shaders`) : copiez-le en entier.
3. Démarrez le serveur et lisez `BepInEx/LogOutput.log` : vous devez voir `Loading [Jotunn ...]` puis `Loading [MistWorms 1.0.0]`, et aucune erreur.

Le serveur dédié **ne simule aucune colonie** : ce sont les clients qui en sont propriétaires, le serveur route les messages et applique sa configuration (décision 215).

## 3. Configuration imposée

Le fichier `BepInEx/config/dev.pointvirgule.mistworms.cfg` est créé au premier démarrage. Éditez-le **serveur arrêté** (ou rechargez-le selon le panel) : les valeurs des sections de jeu (`General`, `Worm`, `Resist`, `Mass`, `Engulf`, `Targets`, `Fire`, `Drops`, `Nest`, `Colony`, `Network`) sont synchronisées vers chaque client par Jötunn à la connexion, et un client ne peut pas les modifier. Seule la section `Performance` reste locale à chaque machine. Valeurs par défaut importantes : 25 vers par nid, 200 par mine, 200 par amas.

Exemple, pour désactiver toute la mécanique sans retirer le mod :

```
[General]
Enabled = false
```

## 4. Mot de passe et accès

MistWorms n'ajoute aucun mot de passe : celui du serveur reste celui de Valheim (`-password` dans la ligne de commande de démarrage ou le champ du panel). Les clés de jeu sont marquées « réservées à l'administrateur » pour Jötunn ; le fichier du serveur reste la référence.

## 5. Compatibilité

- Chaque joueur doit installer MistWorms. Un client sans le mod, ou avec une autre version mineure (1.0.x contre 1.1.x), est refusé à la connexion avec un message de Jötunn.
- Côté serveur, la trace se lit dans `BepInEx/LogOutput.log` : « ... is not installed on the client. Server has mandatory mods, cancelling connection » ou « Disconnecting modded client with incompatible version message » (textes exacts à confirmer à la première exécution, décision 224).
- 1.0.0 change le protocole réseau par rapport aux versions de développement : les deux côtés doivent être en 1.0.x.
- Mise à jour : le serveur d'abord, puis les clients. Une mise à jour mineure du mod impose la mise à jour de tous ; un correctif (1.0.x) peut se faire sans l'imposer.
- Aucun autre mod n'a été testé avec MistWorms.

## 6. Dépannage

| Symptôme | Piste |
| --- | --- |
| Pas de ligne `Loading [MistWorms ...]` | BepInEx n'est pas actif : vérifiez le script de démarrage (Linux) et la présence de `BepInEx/core/` |
| Les clients sont tous refusés | Versions différentes du mod ou de Jötunn entre serveur et clients |
| Aucun ver visible | `General.Enabled`, ou mines non encore générées dans cette zone ; les vers n'existent que dans les mines |
| Vers rendus de façon dégradée | `Performance.GpuWorms` (client) : repli CPU si le bundle de shaders est absent ou refusé ; un avertissement est écrit dans le journal du client |

## Annexe (facultative) : serveurs loués

Aucun hébergeur n'est imposé et aucun n'a été testé. Si vous louez un serveur, vérifiez auprès de lui : (a) le système d'exploitation (Linux ou Windows) ; (b) la prise en charge de BepInEx, par une option du panel ou par dépôt SFTP à la racine ; (c) que le script de démarrage est modifiable (sous Linux, `start_server_bepinex.sh` ou la ligne de commande équivalente qui exporte `DOORSTOP_*` et `LD_PRELOAD`) ; (d) où se règlent le mot de passe et la liste des administrateurs (`adminlist.txt`). Sans accès à BepInEx et au dossier `BepInEx/config/`, la configuration imposée par le serveur n'est pas possible.
