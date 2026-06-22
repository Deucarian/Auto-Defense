using System;
using System.Collections.Generic;
using System.Diagnostics;
using Deucarian.Attacks;
using Deucarian.Combat;
using Deucarian.Encounters;
using Deucarian.Projectiles;
using Deucarian.WeaponSystems;
using Deucarian.WorldNavigation;
using Deucarian.WorldSpawning;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Deucarian.AutoDefense.Tests
{
    public sealed class AutoDefenseTests
    {
        [Test]
        public void ValidAutoDefenseDefinition()
        {
            AutoDefenseDefinition definition = Definition(includeProjectile: true);
            Assert.AreEqual("core", definition.Objective.Id.Value);
            Assert.AreEqual(4, definition.SpawnRing.Channels.Count);
            Assert.AreEqual(2, definition.Mounts.Count);
        }

        [Test]
        public void InvalidObjectiveMountAndChannelAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AutoDefenseObjectiveDefinition(default, Vector3.zero, 100, DamageType));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoDefenseObjectiveDefinition(ObjectiveId, Vector3.zero, 0, DamageType));
            Assert.Throws<ArgumentException>(() => new AutoDefenseMountDefinition(default, Vector3.zero, new WeaponSlotId("slot"), DirectWeaponId));
            Assert.Throws<ArgumentException>(() => new AutoDefenseSpawnChannelDefinition(default, 0));
        }

        [Test]
        public void PerimeterChannelMappingIsDeterministicAndSceneFree()
        {
            var objective = Objective();
            var resolver = new AutoDefensePerimeterPoseResolver(objective, AutoDefenseSpawnRingDefinition.FourWay(10));
            Assert.True(resolver.TryResolvePose(new WorldSpawnChannelId("perimeter-north"), out SpawnPose north));
            Assert.True(resolver.TryResolvePose(new WorldSpawnChannelId("perimeter-east"), out SpawnPose east));
            Assert.False(resolver.TryResolvePose(new WorldSpawnChannelId("perimeter-random"), out _));
            Assert.That(Vector3.Distance(new Vector3(0, 0, 10), north.Position), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(new Vector3(10, 0, 0), east.Position), Is.LessThan(0.001f));
        }

        [Test]
        public void StartAndStopRun()
        {
            using (Harness h = Harness.Create(enemySpeed: 2, includeDirect: false, includeProjectile: false))
            {
                h.Runtime.Start();
                Assert.AreEqual(AutoDefenseRuntimeState.Running, h.Runtime.State);
                h.Runtime.Stop();
                Assert.AreEqual(AutoDefenseRuntimeState.Stopped, h.Runtime.State);
            }
        }

        [Test]
        public void SpawnEnemyFromPerimeterChannelAndMoveTowardObjective()
        {
            using (Harness h = Harness.Create())
            {
                h.Runtime.Start();
                AutoDefenseRunResult spawned = h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north"));
                Assert.True(spawned.Succeeded);
                Assert.AreEqual(1, h.Runtime.ActiveEnemyCount);
                h.Runtime.Tick(1, 0.5f);
                AutoDefenseRuntimeSnapshot snapshot = h.Runtime.CreateSnapshot();
                Assert.That(snapshot.Enemies[0].Position.z, Is.LessThan(5f));
            }
        }

        [Test]
        public void InvalidChannelAndUnknownEnemyFailExplicitly()
        {
            using (Harness h = Harness.Create())
            {
                h.Runtime.Start();
                Assert.AreEqual(AutoDefenseFailureReason.UnknownChannel, h.Runtime.ConsumeSpawnRequest(SpawnRequest("unknown")).FailureReason);
                Assert.AreEqual(AutoDefenseFailureReason.UnknownEnemy, h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north", "missing")).FailureReason);
            }
        }

        [Test]
        public void EnemyReachesObjectiveAndDamagesObjective()
        {
            using (Harness h = Harness.Create(enemyHealth: 100, enemySpeed: 100, directDamage: 1))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north"));
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 1f);
                Assert.AreEqual(1, tick.ReachedObjective);
                Assert.Less(h.Runtime.Objective.Health.CurrentHealth, h.Runtime.Objective.Health.MaximumHealth);
                Assert.AreEqual(0, h.Runtime.ActiveEnemyCount);
            }
        }

        [Test]
        public void ObjectiveFailure()
        {
            using (Harness h = Harness.Create(objectiveHealth: 1, enemySpeed: 100, enemyContactDamage: 2, directDamage: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north"));
                h.Runtime.Tick(1, 1f);
                Assert.AreEqual(AutoDefenseRuntimeState.Failed, h.Runtime.State);
            }
        }

        [Test]
        public void DirectWeaponFiresAndKillsEnemyBeforeReachingObjective()
        {
            using (Harness h = Harness.Create(enemyHealth: 10, directDamage: 20, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north"));
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(1, tick.Killed);
                Assert.AreEqual(0, h.Runtime.ActiveEnemyCount);
            }
        }

        [Test]
        public void ProjectileWeaponFiresLaunchIntent()
        {
            using (Harness h = Harness.Create(includeDirect: false, includeProjectile: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-east"));
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(1, tick.ProjectileLaunches.Count);
                Assert.AreEqual(ProjectileId, tick.ProjectileLaunches[0].DefinitionId);
            }
        }

        [Test]
        public void MultipleMountsFireDeterministically()
        {
            using (Harness h = Harness.Create(enemyHealth: 100, directDamage: 1, includeProjectile: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-east"));
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(2, tick.WeaponFireResult.Intents.Count);
                Assert.AreEqual("slot.direct", tick.WeaponFireResult.Intents[0].SlotId.Value);
                Assert.AreEqual("slot.projectile", tick.WeaponFireResult.Intents[1].SlotId.Value);
            }
        }

        [Test]
        public void NoTargetCandidatesProducesNoDirectFire()
        {
            using (Harness h = Harness.Create())
            {
                h.Runtime.Start();
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(0, tick.WeaponFireResult.FiredCount);
            }
        }

        [Test]
        public void KilledAndReachedEnemiesAreExcludedFromTargeting()
        {
            using (Harness h = Harness.Create(enemyHealth: 1, directDamage: 5, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-east"));
                h.Runtime.Tick(1, 0.1f);
                AutoDefenseRunResult second = h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(0, second.WeaponFireResult.FiredCount);
            }
        }

        [Test]
        public void EncounterMetricsUpdate()
        {
            using (Harness h = Harness.Create(enemyHealth: 1, directDamage: 5, encounter: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-east"));
                h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(1, h.Encounter.GetMetric(AutoDefenseRuntime.SpawnedMetric));
                Assert.AreEqual(1, h.Encounter.GetMetric(AutoDefenseRuntime.KilledMetric));
            }
        }

        [Test]
        public void EncounterCompletesAfterAllWavesEmitAndEnemiesAreCleared()
        {
            using (Harness h = Harness.Create(enemyHealth: 1, directDamage: 5, encounter: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                var buffer = new SpawnRequest[4];
                EncounterDrainResult drained = h.Encounter.DrainSpawnRequests(buffer);
                Assert.AreEqual(1, drained.Written);
                h.Runtime.ConsumeSpawnRequest(buffer[0]);
                h.Runtime.Tick(1, 0.1f);
                Assert.AreEqual(0, h.Runtime.ActiveEnemyCount);
                Assert.AreEqual(EncounterLifecycleState.Completed, h.Encounter.State);
                Assert.AreEqual(AutoDefenseRuntimeState.Completed, h.Runtime.State);
            }
        }

        [Test]
        public void SnapshotCapturesObjectiveAndEnemyState()
        {
            using (Harness h = Harness.Create(enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-west"));
                AutoDefenseRuntimeSnapshot snapshot = h.Runtime.CreateSnapshot();
                Assert.AreEqual(AutoDefenseRuntimeState.Running, snapshot.State);
                Assert.AreEqual(1, snapshot.Enemies.Count);
                Assert.AreEqual(AutoDefenseEnemyLifecycle.Active, snapshot.Enemies[0].Lifecycle);
            }
        }

        [Test]
        public void DonorAndIdleProofsUseSameApi()
        {
            using (Harness h = Harness.Create(includeProjectile: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-north"));
                AutoDefenseRunResult tick = h.Runtime.Tick(60, 0.5f);
                Assert.True(tick.WeaponFireResult.Intents.Count > 0);
            }
        }

        [Test]
        public void ClassicTowerDefenseBoundaryProof()
        {
            string futurePackage = "com.deucarian.tower-defense";
            Assert.AreEqual("com.deucarian.tower-defense", futurePackage);
        }

        [Test]
        public void ProjectilesRuntimeCanConsumeAutoDefenseLaunch()
        {
            using (Harness h = Harness.Create(includeDirect: false, includeProjectile: true, enemySpeed: 0))
            {
                h.Runtime.Start();
                h.Runtime.ConsumeSpawnRequest(SpawnRequest("perimeter-east"));
                AutoDefenseRunResult tick = h.Runtime.Tick(1, 0.1f);
                var spawner = new FakeProjectileSpawner();
                var runtime = new ProjectileRuntime(h.Catalog, new[] { new ProjectileDefinition(ProjectileId, new WorldSpawnableId("projectile"), DamageType, 1, 10, 1) }, spawner, new FakeProjectileNavigator());
                ProjectileLaunchResult launch = runtime.Launch(tick.ProjectileLaunches[0]);
                Assert.True(launch.Succeeded);
                spawner.DestroyCreated();
            }
        }

        [Test]
        public void BenchmarkRecordsAutoFireOrchestration()
        {
            int[] counts = { 1000, 5000, 10000 };
            for (int i = 0; i < counts.Length; i++)
            {
                using (Harness h = Harness.Create(enemyHealth: 100000, directDamage: 1, enemySpeed: 0, maxEnemies: counts[i], includeProjectile: true))
                {
                    h.Runtime.Start();
                    for (int e = 0; e < counts[i]; e++)
                        h.Runtime.ConsumeSpawnRequest(SpawnRequest(e % 2 == 0 ? "perimeter-north" : "perimeter-east", sequence: e + 1));
                    h.Runtime.Tick(1, 0.01f);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    Stopwatch sw = Stopwatch.StartNew();
                    AutoDefenseRunResult result = h.Runtime.Tick(1, 0.01f);
                    sw.Stop();
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.True(result.Succeeded);
                    Debug.Log($"AutoDefense benchmark Unity=6000.3.5f1 operations={counts[i]} ticks=1 enemies={counts[i]} mounts=2 mix=50% direct/50% projectile elapsedMs={sw.Elapsed.TotalMilliseconds:F3} allocationsBytes={allocated} path=Tests/EditMode/AutoDefenseTests.BenchmarkRecordsAutoFireOrchestration");
                }
            }
        }

        private static readonly DamageTypeId DamageType = new DamageTypeId("damage.basic");
        private static readonly DefenseGames.DefenseObjectiveId ObjectiveId = new DefenseGames.DefenseObjectiveId("core");
        private static readonly AttackDefinitionId AttackId = new AttackDefinitionId("attack.basic");
        private static readonly ProjectileDefinitionId ProjectileId = new ProjectileDefinitionId("projectile.basic");
        private static readonly WeaponDefinitionId DirectWeaponId = new WeaponDefinitionId("weapon.direct");
        private static readonly WeaponDefinitionId ProjectileWeaponId = new WeaponDefinitionId("weapon.projectile");

        private static AutoDefenseObjectiveDefinition Objective(double health = 100) => new AutoDefenseObjectiveDefinition(ObjectiveId, Vector3.zero, health, DamageType, 0.25f, -1, 1);

        private static SpawnRequest SpawnRequest(string channel, string spawnable = "enemy.basic", long sequence = 1) => new SpawnRequest(new EncounterId("encounter"), new WaveId("wave"), new SpawnGroupId("group"), new SpawnableId(spawnable), new SpawnChannelId(channel), 0, sequence, 0, 0);

        private static AutoDefenseDefinition Definition(double objectiveHealth = 100, double enemyHealth = 10, float enemySpeed = 0, double enemyContactDamage = 1, bool includeDirect = true, bool includeProjectile = false)
        {
            var mounts = new List<AutoDefenseMountDefinition>();
            var modules = new List<AutoDefenseWeaponModuleDefinition>();
            if (includeDirect)
            {
                mounts.Add(new AutoDefenseMountDefinition(new AutoDefenseMountId("mount.direct"), Vector3.left, new WeaponSlotId("slot.direct"), DirectWeaponId));
                modules.Add(new AutoDefenseWeaponModuleDefinition(new AutoDefenseMountId("mount.direct"), new WeaponDefinition(DirectWeaponId, WeaponFireMode.DirectAttack, AttackId, 1), Source("direct")));
            }
            if (includeProjectile)
            {
                mounts.Add(new AutoDefenseMountDefinition(new AutoDefenseMountId("mount.projectile"), Vector3.right, new WeaponSlotId("slot.projectile"), ProjectileWeaponId));
                modules.Add(new AutoDefenseWeaponModuleDefinition(new AutoDefenseMountId("mount.projectile"), new WeaponDefinition(ProjectileWeaponId, WeaponFireMode.Projectile, AttackId, 1, ProjectileId), Source("projectile")));
            }
            return new AutoDefenseDefinition(Objective(objectiveHealth), AutoDefenseSpawnRingDefinition.FourWay(5), new[] { new AutoDefenseEnemyDefinition(new WorldSpawnableId("enemy.basic"), enemyHealth, enemySpeed, enemyContactDamage, DamageType) }, mounts, modules);
        }

        private static AttackSourceSnapshot Source(string id) => new AttackSourceSnapshot(new AttackSourceId("source." + id), new CombatantId("core"));

        private sealed class Harness : IDisposable
        {
            public AutoDefenseRuntime Runtime;
            public EncounterRuntime Encounter;
            public CombatCatalog Catalog;
            private WorldSpawnService _spawning;
            private GameObject _prefab;

            public static Harness Create(double objectiveHealth = 100, double enemyHealth = 10, float enemySpeed = 0, double enemyContactDamage = 1, double directDamage = 10, bool includeDirect = true, bool includeProjectile = false, bool encounter = false, int maxEnemies = 128)
            {
                var h = new Harness();
                AutoDefenseDefinition definition = Definition(objectiveHealth, enemyHealth, enemySpeed, enemyContactDamage, includeDirect, includeProjectile);
                h.Catalog = new CombatCatalog(new[] { new DamageTypeDefinition(DamageType) });
                var attackDefs = new[] { new AttackDefinition(AttackId, 0, DamageType, directDamage) };
                var attackRuntime = new AttackRuntime(h.Catalog, attackDefs);
                var weaponDefs = new List<WeaponDefinition>();
                for (int i = 0; i < definition.WeaponModules.Count; i++)
                {
                    weaponDefs.Add(definition.WeaponModules[i].WeaponDefinition);
                    attackRuntime.RegisterSource(definition.WeaponModules[i].Source);
                }
                if (weaponDefs.Count == 0) weaponDefs.Add(new WeaponDefinition(new WeaponDefinitionId("weapon.placeholder"), WeaponFireMode.Projectile, AttackId, 1, ProjectileId));
                var weaponRuntime = new WeaponRuntime(weaponDefs, new AttackRuntimeWeaponAttackAdapter(attackRuntime), new ProjectileLaunchWeaponAdapter());
                h._prefab = new GameObject("enemy-prefab");
                h._prefab.SetActive(false);
                var poseResolver = new AutoDefensePerimeterPoseResolver(definition.Objective, definition.SpawnRing);
                var spawnCatalog = new SpawnableCatalog(new[] { new SpawnableDefinition(new WorldSpawnableId("enemy.basic"), new GameObjectPrefabProvider(h._prefab), 0, maxEnemies + 4) });
                h._spawning = new WorldSpawnService(spawnCatalog, poseResolver, rootName: "AutoDefenseTestSpawning");
                h.Encounter = encounter ? CreateEncounter() : null;
                h.Runtime = new AutoDefenseRuntime(definition, h._spawning, new WorldNavigationService(), weaponRuntime, h.Catalog, h.Encounter, poses: poseResolver, candidateCapacity: Math.Max(256, maxEnemies + 4));
                return h;
            }

            public void Dispose()
            {
                _spawning?.Dispose();
                if (_prefab != null) Object.DestroyImmediate(_prefab);
            }

            private static EncounterRuntime CreateEncounter()
            {
                var wave = new WaveDefinition(new WaveId("wave"), 0, new[] { SpawnGroupDefinition.Fixed(new SpawnGroupId("group"), new SpawnableId("enemy.basic"), 1, 1, 0, 1, new SpawnChannelId("perimeter-north")) });
                var definition = new EncounterDefinition(new EncounterId("encounter"), null, new[] { wave }, new[] { ObjectiveDefinition.AllWavesEmitted(new EncounterObjectiveId("all-waves-emitted")) });
                return new EncounterRuntime(definition);
            }
        }

        private sealed class FakeProjectileSpawner : IProjectileSpawner
        {
            private readonly List<GameObject> _created = new List<GameObject>();
            public ProjectileSpawnResult Spawn(ProjectileDefinition definition, ProjectileLaunchRequest request) { var go = new GameObject("projectile"); _created.Add(go); return new ProjectileSpawnResult(true, new ProjectileSpawnHandle(_created.Count), go); }
            public void Despawn(ProjectileSpawnHandle handle, ProjectileExpiryReason reason) { }
            public void DestroyCreated() { for (int i = 0; i < _created.Count; i++) if (_created[i] != null) Object.DestroyImmediate(_created[i]); _created.Clear(); }
        }

        private sealed class FakeProjectileNavigator : IProjectileNavigator
        {
            public ProjectileNavigationResult Start(GameObject instance, ProjectileDefinition definition, ProjectileLaunchRequest request) => new ProjectileNavigationResult(true, new MovementAgentId(1));
            public void Stop(MovementAgentId agentId) { }
            public bool TryGetProgress(MovementAgentId agentId, out MovementProgress progress) { progress = default; return false; }
        }
    }
}
