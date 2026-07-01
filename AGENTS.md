# Deucarian Auto Defense Agent Notes

Package ID: `com.deucarian.auto-defense`
Repository: `Deucarian/Auto-Defense`

Follow the canonical Deucarian governance docs in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md), especially capability ownership and dependency rules.

## Ownership

This package owns:

- Idle Auto Defense and Auto Tower Defense orchestration for central objectives, perimeter spawning, mounted weapons, target selection, auto-fire composition, defense snapshots, and lower-package runtime composition.

Registered capabilities:
- None.

This package must not own:

- Generic defense-game orchestration, attack definition authoring, projectile lifecycle, weapon system internals, combat damage resolution, encounter scheduling internals, progression/reward systems, persistence, UI, monetization, starter scenes, or template-specific balance.

## Dependencies

Allowed dependency shape:

- May depend on lower gameplay simulation packages needed to compose the auto-defense runtime.

Required dependencies and why:

- `com.deucarian.gameplay-foundation`: shared gameplay IDs and deterministic primitives.
- `com.deucarian.encounters`: encounter wave/spawn request inputs.
- `com.deucarian.combat`: objective and attacker combat state.
- `com.deucarian.defense-games`: generic defense-game orchestration base.
- `com.deucarian.world-spawning`: perimeter and projectile/world object spawning.
- `com.deucarian.world-navigation`: spawned enemy movement.
- `com.deucarian.attacks`: mounted attack definitions and target evaluation.
- `com.deucarian.projectiles`: projectile weapon mode integration.
- `com.deucarian.weapon-systems`: weapon slots and fire cadence.

Optional/version-defined dependencies:

- None.

Architecture exceptions:

- The `Samples~/BasicAutoDefense/BasicAutoDefenseSample.cs` sample destroys sample-created prefabs and root objects directly during `OnDestroy`; keep this scoped to the imported sample unless Common becomes a production dependency for framework cleanup.

## Policies

- Keep this package focused on auto-defense composition and orchestration.
- Do not add hard dependencies on Persistence, Progression, Run Upgrades, Idle Progression, UI, Monetization, Game Content Authoring, or template packages.
- Save/load, currencies, upgrade drafting, UI, scene setup, and product-specific balance belong in templates or applications.
- Logging: Do not introduce direct Unity Debug calls.
- Unity object lifetime: Use Common only if production code directly owns transient Unity object cleanup.
- Testing: Test fixture teardown may use Unity `DestroyImmediate` directly.

## Validation

Run the shared validator before committing:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Also run existing repository tests when changing code or asmdefs. Documentation-only updates should still run `git diff --check`.

## Codex Guidance

- Inspect current files before changing anything.
- Work on `develop`; do not edit or merge `main` unless the task is promotion-only.
- Do not edit `Library/PackageCache`.
- Do not guess package versions or dependency versions.
- Do not add package dependencies casually; update asmdefs, `package.json`, `deucarian-package.json`, Package Registry, Package Installer fallback, and Bootstrap fallback together when a dependency is truly required.
- Do not create local copies of shared helpers.
- Keep commits focused and report exactly what changed and what was validated.
