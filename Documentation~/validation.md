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

- Attempted through `AutoDefenseBatchTestRunner.RunPlayMode`.
- Unity entered a backup-scene PlayMode loop and did not produce the callback summary within the validation window. The process was stopped manually. No compiler errors were present in the PlayMode log.

Benchmarks:

- Pass 1: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 2.714 ms, 0 allocated bytes.
- Pass 1: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 9.008 ms, 0 allocated bytes.
- Pass 1: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 18.682 ms, 0 allocated bytes.
- Pass 2: 1,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 1.759 ms, 0 allocated bytes.
- Pass 2: 5,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 10.668 ms, 0 allocated bytes.
- Pass 2: 10,000 enemies, 1 tick, 2 mounts, 50% direct/50% projectile, 21.997 ms, 0 allocated bytes.

Benchmark path: `Tests/EditMode/AutoDefenseTests.BenchmarkRecordsAutoFireOrchestration`.
