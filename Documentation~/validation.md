# Package Validation Notes

Unity version: `6000.3.5f1`.

Validation project: `C:\Repositories\Deucarian\AutoDefense-TestProject`.

Runtime dependencies:

- `com.deucarian.gameplay-foundation`
- `com.deucarian.encounters`
- `com.deucarian.combat`
- `com.deucarian.defense-games`
- `com.deucarian.world-spawning`
- `com.deucarian.world-navigation`
- `com.deucarian.attacks`
- `com.deucarian.projectiles`
- `com.deucarian.weapon-systems`

No runtime dependency on Progression, Persistence, UI Binding, UI Flow, Core State, or Entities.

## Results

Import:

- `AutoDefense-TestProject-phase1q-import.log`: clean; no compiler/package errors found.

EditMode:

- Pass 1: 20 passed, 0 failed, 0 skipped, 0 inconclusive, duration 14.341 seconds.
- Pass 2: 20 passed, 0 failed, 0 skipped, 0 inconclusive, duration 13.357 seconds.

PlayMode:

- Phase 1P replaced the project-local `AutoDefenseBatchTestRunner` with `com.deucarian.test-automation` consumed through a local file reference in `C:\Repositories\Deucarian\AutoDefense-TestProject`.
- Pass 1 through `Deucarian.TestAutomation.BatchTestRunner.RunPlayMode`: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.956 seconds, `callbackCompleted=True`, exit code 0.
- Pass 2 through `Deucarian.TestAutomation.BatchTestRunner.RunPlayMode`: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.919 seconds, `callbackCompleted=True`, exit code 0.
- Root cause of the earlier caveat: the old project-local callback did not survive the PlayMode assembly reload and backup-scene transition (`Temp/__Backupscenes/0.backup`), so no durable summary was written.

Shared all-packages validation project:

- Path: `C:\Repositories\Deucarian-Validation\AllPackages-TestProject`.
- Import: clean after removing broad package `testables`; only `com.deucarian.test-automation` remains testable so package-owned historical tests do not compile inside the shared project.
- EditMode pass 1: 2 passed, 0 failed, 0 skipped, 0 inconclusive, duration 0.783 seconds. This pass imports `Samples~/BasicAutoDefense`.
- EditMode pass 2: 2 passed, 0 failed, 0 skipped, 0 inconclusive, duration 0.756 seconds.
- PlayMode pass 1: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.382 seconds. Imported Basic Auto Defense sample smoke ran through durable result output.
- PlayMode pass 2: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.300 seconds.

Phase 1R Run Upgrades sample integration:

- `Samples~/BasicAutoDefense` now includes a sample-owned Run Upgrades catalog and adapter.
- The sample adapter translates abstract descriptors into direct-damage support, projectile movement speed, objective healing, and enemy pacing.
- Auto Defense runtime dependencies remain unchanged; `com.deucarian.run-upgrades` is referenced only by the imported sample asmdef.
- Shared EditMode with Run Upgrades package tests: 19 passed, 0 failed, duration 0.803 seconds; repeat 19 passed, 0 failed, duration 0.895 seconds.
- Shared PlayMode upgraded sample pass 1: 1 passed, 0 failed, duration 2.395 seconds.
- Shared PlayMode upgraded sample pass 2: 1 passed, 0 failed, duration 2.501 seconds.

Phase 1S Idle Progression sample integration:

- `Samples~/BasicAutoDefense` now includes a sample-owned Idle Progression offline reward adapter.
- The sample calculates a capped offline reward through `com.deucarian.idle-progression` and applies the resulting Progression `RewardBundle` in sample-owned application glue.
- Auto Defense runtime dependencies remain unchanged; `com.deucarian.run-upgrades`, `com.deucarian.idle-progression`, and `com.deucarian.progression` are referenced only by the imported sample asmdef.
- Shared EditMode with Idle Progression package tests: 33 passed, 0 failed, 0 skipped, 0 inconclusive, duration 0.918 seconds; repeat 33 passed, 0 failed, 0 skipped, 0 inconclusive, duration 0.931 seconds.
- Shared PlayMode sample smoke with offline reward proof: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.291 seconds; repeat 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.351 seconds.

Phase 1T save/progression sample composition:

- `Samples~/BasicAutoDefense/BasicAutoDefenseSaveProgressionComposition.cs` proves sample-owned composition with Persistence, Progression, Run Upgrades, and Idle Progression.
- Covered profile DTO save/load, run resume DTO save/load, settings DTO save/load, Progression run reward application, run upgrade snapshot/restore, offline reward from a saved timestamp, missing-save default load, corrupted-primary backup recovery, and a profile migration fixture.
- Auto Defense runtime dependencies remain unchanged. Persistence, Progression, Run Upgrades, and Idle Progression are referenced only by the imported sample asmdef.
- Import: `Logs\AllPackages-TestProject-phase1t-import-2.log`, exit code 0.
- Shared EditMode pass 1: 34 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.106 seconds; pass 2: 34 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.035 seconds.
- Shared PlayMode pass 1: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.355 seconds; pass 2: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 2.323 seconds.

Benchmarks:

- Pass 1: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.828 ms, 0 allocated bytes.
- Pass 1: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 9.773 ms, 0 allocated bytes.
- Pass 1: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 22.543 ms, 0 allocated bytes.
- Pass 2: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.745 ms, 0 allocated bytes.
- Pass 2: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 10.104 ms, 0 allocated bytes.
- Pass 2: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 17.474 ms, 0 allocated bytes.

Benchmark path: `Tests/EditMode/AutoDefenseTests.BenchmarkRecordsAutoFireOrchestration`.
