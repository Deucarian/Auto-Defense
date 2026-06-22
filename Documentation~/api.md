# API Documentation

- `AutoDefenseRuntime`: central runtime for start/stop, spawn consumption, movement ticks, weapon ticks, objective contact, damage resolution, metrics, and snapshots.
- `AutoDefenseDefinition`: authored central objective, spawn channels, enemies, mounts, and weapon module definitions.
- `AutoDefenseObjectiveDefinition`: objective id, position, contact radius, health, lives, contact damage, and damage type.
- `AutoDefenseMountId`: stable mount id.
- `AutoDefenseMountDefinition`: mount id, local offset, assigned weapon slot/definition, enabled flag.
- `AutoDefenseSpawnRingDefinition`: deterministic ring channel set.
- `AutoDefenseSpawnChannelDefinition`: channel id and spawn angle.
- `AutoDefenseEnemyDefinition`: spawnable id, health, speed, contact damage, target radius, and combat ids.
- `AutoDefenseWeaponModuleDefinition`: weapon definition and attack source data for a mount.
- `AutoDefenseRuntimeSnapshot`: objective and active enemy snapshot.
- `AutoDefenseRunResult`: tick/spawn/action result summary.
- `AutoDefenseFailureReason`: explicit failure reasons.
- `IAutoDefenseContentResolver`: maps spawnables to enemy definitions.
- `IAutoDefensePoseResolver`: resolves channel poses without scene objects.
- `IAutoDefenseTargetProvider`: builds candidate lists from active enemy state.
