using System.Collections.Generic;
using Deucarian.Attacks;
using Deucarian.Combat;
using Deucarian.DefenseGames;
using Deucarian.Encounters;
using Deucarian.IdleProgression;
using Deucarian.Projectiles;
using Deucarian.Progression;
using Deucarian.RunUpgrades;
using Deucarian.WeaponSystems;
using Deucarian.WorldNavigation;
using Deucarian.WorldSpawning;
using UnityEngine;

namespace Deucarian.AutoDefense.Samples
{
    public static class BasicAutoDefenseSample
    {
        public static readonly DamageTypeId DamageType = new DamageTypeId("damage.basic");
        public static readonly AttackDefinitionId AttackId = new AttackDefinitionId("attack.basic");
        public static readonly ProjectileDefinitionId ProjectileId = new ProjectileDefinitionId("projectile.basic");
        public static readonly WorldSpawnableId EnemySpawnableId = new WorldSpawnableId("enemy.basic");
        public static readonly WorldSpawnableId ProjectileSpawnableId = new WorldSpawnableId("projectile.basic");

        public static AutoDefenseDefinition CreateDefinition()
        {
            var directWeaponId = new WeaponDefinitionId("weapon.direct");
            var projectileWeaponId = new WeaponDefinitionId("weapon.projectile");
            var directMount = new AutoDefenseMountId("mount.direct");
            var projectileMount = new AutoDefenseMountId("mount.projectile");
            return new AutoDefenseDefinition(
                new AutoDefenseObjectiveDefinition(new DefenseObjectiveId("core"), Vector3.zero, 24, DamageType, 0.45f, 3, 3),
                AutoDefenseSpawnRingDefinition.FourWay(7f),
                new[] { new AutoDefenseEnemyDefinition(EnemySpawnableId, 8, 2.2f, 3, DamageType, 0.3f) },
                new[]
                {
                    new AutoDefenseMountDefinition(directMount, new Vector3(-1.4f, 0f, 0f), new WeaponSlotId("slot.direct"), directWeaponId),
                    new AutoDefenseMountDefinition(projectileMount, new Vector3(1.4f, 0f, 0f), new WeaponSlotId("slot.projectile"), projectileWeaponId)
                },
                new[]
                {
                    new AutoDefenseWeaponModuleDefinition(directMount, new WeaponDefinition(directWeaponId, WeaponFireMode.DirectAttack, AttackId, 15), Source("direct")),
                    new AutoDefenseWeaponModuleDefinition(projectileMount, new WeaponDefinition(projectileWeaponId, WeaponFireMode.Projectile, AttackId, 5, ProjectileId), Source("projectile"))
                });
        }

        public static EncounterDefinition CreateEncounterDefinition()
        {
            var channels = new[]
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
                new EncounterId("basic-auto-defense"),
                null,
                new[] { new WaveDefinition(new WaveId("wave.basic"), 0, groups) },
                new[] { ObjectiveDefinition.AllWavesEmitted(new EncounterObjectiveId("all-waves-emitted")) },
                seed: 1337);
        }

        public static CombatCatalog CreateCombatCatalog()
        {
            return new CombatCatalog(new[] { new DamageTypeDefinition(DamageType) });
        }

        public static AttackRuntime CreateAttackRuntime(CombatCatalog catalog, AutoDefenseDefinition definition)
        {
            var runtime = new AttackRuntime(catalog, new[] { new AttackDefinition(AttackId, 0, DamageType, 8) });
            for (int i = 0; i < definition.WeaponModules.Count; i++)
                runtime.RegisterSource(definition.WeaponModules[i].Source);
            return runtime;
        }

        public static WeaponRuntime CreateWeaponRuntime(AutoDefenseDefinition definition, AttackRuntime attacks)
        {
            var weapons = new List<WeaponDefinition>();
            for (int i = 0; i < definition.WeaponModules.Count; i++)
                weapons.Add(definition.WeaponModules[i].WeaponDefinition);
            return new WeaponRuntime(weapons, new AttackRuntimeWeaponAttackAdapter(attacks), new ProjectileLaunchWeaponAdapter());
        }

        public static ProjectileDefinition CreateProjectileDefinition()
        {
            return new ProjectileDefinition(ProjectileId, ProjectileSpawnableId, DamageType, 6, 120, 8f, 1);
        }

        public static RunUpgradeCatalog CreateRunUpgradeCatalog()
        {
            return new RunUpgradeCatalog(new[]
            {
                Upgrade("upgrade.direct.damage", "sample.direct.damage_bonus", "weapon.direct", 1),
                Upgrade("upgrade.projectile.speed", "sample.projectile.speed_multiplier", "projectile.basic", 0.5),
                Upgrade("upgrade.objective.repair", "sample.objective.heal", "objective.core", 2),
                Upgrade("upgrade.enemy.pacing", "sample.enemy.spawn_delay_ticks", "encounter.basic", 6)
            });
        }

        public static IdleProgressionDefinition CreateOfflineProgressionDefinition()
        {
            return new IdleProgressionDefinition(
                System.TimeSpan.FromHours(8),
                new[] { new IdleProductionRate(new CurrencyId("currency.sample.credits"), 0.25d) },
                new[] { new IdleCycleReward(new CurrencyId("currency.sample.parts"), new ProgressionAmount(1), System.TimeSpan.FromMinutes(5)) });
        }

        public static ProgressionCatalog CreateProgressionCatalog()
        {
            return new ProgressionCatalog(new[]
            {
                new CurrencyDefinition(new CurrencyId("currency.sample.credits"), new ProgressionAmount(100_000)),
                new CurrencyDefinition(new CurrencyId("currency.sample.parts"), new ProgressionAmount(10_000))
            });
        }

        private static RunUpgradeDefinition Upgrade(string id, string effect, string target, double amount)
        {
            return new RunUpgradeDefinition(
                new RunUpgradeId(id),
                RunUpgradeRarity.Common,
                1,
                3,
                new[] { new RunUpgradeEffectDescriptor(new RunUpgradeEffectId(effect), new RunUpgradeTargetId(target), amount) });
        }

        private static AttackSourceSnapshot Source(string suffix)
        {
            return new AttackSourceSnapshot(new AttackSourceId("source." + suffix), new CombatantId("core"));
        }
    }

    public sealed class BasicAutoDefenseSampleController : MonoBehaviour
    {
        private readonly SpawnRequest[] _spawnBuffer = new SpawnRequest[16];
        private AutoDefenseRuntime _runtime;
        private EncounterRuntime _encounter;
        private ProjectileRuntime _projectiles;
        private WorldSpawnService _enemySpawning;
        private WorldSpawnService _projectileSpawning;
        private WorldNavigationService _navigation;
        private WorldNavigationService _projectileNavigation;
        private RunUpgradeCatalog _upgradeCatalog;
        private RunUpgradeState _upgradeState;
        private RunUpgradeDraft _currentDraft;
        private ProgressionCatalog _progressionCatalog;
        private ProgressionState _progressionState;
        private IdleProgressionDefinition _offlineDefinition;
        private GameObject _enemyPrefab;
        private GameObject _projectilePrefab;
        private GameObject _root;

        public AutoDefenseRuntime Runtime => _runtime;
        public int SpawnedCount { get; private set; }
        public int DirectOrCombatKillCount { get; private set; }
        public int ProjectileLaunchCount { get; private set; }
        public int ProjectileAdapterKillCount { get; private set; }
        public int ObjectiveReachCount { get; private set; }
        public int ObjectiveDamageEvents { get; private set; }
        public int DraftCount { get; private set; }
        public int SelectedUpgradeCount { get; private set; }
        public double DirectDamageBonus { get; private set; }
        public double ProjectileSpeedMultiplier { get; private set; } = 1d;
        public int EnemySpawnDelayTicks { get; private set; }
        public long OfflineRewardCredits { get; private set; }
        public long OfflineRewardParts { get; private set; }
        public IdleProgressionResultCode LastOfflineRewardCode { get; private set; } = IdleProgressionResultCode.NoElapsedTime;
        public bool EncounterCompleted => _runtime != null && _runtime.State == AutoDefenseRuntimeState.Completed;
        public bool EncounterFailed => _runtime != null && _runtime.State == AutoDefenseRuntimeState.Failed;

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
            if (_runtime != null) return;
            AutoDefenseDefinition definition = BasicAutoDefenseSample.CreateDefinition();
            CombatCatalog catalog = BasicAutoDefenseSample.CreateCombatCatalog();
            AttackRuntime attacks = BasicAutoDefenseSample.CreateAttackRuntime(catalog, definition);
            WeaponRuntime weapons = BasicAutoDefenseSample.CreateWeaponRuntime(definition, attacks);

            _root = new GameObject("BasicAutoDefense");
            CreatePrimitive("Core", PrimitiveType.Cube, definition.Objective.Position, new Vector3(1.1f, 0.6f, 1.1f), Color.cyan);
            for (int i = 0; i < definition.Mounts.Count; i++)
                CreatePrimitive(definition.Mounts[i].Id.Value, PrimitiveType.Cube, definition.Objective.Position + definition.Mounts[i].LocalOffset, new Vector3(0.45f, 0.35f, 0.45f), Color.yellow);

            _enemyPrefab = CreatePrefab("BasicAutoDefenseEnemyPrefab", PrimitiveType.Capsule, Color.red);
            _projectilePrefab = CreatePrefab("BasicAutoDefenseProjectilePrefab", PrimitiveType.Sphere, Color.magenta);

            var poseResolver = new AutoDefensePerimeterPoseResolver(definition.Objective, definition.SpawnRing);
            _enemySpawning = new WorldSpawnService(
                new SpawnableCatalog(new[] { new SpawnableDefinition(BasicAutoDefenseSample.EnemySpawnableId, new GameObjectPrefabProvider(_enemyPrefab), 8, 32) }),
                poseResolver,
                rootName: "BasicAutoDefenseEnemies");
            _navigation = new WorldNavigationService();
            _encounter = new EncounterRuntime(BasicAutoDefenseSample.CreateEncounterDefinition());
            _runtime = new AutoDefenseRuntime(definition, _enemySpawning, _navigation, weapons, catalog, _encounter, poses: poseResolver, candidateCapacity: 64);

            var projectilePoseResolver = new ChannelPoseResolver(new Dictionary<WorldSpawnChannelId, SpawnPose>
            {
                { new WorldSpawnChannelId("projectile-origin"), new SpawnPose(definition.Objective.Position, Quaternion.identity) }
            });
            _projectileSpawning = new WorldSpawnService(
                new SpawnableCatalog(new[] { new SpawnableDefinition(BasicAutoDefenseSample.ProjectileSpawnableId, new GameObjectPrefabProvider(_projectilePrefab), 4, 32) }),
                projectilePoseResolver,
                rootName: "BasicAutoDefenseProjectiles");
            _projectileNavigation = new WorldNavigationService();
            _projectiles = new ProjectileRuntime(
                catalog,
                new[] { BasicAutoDefenseSample.CreateProjectileDefinition() },
                new WorldSpawnProjectileSpawner(_projectileSpawning, new WorldSpawnChannelId("projectile-origin")),
                new WorldNavigationProjectileNavigator(_projectileNavigation));
            _upgradeCatalog = BasicAutoDefenseSample.CreateRunUpgradeCatalog();
            _upgradeState = new RunUpgradeState();
            _progressionCatalog = BasicAutoDefenseSample.CreateProgressionCatalog();
            _progressionState = new ProgressionState();
            _offlineDefinition = BasicAutoDefenseSample.CreateOfflineProgressionDefinition();

            _runtime.Start();
        }

        public IdleProgressionResult SimulateOfflineReward(System.DateTimeOffset lastSeenUtc, System.DateTimeOffset nowUtc)
        {
            if (_runtime == null) Build();
            IdleProgressionResult result = IdleProgressionCalculator.Calculate(lastSeenUtc, nowUtc, _offlineDefinition);
            LastOfflineRewardCode = result.Code;
            if (result.Reward.CurrencyLines.Count > 0)
            {
                _progressionState.ApplyReward(_progressionCatalog, new ProgressionOperationId("sample.offline." + nowUtc.UtcTicks), result.Reward);
            }

            OfflineRewardCredits = _progressionState.GetBalance(new CurrencyId("currency.sample.credits")).Value;
            OfflineRewardParts = _progressionState.GetBalance(new CurrencyId("currency.sample.parts")).Value;
            return result;
        }

        public void Step(int ticks, float deltaSeconds)
        {
            if (_runtime == null || _runtime.State != AutoDefenseRuntimeState.Running) return;
            DraftAndApplyUpgradeIfDue(ticks);
            _encounter.AdvanceTicks(EnemySpawnDelayTicks);
            _encounter.DrainSpawnRequests(_spawnBuffer);
            for (int i = 0; i < _spawnBuffer.Length; i++)
            {
                if (_spawnBuffer[i].SpawnableId.IsEmpty) continue;
                AutoDefenseRunResult spawn = _runtime.ConsumeSpawnRequest(_spawnBuffer[i]);
                if (spawn.Succeeded) SpawnedCount += spawn.Spawned;
                _spawnBuffer[i] = default;
            }

            AutoDefenseRunResult result = _runtime.Tick(ticks, deltaSeconds);
            DirectOrCombatKillCount += result.Killed;
            ObjectiveReachCount += result.ReachedObjective;
            if (result.ReachedObjective > 0) ObjectiveDamageEvents += result.ReachedObjective;

            for (int i = 0; i < result.ProjectileLaunches.Count; i++)
            {
                ProjectileLaunchResult launch = _projectiles.Launch(result.ProjectileLaunches[i]);
                if (!launch.Succeeded) continue;
                ProjectileLaunchCount++;
                TryApplySampleProjectileHit();
            }

            _projectiles.Tick(ticks);
            _projectileNavigation.Tick((float)(deltaSeconds * ProjectileSpeedMultiplier));
            ApplyDirectDamageBonusIfReady();
        }

        private void DraftAndApplyUpgradeIfDue(int ticks)
        {
            if (_upgradeCatalog == null) return;
            DraftCount += ticks;
            if (DraftCount % 30 != 0) return;
            _currentDraft = RunUpgradeDraftService.Generate(_upgradeCatalog, _upgradeState, new RunUpgradeDraftRequest(3, 20260622, DraftCount / 30));
            if (_currentDraft.Choices.Count == 0) return;
            RunUpgradeSelectionResult selected = _upgradeState.Select(_upgradeCatalog, _currentDraft.Choices[0].Id);
            if (!selected.Succeeded) return;
            SelectedUpgradeCount++;
            ApplyUpgrade(_currentDraft.Choices[0]);
        }

        private void ApplyUpgrade(RunUpgradeDefinition upgrade)
        {
            for (int i = 0; i < upgrade.Effects.Count; i++)
            {
                RunUpgradeEffectDescriptor effect = upgrade.Effects[i];
                if (effect.EffectId.Value == "sample.direct.damage_bonus") DirectDamageBonus += effect.Amount;
                else if (effect.EffectId.Value == "sample.projectile.speed_multiplier") ProjectileSpeedMultiplier += effect.Amount;
                else if (effect.EffectId.Value == "sample.objective.heal") _runtime.Objective.Health.Heal(effect.Amount);
                else if (effect.EffectId.Value == "sample.enemy.spawn_delay_ticks") EnemySpawnDelayTicks += (int)effect.Amount;
            }
        }

        private void ApplyDirectDamageBonusIfReady()
        {
            if (DirectDamageBonus <= 0d) return;
            AutoDefenseRuntimeSnapshot snapshot = _runtime.CreateSnapshot();
            for (int i = 0; i < snapshot.Enemies.Count; i++)
            {
                AutoDefenseEnemySnapshot enemy = snapshot.Enemies[i];
                if (enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active) continue;
                if (enemy.Health <= DirectDamageBonus && _runtime.TryKillEnemy(enemy.Id))
                {
                    DirectOrCombatKillCount++;
                    return;
                }
            }
        }

        private void TryApplySampleProjectileHit()
        {
            AutoDefenseRuntimeSnapshot snapshot = _runtime.CreateSnapshot();
            for (int i = 0; i < snapshot.Enemies.Count; i++)
            {
                AutoDefenseEnemySnapshot enemy = snapshot.Enemies[i];
                if (enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active) continue;
                if (_runtime.TryKillEnemy(enemy.Id))
                {
                    ProjectileAdapterKillCount++;
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

        private GameObject CreatePrimitive(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
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
            if (renderer != null) renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
        }

        private void OnDestroy()
        {
            _enemySpawning?.Dispose();
            _projectileSpawning?.Dispose();
            if (_enemyPrefab != null) Destroy(_enemyPrefab);
            if (_projectilePrefab != null) Destroy(_projectilePrefab);
            if (_root != null) Destroy(_root);
        }
    }
}
