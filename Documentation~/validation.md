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

Benchmarks:

- Pass 1: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.828 ms, 0 allocated bytes.
- Pass 1: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 9.773 ms, 0 allocated bytes.
- Pass 1: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 22.543 ms, 0 allocated bytes.
- Pass 2: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.745 ms, 0 allocated bytes.
- Pass 2: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 10.104 ms, 0 allocated bytes.
- Pass 2: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 17.474 ms, 0 allocated bytes.

Benchmark path: `Tests/EditMode/AutoDefenseTests.BenchmarkRecordsAutoFireOrchestration`.
