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

- `AutoDefense-TestProject-import-final.log`: clean; no compiler/package errors found.

EditMode:

- Pass 1: 19 passed, 0 failed, 0 skipped, 0 inconclusive, duration 15.2111318 seconds.
- Pass 2: 19 passed, 0 failed, 0 skipped, 0 inconclusive, duration 14.718652 seconds.

PlayMode:

- Phase 1P replaced the project-local `AutoDefenseBatchTestRunner` with `com.deucarian.test-automation` consumed through a local file reference in `C:\Repositories\Deucarian\AutoDefense-TestProject`.
- Pass 1 through `Deucarian.TestAutomation.BatchTestRunner.RunPlayMode`: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.923 seconds, `callbackCompleted=True`, exit code 0.
- Pass 2 through `Deucarian.TestAutomation.BatchTestRunner.RunPlayMode`: 1 passed, 0 failed, 0 skipped, 0 inconclusive, duration 1.976 seconds, `callbackCompleted=True`, exit code 0.
- Root cause of the earlier caveat: the old project-local callback did not survive the PlayMode assembly reload and backup-scene transition (`Temp/__Backupscenes/0.backup`), so no durable summary was written.

Benchmarks:

- Pass 1: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.821 ms, 0 allocated bytes.
- Pass 1: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 11.117 ms, 0 allocated bytes.
- Pass 1: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 18.920 ms, 0 allocated bytes.
- Pass 2: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 2.033 ms, 0 allocated bytes.
- Pass 2: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 8.372 ms, 0 allocated bytes.
- Pass 2: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 18.248 ms, 0 allocated bytes.

Benchmark path: `Tests/EditMode/AutoDefenseTests.BenchmarkRecordsAutoFireOrchestration`.
