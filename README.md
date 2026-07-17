# Deucarian Auto Defense

`com.deucarian.auto-defense` is the first Deucarian genre package. It composes Encounters, Defense Games, World Spawning, World Navigation, Weapon Systems, Attacks, Projectiles, and Combat into a reusable central-objective auto-defense loop.

The package owns central objectives, perimeter spawn-channel conventions, auto-defense runtime state, mounted weapon modules, auto-fire orchestration, active enemy targeting candidates, objective contact/leak handling, encounter metric updates, and simple run lifecycle.

It does not own saves, offline rewards, permanent progression, upgrade drafting, UI, analytics, monetization, tower placement, classic tower-defense grids/paths, raw projectile physics, VFX/audio, or ECS.

See `Samples~/LeanAutoDefense` for a focused playable example. It creates visible primitive GameObjects, a central core, four perimeter spawn channels, a direct mount, a projectile mount, encounter-driven enemy spawns, and objective contact damage using only this package's declared dependencies.

The full-stack persistence, progression, run-upgrade, and idle-reward example is owned by `com.deucarian.auto-defense-suite`. Playable product content and balance remain in `com.deucarian.template.game.idle-auto-defense`.

## Install

Stable:

```json
"com.deucarian.auto-defense": "https://github.com/Deucarian/Auto-Defense.git#main"
```

Development:

```json
"com.deucarian.auto-defense": "https://github.com/Deucarian/Auto-Defense.git#develop"
```

Use `#main` for stable package consumption and `#develop` when testing active package work.

## When To Use This

Use this package when you need Idle Auto Defense and Auto Tower Defense orchestration for central objectives, perimeter spawning, mounted weapons, targeting, and lower-package composition.

Do not use this package to take ownership of capabilities outside its `AGENTS.md` boundary. Reusable behavior should stay with the package that owns that capability in the Package Registry governance docs.

## Quick Start

1. Install the package through Deucarian Package Installer or Unity Package Manager using the URL above.
2. Let Unity finish resolving packages and compiling assemblies.
3. Import the `Lean Auto Defense` sample if you want a working reference scene.
4. Start from the package README sections above and the public runtime/editor APIs in this repository.

## Integrations

Direct Deucarian package dependencies:

- `com.deucarian.gameplay-foundation`
- `com.deucarian.encounters`
- `com.deucarian.combat`
- `com.deucarian.defense-games`
- `com.deucarian.world-spawning`
- `com.deucarian.world-navigation`
- `com.deucarian.attacks`
- `com.deucarian.projectiles`
- `com.deucarian.weapon-systems`

Install optional companion packages only when their owned capability is needed by production code, samples, or tests.

## Validation

Run the shared package validator from this repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Documentation-only updates should still pass:

```powershell
git diff --check
```

## Troubleshooting

- Package does not resolve: confirm the stable or development Git URL matches the Package Registry entry and that required Deucarian dependencies are installed.
- Unity compile errors after install: let Package Manager finish resolving dependencies, then check asmdef references against `package.json` dependencies.
- Behavior appears to belong in another package: consult `AGENTS.md` and the Package Registry governance docs before moving or duplicating code.

## License

MIT. See `LICENSE.md`.
