using System;
using System.Collections.Generic;
using Deucarian.Attacks;
using Deucarian.Combat;
using Deucarian.DefenseGames;
using Deucarian.Encounters;
using Deucarian.GameplayFoundation;
using Deucarian.Projectiles;
using Deucarian.WeaponSystems;
using Deucarian.WorldNavigation;
using Deucarian.WorldSpawning;
using UnityEngine;

namespace Deucarian.AutoDefense
{
    /// <summary>Stable id for a fixed auto-defense mount.</summary>
    public readonly struct AutoDefenseMountId : IEquatable<AutoDefenseMountId>, IComparable<AutoDefenseMountId>
    {
        private readonly ContentId _value;
        public AutoDefenseMountId(string value) { _value = new ContentId(value); }
        public string Value => _value.Value;
        public bool IsEmpty => _value.IsEmpty;
        public bool Equals(AutoDefenseMountId other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is AutoDefenseMountId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public int CompareTo(AutoDefenseMountId other) => _value.CompareTo(other._value);
        public override string ToString() => Value;
    }

    public enum AutoDefenseRuntimeState { Created = 0, Running = 1, Stopped = 2, Completed = 3, Failed = 4 }
    public enum AutoDefenseEnemyLifecycle { Active = 0, Killed = 1, ReachedObjective = 2, Despawned = 3 }
    public enum AutoDefenseFailureReason { None = 0, InvalidDefinition = 1, NotRunning = 2, UnknownChannel = 3, UnknownEnemy = 4, SpawnFailed = 5, NavigationFailed = 6, UnknownMount = 7, InvalidWeapon = 8, InvalidInput = 9 }

    /// <summary>Central objective content for an auto-defense run.</summary>
    public sealed class AutoDefenseObjectiveDefinition
    {
        public AutoDefenseObjectiveDefinition(DefenseObjectiveId id, Vector3 position, double maximumHealth, DamageTypeId damageTypeId, float contactRadius = 0.25f, int lives = -1, double contactDamage = 1d)
        {
            if (id.IsEmpty) throw new ArgumentException("Objective id cannot be empty.", nameof(id));
            if (maximumHealth <= 0d) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            if (damageTypeId.IsEmpty) throw new ArgumentException("Damage type id cannot be empty.", nameof(damageTypeId));
            if (contactRadius < 0f || float.IsNaN(contactRadius) || float.IsInfinity(contactRadius)) throw new ArgumentOutOfRangeException(nameof(contactRadius));
            CombatNumbers.RequireNonNegative(contactDamage, nameof(contactDamage));
            if (lives < -1) throw new ArgumentOutOfRangeException(nameof(lives));
            Id = id; Position = position; MaximumHealth = maximumHealth; DamageTypeId = damageTypeId; ContactRadius = contactRadius; Lives = lives; ContactDamage = contactDamage;
        }

        public DefenseObjectiveId Id { get; }
        public Vector3 Position { get; }
        public double MaximumHealth { get; }
        public DamageTypeId DamageTypeId { get; }
        public float ContactRadius { get; }
        public int Lives { get; }
        public double ContactDamage { get; }
    }

    /// <summary>Mutable state for the central objective.</summary>
    public sealed class AutoDefenseObjectiveState
    {
        public AutoDefenseObjectiveState(AutoDefenseObjectiveDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Health = new HealthState(new CombatantId("auto-defense.objective/" + definition.Id.Value), definition.MaximumHealth, definition.MaximumHealth);
            LivesRemaining = definition.Lives;
        }

        public AutoDefenseObjectiveDefinition Definition { get; }
        public HealthState Health { get; }
        public int LivesRemaining { get; private set; }
        public bool Failed { get; private set; }
        public void ApplyLeak() { if (LivesRemaining >= 0) LivesRemaining = Math.Max(0, LivesRemaining - 1); if (LivesRemaining == 0 || Health.LifeState == LifeState.Dead) Failed = true; }
        public void MarkFailed() { Failed = true; }
    }

    /// <summary>Authored perimeter spawn channel.</summary>
    public sealed class AutoDefenseSpawnChannelDefinition
    {
        public AutoDefenseSpawnChannelDefinition(WorldSpawnChannelId id, float angleDegrees)
        {
            if (id.IsEmpty) throw new ArgumentException("Channel id cannot be empty.", nameof(id));
            if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
            Id = id; AngleDegrees = angleDegrees;
        }

        public WorldSpawnChannelId Id { get; }
        public float AngleDegrees { get; }
    }

    /// <summary>Reusable deterministic perimeter channel set.</summary>
    public sealed class AutoDefenseSpawnRingDefinition
    {
        public AutoDefenseSpawnRingDefinition(float radius, IReadOnlyList<AutoDefenseSpawnChannelDefinition> channels)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
            if (channels == null || channels.Count == 0) throw new ArgumentException("At least one channel is required.", nameof(channels));
            Radius = radius; Channels = Copy(channels);
        }

        public float Radius { get; }
        public IReadOnlyList<AutoDefenseSpawnChannelDefinition> Channels { get; }
        public static AutoDefenseSpawnRingDefinition FourWay(float radius) => new AutoDefenseSpawnRingDefinition(radius, new[]
        {
            new AutoDefenseSpawnChannelDefinition(new WorldSpawnChannelId("perimeter-north"), 0f),
            new AutoDefenseSpawnChannelDefinition(new WorldSpawnChannelId("perimeter-east"), 90f),
            new AutoDefenseSpawnChannelDefinition(new WorldSpawnChannelId("perimeter-south"), 180f),
            new AutoDefenseSpawnChannelDefinition(new WorldSpawnChannelId("perimeter-west"), 270f)
        });
        private static AutoDefenseSpawnChannelDefinition[] Copy(IReadOnlyList<AutoDefenseSpawnChannelDefinition> source) { var copy = new AutoDefenseSpawnChannelDefinition[source.Count]; var seen = new HashSet<WorldSpawnChannelId>(); for (int i = 0; i < source.Count; i++) { copy[i] = source[i] ?? throw new ArgumentException("Channel cannot be null."); if (!seen.Add(copy[i].Id)) throw new ArgumentException("Duplicate channel: " + copy[i].Id); } return copy; }
    }

    /// <summary>Enemy content resolved from Encounter spawnables.</summary>
    public sealed class AutoDefenseEnemyDefinition
    {
        public AutoDefenseEnemyDefinition(WorldSpawnableId spawnableId, double maximumHealth, float speed, double contactDamage, DamageTypeId damageTypeId, float collisionRadius = 0.25f)
        {
            if (spawnableId.IsEmpty) throw new ArgumentException("Spawnable id cannot be empty.", nameof(spawnableId));
            if (maximumHealth <= 0d) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            if (speed < 0f || float.IsNaN(speed) || float.IsInfinity(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            CombatNumbers.RequireNonNegative(contactDamage, nameof(contactDamage));
            if (damageTypeId.IsEmpty) throw new ArgumentException("Damage type id cannot be empty.", nameof(damageTypeId));
            if (collisionRadius < 0f) throw new ArgumentOutOfRangeException(nameof(collisionRadius));
            SpawnableId = spawnableId; MaximumHealth = maximumHealth; Speed = speed; ContactDamage = contactDamage; DamageTypeId = damageTypeId; CollisionRadius = collisionRadius;
        }

        public WorldSpawnableId SpawnableId { get; }
        public double MaximumHealth { get; }
        public float Speed { get; }
        public double ContactDamage { get; }
        public DamageTypeId DamageTypeId { get; }
        public float CollisionRadius { get; }
    }

    /// <summary>Fixed mount definition. Inventory and upgrades are intentionally outside.</summary>
    public sealed class AutoDefenseMountDefinition
    {
        public AutoDefenseMountDefinition(AutoDefenseMountId id, Vector3 localOffset, WeaponSlotId weaponSlotId, WeaponDefinitionId weaponDefinitionId, bool enabled = true)
        {
            if (id.IsEmpty) throw new ArgumentException("Mount id cannot be empty.", nameof(id));
            Id = id; LocalOffset = localOffset; WeaponSlotId = weaponSlotId; WeaponDefinitionId = weaponDefinitionId; Enabled = enabled;
        }

        public AutoDefenseMountId Id { get; }
        public Vector3 LocalOffset { get; }
        public WeaponSlotId WeaponSlotId { get; }
        public WeaponDefinitionId WeaponDefinitionId { get; }
        public bool Enabled { get; }
        public bool HasWeapon => !WeaponSlotId.IsEmpty && !WeaponDefinitionId.IsEmpty;
    }

    /// <summary>Weapon module content assigned to a mount.</summary>
    public sealed class AutoDefenseWeaponModuleDefinition
    {
        public AutoDefenseWeaponModuleDefinition(AutoDefenseMountId mountId, WeaponDefinition weaponDefinition, AttackSourceSnapshot source)
        {
            if (mountId.IsEmpty) throw new ArgumentException("Mount id cannot be empty.", nameof(mountId));
            MountId = mountId; WeaponDefinition = weaponDefinition ?? throw new ArgumentNullException(nameof(weaponDefinition)); Source = source;
        }

        public AutoDefenseMountId MountId { get; }
        public WeaponDefinition WeaponDefinition { get; }
        public AttackSourceSnapshot Source { get; }
    }

    /// <summary>Authored auto-defense run definition.</summary>
    public sealed class AutoDefenseDefinition
    {
        public AutoDefenseDefinition(AutoDefenseObjectiveDefinition objective, AutoDefenseSpawnRingDefinition spawnRing, IReadOnlyList<AutoDefenseEnemyDefinition> enemies, IReadOnlyList<AutoDefenseMountDefinition> mounts, IReadOnlyList<AutoDefenseWeaponModuleDefinition> weaponModules)
        {
            Objective = objective ?? throw new ArgumentNullException(nameof(objective));
            SpawnRing = spawnRing ?? throw new ArgumentNullException(nameof(spawnRing));
            Enemies = CopyEnemies(enemies);
            Mounts = CopyMounts(mounts);
            WeaponModules = CopyModules(weaponModules);
        }

        public AutoDefenseObjectiveDefinition Objective { get; }
        public AutoDefenseSpawnRingDefinition SpawnRing { get; }
        public IReadOnlyList<AutoDefenseEnemyDefinition> Enemies { get; }
        public IReadOnlyList<AutoDefenseMountDefinition> Mounts { get; }
        public IReadOnlyList<AutoDefenseWeaponModuleDefinition> WeaponModules { get; }
        private static AutoDefenseEnemyDefinition[] CopyEnemies(IReadOnlyList<AutoDefenseEnemyDefinition> source) { if (source == null || source.Count == 0) throw new ArgumentException("At least one enemy is required.", nameof(source)); var copy = new AutoDefenseEnemyDefinition[source.Count]; var seen = new HashSet<WorldSpawnableId>(); for (int i = 0; i < source.Count; i++) { copy[i] = source[i] ?? throw new ArgumentException("Enemy cannot be null."); if (!seen.Add(copy[i].SpawnableId)) throw new ArgumentException("Duplicate enemy spawnable: " + copy[i].SpawnableId); } return copy; }
        private static AutoDefenseMountDefinition[] CopyMounts(IReadOnlyList<AutoDefenseMountDefinition> source) { if (source == null) return Array.Empty<AutoDefenseMountDefinition>(); var copy = new AutoDefenseMountDefinition[source.Count]; var seen = new HashSet<AutoDefenseMountId>(); for (int i = 0; i < source.Count; i++) { copy[i] = source[i] ?? throw new ArgumentException("Mount cannot be null."); if (!seen.Add(copy[i].Id)) throw new ArgumentException("Duplicate mount: " + copy[i].Id); } Array.Sort(copy, (a, b) => a.Id.CompareTo(b.Id)); return copy; }
        private static AutoDefenseWeaponModuleDefinition[] CopyModules(IReadOnlyList<AutoDefenseWeaponModuleDefinition> source) { if (source == null) return Array.Empty<AutoDefenseWeaponModuleDefinition>(); var copy = new AutoDefenseWeaponModuleDefinition[source.Count]; var seen = new HashSet<AutoDefenseMountId>(); for (int i = 0; i < source.Count; i++) { copy[i] = source[i] ?? throw new ArgumentException("Module cannot be null."); if (!seen.Add(copy[i].MountId)) throw new ArgumentException("Duplicate module mount: " + copy[i].MountId); } return copy; }
    }

    public interface IAutoDefenseContentResolver { bool TryResolveEnemy(WorldSpawnableId spawnableId, out AutoDefenseEnemyDefinition enemy); }
    public interface IAutoDefensePoseResolver { bool TryResolvePose(WorldSpawnChannelId channelId, out SpawnPose pose); }
    public interface IAutoDefenseTargetProvider { int BuildCandidates(IReadOnlyList<AutoDefenseEnemySnapshot> enemies, AttackTargetCandidate[] buffer); }

    public sealed class AutoDefenseDefinitionContentResolver : IAutoDefenseContentResolver
    {
        private readonly Dictionary<WorldSpawnableId, AutoDefenseEnemyDefinition> _enemies = new Dictionary<WorldSpawnableId, AutoDefenseEnemyDefinition>();
        public AutoDefenseDefinitionContentResolver(AutoDefenseDefinition definition) { for (int i = 0; i < definition.Enemies.Count; i++) _enemies.Add(definition.Enemies[i].SpawnableId, definition.Enemies[i]); }
        public bool TryResolveEnemy(WorldSpawnableId spawnableId, out AutoDefenseEnemyDefinition enemy) => _enemies.TryGetValue(spawnableId, out enemy);
    }

    public sealed class AutoDefensePerimeterPoseResolver : IAutoDefensePoseResolver, ISpawnPoseResolver
    {
        private readonly AutoDefenseObjectiveDefinition _objective;
        private readonly Dictionary<WorldSpawnChannelId, AutoDefenseSpawnChannelDefinition> _channels = new Dictionary<WorldSpawnChannelId, AutoDefenseSpawnChannelDefinition>();
        private readonly float _radius;
        public AutoDefensePerimeterPoseResolver(AutoDefenseObjectiveDefinition objective, AutoDefenseSpawnRingDefinition ring)
        {
            _objective = objective ?? throw new ArgumentNullException(nameof(objective)); if (ring == null) throw new ArgumentNullException(nameof(ring));
            _radius = ring.Radius; for (int i = 0; i < ring.Channels.Count; i++) _channels.Add(ring.Channels[i].Id, ring.Channels[i]);
        }
        public bool TryResolvePose(WorldSpawnChannelId channelId, out SpawnPose pose)
        {
            if (!_channels.TryGetValue(channelId, out AutoDefenseSpawnChannelDefinition channel)) { pose = default; return false; }
            float radians = channel.AngleDegrees * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            pose = new SpawnPose(_objective.Position + direction * _radius, Quaternion.LookRotation(-direction, Vector3.up));
            return true;
        }
        public SpawnPoseResult TryResolvePose(WorldSpawnRequest request) => TryResolvePose(request.ChannelId, out SpawnPose pose) ? SpawnPoseResult.Success(pose) : SpawnPoseResult.Failure("Unknown auto-defense channel: " + request.ChannelId);
    }

    public readonly struct AutoDefenseEnemySnapshot
    {
        public AutoDefenseEnemySnapshot(long id, WorldSpawnableId spawnableId, CombatantId combatantId, Vector3 position, double health, AutoDefenseEnemyLifecycle lifecycle, float objectiveProgress)
        {
            Id = id; SpawnableId = spawnableId; CombatantId = combatantId; Position = position; Health = health; Lifecycle = lifecycle; ObjectiveProgress = objectiveProgress;
        }
        public long Id { get; }
        public WorldSpawnableId SpawnableId { get; }
        public CombatantId CombatantId { get; }
        public Vector3 Position { get; }
        public double Health { get; }
        public AutoDefenseEnemyLifecycle Lifecycle { get; }
        public float ObjectiveProgress { get; }
    }

    public sealed class AutoDefenseRuntimeSnapshot
    {
        public AutoDefenseRuntimeSnapshot(AutoDefenseRuntimeState state, double objectiveHealth, int lives, IReadOnlyList<AutoDefenseEnemySnapshot> enemies)
        {
            State = state; ObjectiveHealth = objectiveHealth; Lives = lives; Enemies = Copy(enemies);
        }
        public AutoDefenseRuntimeState State { get; }
        public double ObjectiveHealth { get; }
        public int Lives { get; }
        public IReadOnlyList<AutoDefenseEnemySnapshot> Enemies { get; }
        private static AutoDefenseEnemySnapshot[] Copy(IReadOnlyList<AutoDefenseEnemySnapshot> source) { if (source == null) return Array.Empty<AutoDefenseEnemySnapshot>(); var copy = new AutoDefenseEnemySnapshot[source.Count]; for (int i = 0; i < source.Count; i++) copy[i] = source[i]; return copy; }
    }

    public sealed class AutoDefenseRunResult
    {
        public AutoDefenseRunResult(int spawned, int moved, int reached, int killed, WeaponFireResult weaponFireResult, IReadOnlyList<ProjectileLaunchRequest> projectileLaunches, AutoDefenseFailureReason failureReason = AutoDefenseFailureReason.None)
        {
            Spawned = spawned; Moved = moved; ReachedObjective = reached; Killed = killed; WeaponFireResult = weaponFireResult; ProjectileLaunches = Copy(projectileLaunches); FailureReason = failureReason;
        }
        public int Spawned { get; }
        public int Moved { get; }
        public int ReachedObjective { get; }
        public int Killed { get; }
        public WeaponFireResult WeaponFireResult { get; }
        public IReadOnlyList<ProjectileLaunchRequest> ProjectileLaunches { get; }
        public AutoDefenseFailureReason FailureReason { get; }
        public bool Succeeded => FailureReason == AutoDefenseFailureReason.None;
        private static ProjectileLaunchRequest[] Copy(IReadOnlyList<ProjectileLaunchRequest> source) { if (source == null) return Array.Empty<ProjectileLaunchRequest>(); var copy = new ProjectileLaunchRequest[source.Count]; for (int i = 0; i < source.Count; i++) copy[i] = source[i]; return copy; }
    }

    /// <summary>Coordinates a central-objective auto-defense run.</summary>
    public sealed class AutoDefenseRuntime
    {
        public static readonly EncounterMetricId SpawnedMetric = new EncounterMetricId("auto-defense.spawned");
        public static readonly EncounterMetricId KilledMetric = new EncounterMetricId("auto-defense.killed");
        public static readonly EncounterMetricId LeakedMetric = new EncounterMetricId("auto-defense.leaked");
        public static readonly EncounterMetricId ObjectiveDamageMetric = new EncounterMetricId("auto-defense.objective-damage");
        private readonly AutoDefenseDefinition _definition;
        private readonly IAutoDefenseContentResolver _content;
        private readonly IAutoDefensePoseResolver _poses;
        private readonly WorldSpawnService _spawning;
        private readonly WorldNavigationService _navigation;
        private readonly WeaponRuntime _weapons;
        private readonly CombatCatalog _combatCatalog;
        private readonly EncounterRuntime _encounter;
        private readonly List<EnemyState> _enemies = new List<EnemyState>();
        private readonly AttackTargetCandidate[] _candidateBuffer;
        private long _nextEnemyId;

        public AutoDefenseRuntime(AutoDefenseDefinition definition, WorldSpawnService spawning, WorldNavigationService navigation, WeaponRuntime weapons, CombatCatalog combatCatalog, EncounterRuntime encounter = null, IAutoDefenseContentResolver content = null, IAutoDefensePoseResolver poses = null, int candidateCapacity = 256)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _spawning = spawning ?? throw new ArgumentNullException(nameof(spawning));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _combatCatalog = combatCatalog ?? throw new ArgumentNullException(nameof(combatCatalog));
            _encounter = encounter;
            _content = content ?? new AutoDefenseDefinitionContentResolver(definition);
            _poses = poses ?? new AutoDefensePerimeterPoseResolver(definition.Objective, definition.SpawnRing);
            _candidateBuffer = new AttackTargetCandidate[Math.Max(1, candidateCapacity)];
            Objective = new AutoDefenseObjectiveState(definition.Objective);
            RegisterMounts();
        }

        public AutoDefenseObjectiveState Objective { get; }
        public AutoDefenseRuntimeState State { get; private set; }
        public int ActiveEnemyCount { get { int count = 0; for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Lifecycle == AutoDefenseEnemyLifecycle.Active) count++; return count; } }

        public void Start() { State = AutoDefenseRuntimeState.Running; _encounter?.Start(); }
        public void Stop() { State = AutoDefenseRuntimeState.Stopped; _encounter?.Stop(); }

        public AutoDefenseRunResult ConsumeSpawnRequest(SpawnRequest request)
        {
            if (State != AutoDefenseRuntimeState.Running) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.NotRunning);
            var worldRequest = new WorldSpawnRequest(new WorldSpawnableId(request.SpawnableId.Value), new WorldSpawnChannelId(request.ChannelId.Value), request.Sequence, new WorldSpawnRequestContext("auto-defense", request.EncounterId.Value, request.WaveId.Value, request.GroupId.Value, 0, (int)request.ScheduledTick));
            if (!_content.TryResolveEnemy(worldRequest.SpawnableId, out AutoDefenseEnemyDefinition enemyDefinition)) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.UnknownEnemy);
            if (!_poses.TryResolvePose(worldRequest.ChannelId, out _)) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.UnknownChannel);
            SpawnResult spawn = _spawning.Spawn(worldRequest);
            if (!spawn.Succeeded || spawn.Instance == null) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.SpawnFailed);
            MovementAgentHandle handle = _navigation.Register(new TransformMovementPoseAccessor(spawn.Instance.transform), new ConstantMovementSpeedProvider(enemyDefinition.Speed));
            MovementResult destination = _navigation.SetDestination(handle.Id, _definition.Objective.Position, Math.Max(_definition.Objective.ContactRadius, enemyDefinition.CollisionRadius));
            if (!destination.Succeeded) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.NavigationFailed);
            var id = ++_nextEnemyId;
            var health = new HealthState(new CombatantId("auto-defense.enemy/" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)), enemyDefinition.MaximumHealth, enemyDefinition.MaximumHealth);
            _enemies.Add(new EnemyState(id, enemyDefinition, spawn.InstanceId, handle.Id, spawn.Instance, health));
            _encounter?.IncrementMetric(SpawnedMetric, 1);
            return new AutoDefenseRunResult(1, 0, 0, 0, null, null);
        }

        public AutoDefenseRunResult Tick(int weaponTicks, float deltaSeconds)
        {
            if (State != AutoDefenseRuntimeState.Running) return new AutoDefenseRunResult(0, 0, 0, 0, null, null, AutoDefenseFailureReason.NotRunning);
            _encounter?.AdvanceTicks(weaponTicks);
            MovementTickResult movement = _navigation.Tick(deltaSeconds);
            int reached = HandleObjectiveContacts();
            _weapons.Tick(weaponTicks);
            int candidateCount = BuildCandidates(_candidateBuffer);
            WeaponFireResult fire = _weapons.FireReady(new WeaponFireRequest(new ArraySegment<AttackTargetCandidate>(_candidateBuffer, 0, candidateCount), _definition.Objective.Position, _definition.Objective.Position));
            int killed = ResolveWeaponIntents(fire, out ProjectileLaunchRequest[] projectileLaunches);
            UpdateTerminalState();
            return new AutoDefenseRunResult(0, movement.AgentsMoved, reached, killed, fire, projectileLaunches);
        }

        public bool TryKillEnemy(long id)
        {
            EnemyState enemy = FindEnemy(id);
            if (enemy == null || enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active) return false;
            MarkKilled(enemy);
            return true;
        }

        public AutoDefenseRuntimeSnapshot CreateSnapshot()
        {
            var snapshots = new List<AutoDefenseEnemySnapshot>();
            for (int i = 0; i < _enemies.Count; i++) snapshots.Add(CreateEnemySnapshot(_enemies[i]));
            return new AutoDefenseRuntimeSnapshot(State, Objective.Health.CurrentHealth, Objective.LivesRemaining, snapshots);
        }

        private void RegisterMounts()
        {
            for (int i = 0; i < _definition.Mounts.Count; i++)
            {
                AutoDefenseMountDefinition mount = _definition.Mounts[i];
                if (!mount.HasWeapon) continue;
                AutoDefenseWeaponModuleDefinition module = FindModule(mount.Id);
                if (module == null) continue;
                _weapons.RegisterWeapon(new WeaponMountSnapshot(mount.WeaponSlotId, mount.WeaponDefinitionId, module.Source, mount.Enabled));
            }
        }

        private AutoDefenseWeaponModuleDefinition FindModule(AutoDefenseMountId id)
        {
            for (int i = 0; i < _definition.WeaponModules.Count; i++) if (_definition.WeaponModules[i].MountId.Equals(id)) return _definition.WeaponModules[i];
            return null;
        }

        private int BuildCandidates(AttackTargetCandidate[] buffer)
        {
            int count = 0;
            for (int i = 0; i < _enemies.Count && count < buffer.Length; i++)
            {
                EnemyState enemy = _enemies[i];
                if (enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active || enemy.Health == null || !enemy.Health.IsAlive) continue;
                if (!_navigation.TryGetPose(enemy.MovementAgentId, out MovementPose pose)) continue;
                float distance = Vector3.Distance(pose.Position, _definition.Objective.Position);
                float score = 100000f - distance;
                buffer[count++] = new AttackTargetCandidate(enemy.Health.Id, enemy.Health, score);
            }
            return count;
        }

        private int ResolveWeaponIntents(WeaponFireResult result, out ProjectileLaunchRequest[] projectileLaunches)
        {
            var launches = new List<ProjectileLaunchRequest>();
            int killed = 0;
            if (result != null)
            {
                for (int i = 0; i < result.Intents.Count; i++)
                {
                    WeaponIntent intent = result.Intents[i];
                    if (intent.Kind == WeaponIntentKind.ProjectileLaunch)
                    {
                        launches.Add(intent.ProjectileLaunchRequest);
                        continue;
                    }
                    if (intent.AttackIntent == null) continue;
                    DamageResolutionResult damage = CombatDamageResolver.Resolve(intent.AttackIntent.ResolutionRequest);
                    EnemyState enemy = FindEnemy(intent.AttackIntent.Selection.Target.CombatantId);
                    if (enemy != null && enemy.Lifecycle == AutoDefenseEnemyLifecycle.Active && enemy.Health.LifeState == LifeState.Dead)
                    {
                        MarkKilled(enemy);
                        killed++;
                    }
                }
            }
            projectileLaunches = launches.ToArray();
            return killed;
        }

        private int HandleObjectiveContacts()
        {
            int reached = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState enemy = _enemies[i];
                if (enemy.Lifecycle != AutoDefenseEnemyLifecycle.Active) continue;
                if (!_navigation.TryGetPose(enemy.MovementAgentId, out MovementPose pose)) continue;
                float distance = Vector3.Distance(pose.Position, _definition.Objective.Position);
                if (distance > _definition.Objective.ContactRadius + enemy.Definition.CollisionRadius) continue;
                ApplyObjectiveDamage(enemy.Definition.ContactDamage);
                enemy.Lifecycle = AutoDefenseEnemyLifecycle.ReachedObjective;
                Cleanup(enemy, DespawnReason.Completed);
                _encounter?.IncrementMetric(LeakedMetric, 1);
                reached++;
            }
            return reached;
        }

        private void ApplyObjectiveDamage(double damage)
        {
            var request = new DamageRequest(Objective.Health.Id, new[] { new DamageComponent(_definition.Objective.DamageTypeId, damage) });
            DamageResolutionResult result = CombatDamageResolver.Resolve(_combatCatalog, Objective.Health, null, request);
            Objective.ApplyLeak();
            if (Objective.Health.LifeState == LifeState.Dead) Objective.MarkFailed();
            _encounter?.IncrementMetric(ObjectiveDamageMetric, Math.Max(1, (long)Math.Ceiling(result.HealthDamage)));
        }

        private void MarkKilled(EnemyState enemy)
        {
            enemy.Lifecycle = AutoDefenseEnemyLifecycle.Killed;
            Cleanup(enemy, DespawnReason.Killed);
            _encounter?.IncrementMetric(KilledMetric, 1);
        }

        private void Cleanup(EnemyState enemy, DespawnReason reason)
        {
            _navigation.CleanupDespawned(enemy.MovementAgentId);
            _spawning.Despawn(enemy.SpawnInstanceId, reason);
        }

        private void UpdateTerminalState()
        {
            if (Objective.Failed) State = AutoDefenseRuntimeState.Failed;
            else if (_encounter != null && _encounter.State == EncounterLifecycleState.Completed && ActiveEnemyCount == 0) State = AutoDefenseRuntimeState.Completed;
        }

        private EnemyState FindEnemy(CombatantId id)
        {
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Health.Id.Equals(id)) return _enemies[i];
            return null;
        }

        private EnemyState FindEnemy(long id)
        {
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Id == id) return _enemies[i];
            return null;
        }

        private AutoDefenseEnemySnapshot CreateEnemySnapshot(EnemyState enemy)
        {
            Vector3 position = enemy.Instance == null ? default : enemy.Instance.transform.position;
            float progress = 0f;
            if (_navigation.TryGetProgress(enemy.MovementAgentId, out MovementProgress movement)) progress = movement.NormalizedProgress;
            return new AutoDefenseEnemySnapshot(enemy.Id, enemy.Definition.SpawnableId, enemy.Health.Id, position, enemy.Health.CurrentHealth, enemy.Lifecycle, progress);
        }

        private sealed class EnemyState
        {
            public EnemyState(long id, AutoDefenseEnemyDefinition definition, SpawnInstanceId spawnInstanceId, MovementAgentId movementAgentId, GameObject instance, HealthState health)
            {
                Id = id; Definition = definition; SpawnInstanceId = spawnInstanceId; MovementAgentId = movementAgentId; Instance = instance; Health = health; Lifecycle = AutoDefenseEnemyLifecycle.Active;
            }
            public long Id;
            public AutoDefenseEnemyDefinition Definition;
            public SpawnInstanceId SpawnInstanceId;
            public MovementAgentId MovementAgentId;
            public GameObject Instance;
            public HealthState Health;
            public AutoDefenseEnemyLifecycle Lifecycle;
        }
    }
}
