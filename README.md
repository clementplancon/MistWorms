# MistWorms

A Valheim mod (BepInEx + Jotunn) that infests the Mistlands mines with the **Mist Worm**. One worm is harmless; a few hundred merge into a **Writhing Mass** that engulfs you and devours your gear before your health. Fire repels them, area weapons kill them by the dozen.

- Thunderstore: https://thunderstore.io/c/valheim/p/kplir/MistWorms/
- Source and issues: https://github.com/clementplancon/MistWorms

## Status

Version **1.0.0**, release validation in progress (offline packaging done; in-game checks and the final package validation remain). It targets Valheim 1.0.x (tested on 1.0.20), BepInExPack_Valheim 5.4.2351 and Jotunn 2.30.2. No other mod has been tested alongside it.

## Install

Like any other Valheim mod: with r2modman or Thunderstore Mod Manager, or by copying the `Jotunn` and `MistWorms` folders into `BepInEx/plugins/`. For a dedicated server, the mod must be on the server and on every client; the server's config is enforced and a client without the mod is refused. See [`thunderstore/README.md`](thunderstore/README.md) (player page) and [`docs/server-guide.md`](docs/server-guide.md) (server administrators, in French).

## Documentation

- [`MistWorms — Spec du mod Valheim (vers parasites des mines infestées).md`](<MistWorms — Spec du mod Valheim (vers parasites des mines infestées).md>): full specification (in French).
- [`docs/decisions.md`](docs/decisions.md): every decision and deviation from the spec, with its reason.
- [`docs/versions.md`](docs/versions.md): pinned versions and build prerequisites.
- [`docs/test-report.md`](docs/test-report.md): in-game test results and the two-player checklist.
- [`docs/vanilla-reference.md`](docs/vanilla-reference.md): vanilla game data the mod relies on.
- [`thunderstore/CHANGELOG.md`](thunderstore/CHANGELOG.md): release notes.

## Build

Windows, PowerShell. Install the prerequisites listed in [`docs/versions.md`](docs/versions.md) (x64 .NET SDK 8, Blender and Python for assets, Unity 6000.0.75f1 only to rebuild the shader bundle), and place the decompiled-against game assemblies in `reference/game-dlls` (not tracked by git).

```
powershell -File tools/build.ps1 -Configuration Profile -NoDeploy   # tests + optimized build with the dev harness
powershell -File tools/build.ps1 -Configuration Release -NoDeploy   # shipped build, no dev harness
powershell -File tools/package.ps1                                   # Thunderstore zip, checked by tools/verify-zip.ps1
```

`Debug` and `Profile` builds include the in-game dev harness (`MW_DEV`); `Release` does not.

## License

Code: [MIT](LICENSE). Generated assets (models, textures): CC BY 4.0.
