# Changelog

## 1.0.0

First public release.

- **Mist Worms** live in the infested mines of the Mistlands: lone worms in the rooms, and sleeping nests that lay new worms up to 25 per nest and 200 per mine. Worms wake up when a player comes close and calm down again when nobody is around.
- **Writhing Mass**: awake worms gather into one creature (three or more, up to 200) whose health, damage and size are the sum of its worms; its name shows the count.
- **Engulf**: a mass that reaches you slows you down and eats your equipment before your health. Every worm you kill gives back its share of what the mass ate.
- **Combat**: worms take double fire damage and flee from torches and campfires; area weapons kill many worms per hit; pierce is resisted; poison and spirit do nothing.
- **Targets**: awake colonies also attack tamed animals, other creatures and buildings (each can be switched off in the config).
- **Drops**: fishing bait, entrails and blood bags. No trophies, no stars, no corpses.
- **Multiplayer**: a colony is simulated by one client and replicated to the others; a dedicated server routes and applies its own configuration to everyone. Gameplay keys are server-enforced.
- **Performance**: worms are drawn on the GPU with a CPU fallback (`Performance.GpuWorms`), simulation steps are spread over frames, and the worm count per mine is capped.
- **Localization**: English and French.
- Persistence: colonies, nests and masses are saved with the world.

Compatibility note: the mod is registered with `VersionStrictness.Minor`. The network protocol is new in 1.0.0, so every player and the server must run the same 1.0.x line, and a client or server on a different minor version is refused at connection. Update the server first, then the clients.
