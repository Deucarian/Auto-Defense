# Deucarian Auto Defense

`com.deucarian.auto-defense` is the first Deucarian genre package. It composes Encounters, Defense Games, World Spawning, World Navigation, Weapon Systems, Attacks, Projectiles, and Combat into a reusable central-objective auto-defense loop.

The package owns central objectives, perimeter spawn-channel conventions, auto-defense runtime state, mounted weapon modules, auto-fire orchestration, active enemy targeting candidates, objective contact/leak handling, encounter metric updates, and simple run lifecycle.

It does not own saves, offline rewards, permanent progression, upgrade drafting, UI, analytics, monetization, tower placement, classic tower-defense grids/paths, raw projectile physics, VFX/audio, or ECS.

See `Samples~/BasicAutoDefense` for a small playable starter sample. The sample creates visible primitive GameObjects, a central core, four perimeter spawn channels, a direct mount, a projectile mount, encounter-driven enemy spawns, objective contact damage, and terminal completion/failure smoke coverage.

The sample is intentionally starter-game glue. Reusable systems stay in runtime packages; future full starter-game content belongs in `com.deucarian.template.game.idle-auto-defense`, not in an install-only suite.
