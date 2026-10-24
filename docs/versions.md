# Pinned versions

Checked on **2026-10-24** (spec §1.1). `tools/versions.json` is the machine-readable source used by the scripts; keep both in sync when bumping anything.

| Component | Version | Source | Notes |
| --- | --- | --- | --- |
| Valheim (client) | 1.0.20, Steam build 25909357 | https://steamdb.info/app/892970/ | Public branch. Dev copy in `C:\ValheimDev` (robocopy of the Steam install) |
| Valheim Dedicated Server | 1.0.20, Steam build 25909431 | https://steamdb.info/app/896660/ | SteamCMD app 896660 in `C:\ValheimDevServer` |
| Unity runtime | 6000.0.75 | `UnityPlayer.dll` file version of the dev copy | Unity 6: `Graphics.RenderMeshInstanced` is available |
| BepInExPack_Valheim | 5.4.2351 | https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ | Jotunn 2.30.2 itself depends on 5.4.2333; we pin the newer pack |
| Jotunn | 2.30.2 | https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/ | NuGet `JotunnLib` 2.30.2 also exists (https://www.nuget.org/packages/JotunnLib/2.30.2); we reference the Thunderstore `Jotunn.dll` copied into `reference/game-dlls` |
| BepInEx.AssemblyPublicizer.MSBuild | 0.4.3 | https://www.nuget.org/packages/BepInEx.AssemblyPublicizer.MSBuild/0.4.3 | Publicizes `assembly_valheim` and `assembly_utils` |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies/1.0.3 | `net472` targeting without Visual Studio |
| .NET SDK | 8.0.426 (x64) | https://dotnet.microsoft.com/download/dotnet/8.0 | Always `C:\Program Files\dotnet\dotnet.exe`: the x86 `dotnet` on PATH has no SDK. SDK 9.0.103 is also installed |
| Microsoft.NET.Test.Sdk | 17.14.1 | https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1 | Supports .NET Framework 4.6.2+ |
| xunit | 2.9.3 | https://www.nuget.org/packages/xunit/2.9.3 | Last v2 release; v3 is not used |
| xunit.runner.visualstudio | 3.1.5 | https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5 | Requires net472+, runs xunit v2 tests |
| Blender | 4.5.5 LTS | https://www.blender.org/download/lts/4-5/ | Headless only (`--background --python`) |
| Python | 3.11.7 | https://www.python.org/downloads/release/python-3117/ | Spec asks for 3.12; 3.11 accepted, `tools/vh.py` uses the stdlib only |
| ilspycmd | 9.1.0.7988 | https://www.nuget.org/packages/ilspycmd/9.1.0.7988 | 10.x/11.x require .NET 10 and do not install on the 8/9 SDKs |
| 7-Zip | 26.05 | https://www.7-zip.org/ | Not required by the scripts (zips are built with System.IO.Compression) |
| SteamCMD | latest (self-updating) | https://developer.valvesoftware.com/wiki/SteamCMD | `C:\SteamCMD` |

Thunderstore dependency strings in `thunderstore/manifest.json` (validated by `tools/package.ps1` against `tools/versions.json`):

- `denikson-BepInExPack_Valheim-5.4.2351`
- `ValheimModding-Jotunn-2.30.2`

How to re-check: `https://thunderstore.io/api/experimental/package/<namespace>/<name>/` for Thunderstore packages, SteamDB for game builds, `(Get-Item C:\ValheimDev\UnityPlayer.dll).VersionInfo.FileVersion` for Unity.

Note Valheim (2026-10-24) : le jeu a été mis à jour en **1.0.20** (builds Steam lus dans `appmanifest_892970.acf` et `appmanifest_896660.acf`, `Version.GetVersionString(true)` = `1.0.20 e3ee4a778ed0`). La spec a été écrite contre la 1.0.17 ; le dump d'exécution (`vanilla-reference-dump.md`) a été produit sur la 1.0.20 et fait foi. Le décompilage (partie A de `vanilla-reference.md`) a été fait le 2026-10-24 depuis la même installation (mêmes builds Steam 25909357 / 25909431) : c'est donc aussi le code de la 1.0.20. Le chiffre « 1.0.17 » ne vient que du texte de la spec.
