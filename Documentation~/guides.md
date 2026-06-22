# Guides

## Runtime Composition

Create an `AutoDefenseDefinition`, `WorldSpawnService`, `WorldNavigationService`, `WeaponRuntime`, optional `EncounterRuntime`, and `CombatCatalog`. Construct `AutoDefenseRuntime`, call `Start`, consume spawn requests, then tick movement and weapons.

## Central Objective

The objective owns generic health/life failure state and a position. Objective damage is resolved through Combat.

## Perimeter Spawn

Use `AutoDefenseSpawnRingDefinition.FourWay` for north/east/south/west channel ids, or author channels manually. `AutoDefensePerimeterPoseResolver` maps channels to positions around the objective without scene objects.

## Mounts And Modules

Mounts are fixed genre slots. Assign weapon definitions to mounts through `AutoDefenseWeaponModuleDefinition`; inventory, upgrades, and research remain outside.

## Targeting

Auto Defense builds candidates from active enemies and scores them by objective progress and distance. Attacks still owns target selection and tie-break behavior.

## Lower Package Integration

Encounters produces `SpawnRequest` values. World Spawning creates objects. World Navigation moves objects to the objective. Weapon Systems emits direct/projectile intents. Combat resolves direct damage. Projectiles receives launch requests.

## Classic Tower Defense Boundary

This package does not own lane paths, grid placement, tower build/sell, lane-exit leaks, or path-progress targeting. Those belong to a future `com.deucarian.tower-defense`.
