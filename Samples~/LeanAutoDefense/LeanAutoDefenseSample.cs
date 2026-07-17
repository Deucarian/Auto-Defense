using System.Collections.Generic;
using Deucarian.Attacks;
using Deucarian.Combat;
using Deucarian.DefenseGames;
using Deucarian.Encounters;
using Deucarian.Projectiles;
using Deucarian.WeaponSystems;
using Deucarian.WorldNavigation;
using Deucarian.WorldSpawning;
using UnityEngine;

namespace Deucarian.AutoDefense.Samples
{
    public static class LeanAutoDefenseSample
    {
        public static readonly DamageTypeId DamageType = new DamageTypeId("damage.lean");
        public static readonly AttackDefinitionId AttackId = new AttackDefinitionId("attack.lean");
        public static readonly ProjectileDefinitionId ProjectileId = new ProjectileDefinitionId("projectile.lean");
        public static readonly WorldSpawnableId EnemySpawnableId = new WorldSpawnableId("enemy.lean");
        public static readonly WorldSpawnableId ProjectileSpawnableId = new WorldSpawnableId("projectile.lean");

        public static AutoDefenseDefinition CreateDefinition()
        {
            var directWeaponId = new WeaponDefinitionId("weapon.direct");
            var projectileWeaponId = new WeaponDefinitionId("weapon.projectile");
            var directMount = new AutoDefenseMountId("mount.direct");
            var projectileMount = new AutoDefenseMountId("mount.projectile");

            return new AutoDefenseDefinition(
                new AutoDefenseObjectiveDefinition(
                    new DefenseObjectiveId("core"),
                    Vector3.zero,
                    24,
                    DamageType,
                    0.45f,
                    3,
                    3),
                AutoDefenseSpawnRingDefinition.FourWay(7f),
                new[] { new AutoDefenseEnemyDefinition(EnemySpawnableId, 8, 2.2f, 3, DamageType, 0.3f) },
                new[]
                {
                    new AutoDefenseMountDefinition(
                        directMount,
                        new Vector3(-1.4f, 0f, 0f),
                        new WeaponSlotId("slot.direct"),
                        directWeaponId),
                    new AutoDefenseMountDefinition(
                        projectileMount,
                        new Vector3(1.4f, 0f, 0f),
                        new WeaponSlotId("slot.projectile"),
                        projectileWeaponId)
                },
                new[]
                {
                    new AutoDefenseWeaponModuleDefinition(
                        directMount,
                        new WeaponDefinition(directWeaponId, WeaponFireMode.DirectAttack, AttackId, 15),
                        Source("direct")),
                    new AutoDefenseWeaponModuleDefinition(
                        projectileMount,
                        new WeaponDefinition(
                            projectileWeaponId,
                            WeaponFireMode.Projectile,
                            AttackId,
                            5,
                            ProjectileId),
                        Source("projectile"))
                });
        }

        public static EncounterDefinition CreateEncounterDefinition()
        {
            string[] channels =
            {
                "perimeter-north",
                "perimeter-east",
                "perimeter-south",
                "perimeter-west"
            };
            var groups = new SpawnGroupDefinition[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                groups[i] = SpawnGroupDefinition.Fixed(
                    new SpawnGroupId("group." + channels[i]),
                    new SpawnableId(EnemySpawnableId.Value),
                    2,
                    1,
                    i * 12,
                    24,
                    new SpawnChannelId(channels[i]));
            }

            return new EncounterDefinition(
                new EncounterId("lean-auto-defense"),
                null,
                new[] { new WaveDefinition(new WaveId("wave.lean"), 0, groups) },
                new[] { ObjectiveDefinition.AllWavesEmitted(new EncounterObjectiveId("all-waves-emitted")) },
                seed: 1337);
        }

        public static CombatCatalog CreateCombatCatalog()
        {
            return new CombatCatalog(new[] { new DamageTypeDefinition(DamageType) });
        }

        public static AttackRuntime CreateAttackRuntime(CombatCatalog catalog, AutoDefenseDefinition definition)
        {
            var runtime = new AttackRuntime(
                catalog,
                new[] { new AttackDefinition(AttackId, 0, DamageType, 8) });
            for (int i = 0; i < definition.WeaponModules.Count; i++)
            {
                runtime.RegisterSource(definition.WeaponModules[i].Source);
            }

            return runtime;
        }

        public static WeaponRuntime CreateWeaponRuntime(AutoDefenseDefinition definition, AttackRuntime attacks)
        {
            var weapons = new List<WeaponDefinition>();
            for (int i = 0; i < definition.WeaponModules.Count; i++)
            {
                weapons.Add(definition.WeaponModules[i].WeaponDefinition);
            }

            return new WeaponRuntime(
                weapons,
                new AttackRuntimeWeaponAttackAdapter(attacks),
                new ProjectileLaunchWeaponAdapter());
        }

        public static ProjectileDefinition CreateProjectileDefinition()
        {
            return new ProjectileDefinition(
                ProjectileId,
                ProjectileSpawnableId,
                DamageType,
                6,
                120,
                8f,
                1);
        }

        private static AttackSourceSnapshot Source(string suffix)
        {
            return new AttackSourceSnapshot(
                new AttackSourceId("source." + suffix),
                new CombatantId("core"));
        }
    }

    public sealed class LeanAutoDefenseSampleController : MonoBehaviour
    {
        private readonly SpawnRequest[] _spawnBuffer = new SpawnRequest[16];
        private AutoDefenseRuntime _runtime;
        private EncounterRuntime _encounter;
        private ProjectileRuntime _projectiles;
        private WorldSpawnService _enemySpawning;
        private WorldSpawnService _projectileSpawning;
        private WorldNavigationService _navigation;
        private WorldNavigationService _projectileNavigation;
        private GameObject _enemyPrefab;
        private GameObject _projectilePrefab;
        private GameObject _root;

        public AutoDefenseRuntime Runtime => _runtime;
        public int SpawnedCount { get; private set; }
        public int KillCount { get; private set; }
        public int ProjectileLaunchCount { get; private set; }
        public int ObjectiveReachCount { get; private set; }
        public bool EncounterCompleted => _runtime != null &&
            _runtime.State == AutoDefenseRuntimeState.Completed;

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            Step(1, Time.deltaTime <= 0f ? 1f / 60f : Time.deltaTime);
        }

        public void Build()
        {
            if (_runtime != null)
            {
                return;
            }

            AutoDefenseDefinition definition = LeanAutoDefenseSample.CreateDefinition();
            CombatCatalog catalog = LeanAutoDefenseSample.CreateCombatCatalog();
            AttackRuntime attacks = LeanAutoDefenseSample.CreateAttackRuntime(catalog, definition);
            WeaponRuntime weapons = LeanAutoDefenseSample.CreateWeaponRuntime(definition, attacks);

            _root = new GameObject("LeanAutoDefense");
            CreatePrimitive(
                "Core",
                PrimitiveType.Cube,
                definition.Objective.Position,
                new Vector3(1.1f, 0.6f, 1.1f),
                Color.cyan);
            for (int i = 0; i < definition.Mounts.Count; i++)
            {
                CreatePrimitive(
                    definition.Mounts[i].Id.Value,
                    PrimitiveType.Cube,
                    definition.Objective.Position + definition.Mounts[i].LocalOffset,
                    new Vector3(0.45f, 0.35f, 0.45f),
                    Color.yellow);
            }

            _enemyPrefab = CreatePrefab("LeanAutoDefenseEnemyPrefab", PrimitiveType.Capsule, Color.red);
            _projectilePrefab = CreatePrefab(
                "LeanAutoDefenseProjectilePrefab",
                PrimitiveType.Sphere,
                Color.magenta);

            var poseResolver = new AutoDefensePerimeterPoseResolver(
                definition.Objective,
                definition.SpawnRing);
            _enemySpawning = new WorldSpawnService(
                new SpawnableCatalog(new[]
                {
                    new SpawnableDefinition(
                        LeanAutoDefenseSample.EnemySpawnableId,
                        new GameObjectPrefabProvider(_enemyPrefab),
                        8,
                        32)
                }),
                poseResolver,
                rootName: "LeanAutoDefenseEnemies");
            _navigation = new WorldNavigationService();
            _encounter = new EncounterRuntime(LeanAutoDefenseSample.CreateEncounterDefinition());
            _runtime = new AutoDefenseRuntime(
                definition,
                _enemySpawning,
                _navigation,
                weapons,
                catalog,
                _encounter,
                poses: poseResolver,
                candidateCapacity: 64);

            var projectilePoseResolver = new ChannelPoseResolver(
                new Dictionary<WorldSpawnChannelId, SpawnPose>
                {
                    {
                        new WorldSpawnChannelId("projectile-origin"),
                        new SpawnPose(definition.Objective.Position, Quaternion.identity)
                    }
                });
            _projectileSpawning = new WorldSpawnService(
                new SpawnableCatalog(new[]
                {
                    new SpawnableDefinition(
                        LeanAutoDefenseSample.ProjectileSpawnableId,
                        new GameObjectPrefabProvider(_projectilePrefab),
                        4,
                        32)
                }),
                projectilePoseResolver,
                rootName: "LeanAutoDefenseProjectiles");
            _projectileNavigation = new WorldNavigationService();
            _projectiles = new ProjectileRuntime(
                catalog,
                new[] { LeanAutoDefenseSample.CreateProjectileDefinition() },
                new WorldSpawnProjectileSpawner(
                    _projectileSpawning,
                    new WorldSpawnChannelId("projectile-origin")),
                new WorldNavigationProjectileNavigator(_projectileNavigation));

            _runtime.Start();
        }

        public void Step(int ticks, float deltaSeconds)
        {
            if (_runtime == null || _runtime.State != AutoDefenseRuntimeState.Running)
            {
                return;
            }

            _encounter.AdvanceTicks(ticks);
            _encounter.DrainSpawnRequests(_spawnBuffer);
            for (int i = 0; i < _spawnBuffer.Length; i++)
            {
                if (_spawnBuffer[i].SpawnableId.IsEmpty)
                {
                    continue;
                }

                AutoDefenseRunResult spawn = _runtime.ConsumeSpawnRequest(_spawnBuffer[i]);
                if (spawn.Succeeded)
                {
                    SpawnedCount += spawn.Spawned;
                }

                _spawnBuffer[i] = default;
            }

            AutoDefenseRunResult result = _runtime.Tick(ticks, deltaSeconds);
            KillCount += result.Killed;
            ObjectiveReachCount += result.ReachedObjective;

            for (int i = 0; i < result.ProjectileLaunches.Count; i++)
            {
                ProjectileLaunchResult launch = _projectiles.Launch(result.ProjectileLaunches[i]);
                if (!launch.Succeeded)
                {
                    continue;
                }

                ProjectileLaunchCount++;
                TryApplySampleProjectileHit();
            }

            _projectiles.Tick(ticks);
            _projectileNavigation.Tick(deltaSeconds);
        }

        private void TryApplySampleProjectileHit()
        {
            AutoDefenseRuntimeSnapshot snapshot = _runtime.CreateSnapshot();
            for (int i = 0; i < snapshot.Enemies.Count; i++)
            {
                AutoDefenseEnemySnapshot enemy = snapshot.Enemies[i];
                if (enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active)
                {
                    continue;
                }

                if (_runtime.TryKillEnemy(enemy.Id))
                {
                    KillCount++;
                    return;
                }
            }
        }

        private GameObject CreatePrefab(string name, PrimitiveType primitiveType, Color color)
        {
            GameObject prefab = GameObject.CreatePrimitive(primitiveType);
            prefab.name = name;
            ApplyColor(prefab, color);
            prefab.SetActive(false);
            return prefab;
        }

        private GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.SetParent(_root.transform, false);
            instance.transform.position = position;
            instance.transform.localScale = scale;
            ApplyColor(instance, color);
            return instance;
        }

        private static void ApplyColor(GameObject instance, Color color)
        {
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            }
        }

        private void OnDestroy()
        {
            _enemySpawning?.Dispose();
            _projectileSpawning?.Dispose();
            if (_enemyPrefab != null)
            {
                Destroy(_enemyPrefab);
            }

            if (_projectilePrefab != null)
            {
                Destroy(_projectilePrefab);
            }

            if (_root != null)
            {
                Destroy(_root);
            }
        }
    }
}
