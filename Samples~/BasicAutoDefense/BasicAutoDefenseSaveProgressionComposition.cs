using System;
using System.Threading;
using Deucarian.IdleProgression;
using Deucarian.Persistence;
using Deucarian.Progression;
using Deucarian.RunUpgrades;

namespace Deucarian.AutoDefense.Samples
{
    public static class BasicAutoDefenseSaveProgressionComposition
    {
        private static readonly CurrencyId Credits = new CurrencyId("currency.sample.credits");
        private static readonly CurrencyId Parts = new CurrencyId("currency.sample.parts");
        private static readonly TrackId AccountXp = new TrackId("track.sample.account");
        private static readonly UnlockId StarterUnlock = new UnlockId("unlock.sample.starter");
        private static readonly DocumentId ProfileDocumentId = new DocumentId("basic-auto-defense-profile");
        private static readonly DocumentId RunDocumentId = new DocumentId("basic-auto-defense-run");
        private static readonly DocumentId SettingsDocumentId = new DocumentId("basic-auto-defense-settings");

        public static BasicAutoDefenseCompositionSmokeResult RunSmoke()
        {
            return RunSmokeAsync().GetAwaiter().GetResult();
        }

        private static async System.Threading.Tasks.Task<BasicAutoDefenseCompositionSmokeResult> RunSmokeAsync()
        {
            var storage = new InMemoryTextStorage();
            using var service = new PersistenceService(storage);
            ProgressionCatalog progressionCatalog = CreateProgressionCatalog();
            var progressionState = new ProgressionState();
            RunUpgradeCatalog upgradeCatalog = BasicAutoDefenseSample.CreateRunUpgradeCatalog();

            DocumentDefinition<ProfileDto> profileDefinition = CreateProfileDefinition();
            DocumentDefinition<RunResumeDto> runDefinition = CreateRunDefinition();
            DocumentDefinition<SettingsDto> settingsDefinition = CreateSettingsDefinition();
            SaveSlotId slot = SaveSlotId.Default;

            DateTimeOffset lastSeen = DateTimeOffset.UnixEpoch;
            var profile = new ProfileDto
            {
                Credits = 10,
                Parts = 1,
                Experience = 0,
                HasStarterUnlock = false,
                LastSeenUtcTicks = lastSeen.UtcTicks
            };
            WriteResult profileSave = await service.SaveAsync(profileDefinition, profile, slot, CancellationToken.None);
            LoadResult<ProfileDto> profileLoad = await service.LoadAsync(profileDefinition, slot, CancellationToken.None);

            var upgradeState = new RunUpgradeState();
            upgradeState.Select(upgradeCatalog, new RunUpgradeId("upgrade.direct.damage"));
            RunUpgradeSnapshot upgradeSnapshot = upgradeState.CreateSnapshot();
            var run = RunResumeDto.FromSnapshot("run.sample.1", 42, upgradeSnapshot, lastSeen.UtcTicks);
            WriteResult runSave = await service.SaveAsync(runDefinition, run, slot, CancellationToken.None);
            LoadResult<RunResumeDto> runLoad = await service.LoadAsync(runDefinition, slot, CancellationToken.None);
            RunUpgradeState restoredUpgradeState = RunUpgradeState.FromSnapshot(runLoad.Document.ToSnapshot());

            var settings = new SettingsDto { AudioVolume = 0.8f, ReducedMotion = true };
            WriteResult settingsSave = await service.SaveAsync(settingsDefinition, settings, slot, CancellationToken.None);
            LoadResult<SettingsDto> settingsLoad = await service.LoadAsync(settingsDefinition, slot, CancellationToken.None);

            RewardBundle runReward = new RewardBundle(
                new[] { new CurrencyLine(Credits, new ProgressionAmount(25), true) },
                new[] { new XpGrant(AccountXp, new ProgressionAmount(10)) },
                new[] { StarterUnlock });
            ProgressionResult runRewardResult = progressionState.ApplyReward(progressionCatalog, new ProgressionOperationId("sample.run.complete.1"), runReward);

            IdleProgressionResult offline = IdleProgressionCalculator.Calculate(
                new DateTimeOffset(profileLoad.Document.LastSeenUtcTicks, TimeSpan.Zero),
                lastSeen.AddHours(1),
                BasicAutoDefenseSample.CreateOfflineProgressionDefinition());
            ProgressionResult offlineRewardResult = progressionState.ApplyReward(progressionCatalog, new ProgressionOperationId("sample.offline.1"), offline.Reward);

            LoadResult<ProfileDto> missingDefaults = await service.LoadAsync(profileDefinition, new SaveSlotId("empty"), CancellationToken.None);

            await service.SaveAsync(profileDefinition, new ProfileDto { Credits = 2, LastSeenUtcTicks = lastSeen.UtcTicks }, new SaveSlotId("recover"), CancellationToken.None);
            await service.SaveAsync(profileDefinition, new ProfileDto { Credits = 3, LastSeenUtcTicks = lastSeen.UtcTicks }, new SaveSlotId("recover"), CancellationToken.None);
            storage.Files[new DocumentLocation(ProfileDocumentId, new SaveSlotId("recover")).FileStem + ".json"] = "{ broken";
            LoadResult<ProfileDto> recovered = await service.LoadAsync(profileDefinition, new SaveSlotId("recover"), CancellationToken.None);

            LoadResult<ProfileDto> migrated = await LoadMigratedProfile(service, storage);

            return new BasicAutoDefenseCompositionSmokeResult
            {
                ProfileSavedAndLoaded = profileSave.Succeeded && profileLoad.Succeeded && profileLoad.Document.Credits == 10,
                RunSavedAndLoaded = runSave.Succeeded && runLoad.Succeeded && runLoad.Document.Tick == 42,
                SettingsSavedAndLoaded = settingsSave.Succeeded && settingsLoad.Succeeded && settingsLoad.Document.ReducedMotion,
                RunRewardApplied = runRewardResult.Succeeded &&
                    progressionState.GetBalance(Credits).Value >= 25 &&
                    progressionState.GetTrackTotal(AccountXp).Value == 10 &&
                    progressionState.IsUnlocked(StarterUnlock),
                RunUpgradeSnapshotRestored = restoredUpgradeState.GetRank(new RunUpgradeId("upgrade.direct.damage")) == 1,
                OfflineRewardCalculated = offline.Code == IdleProgressionResultCode.Success && offlineRewardResult.Succeeded,
                MissingSaveDefaulted = missingDefaults.Succeeded && missingDefaults.Outcome == LoadOutcome.CreatedDefault,
                CorruptedPrimaryRecovered = recovered.Succeeded && recovered.Outcome == LoadOutcome.RecoveredFromBackup && recovered.Document.Credits == 2,
                MigrationApplied = migrated.Succeeded && migrated.Outcome == LoadOutcome.Migrated && migrated.Document.Parts == 0,
                Credits = progressionState.GetBalance(Credits).Value,
                Parts = progressionState.GetBalance(Parts).Value,
                Experience = progressionState.GetTrackTotal(AccountXp).Value
            };
        }

        private static ProgressionCatalog CreateProgressionCatalog()
        {
            return new ProgressionCatalog(
                new[]
                {
                    new CurrencyDefinition(Credits, new ProgressionAmount(100_000)),
                    new CurrencyDefinition(Parts, new ProgressionAmount(10_000))
                },
                new[] { new ProgressionTrackDefinition(AccountXp, 0, new[] { new ProgressionAmount(100), new ProgressionAmount(250) }) });
        }

        private static DocumentDefinition<ProfileDto> CreateProfileDefinition()
        {
            var migrations = new DocumentMigrationSet(new[]
            {
                new DelegateDocumentMigration(ProfileDocumentId, new SchemaVersion(1), new SchemaVersion(2), (payload, serializer) =>
                {
                    ProfileV1Dto legacy = serializer.Deserialize<ProfileV1Dto>(payload);
                    return serializer.Serialize(new ProfileDto
                    {
                        Credits = legacy.Credits,
                        Parts = 0,
                        Experience = 0,
                        HasStarterUnlock = false,
                        LastSeenUtcTicks = legacy.LastSeenUtcTicks
                    });
                })
            });
            return new DocumentDefinition<ProfileDto>(
                ProfileDocumentId,
                new SchemaVersion(2),
                () => new ProfileDto { LastSeenUtcTicks = DateTimeOffset.UnixEpoch.UtcTicks },
                new DelegateDocumentValidator<ProfileDto>(document => document.Credits >= 0 && document.Parts >= 0 && document.Experience >= 0
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Profile values cannot be negative.")),
                migrations);
        }

        private static DocumentDefinition<RunResumeDto> CreateRunDefinition()
        {
            return new DocumentDefinition<RunResumeDto>(
                RunDocumentId,
                new SchemaVersion(1),
                () => new RunResumeDto(),
                new DelegateDocumentValidator<RunResumeDto>(document => document.Tick >= 0 ? ValidationResult.Success() : ValidationResult.Failure("Tick cannot be negative.")));
        }

        private static DocumentDefinition<SettingsDto> CreateSettingsDefinition()
        {
            return new DocumentDefinition<SettingsDto>(
                SettingsDocumentId,
                new SchemaVersion(1),
                () => new SettingsDto { AudioVolume = 1f },
                new DelegateDocumentValidator<SettingsDto>(document => document.AudioVolume >= 0f && document.AudioVolume <= 1f
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Audio volume must be normalized.")));
        }

        private static async System.Threading.Tasks.Task<LoadResult<ProfileDto>> LoadMigratedProfile(PersistenceService service, InMemoryTextStorage storage)
        {
            var serializer = new NewtonsoftPersistenceSerializer();
            var legacyDefinition = new DocumentDefinition<ProfileV1Dto>(
                ProfileDocumentId,
                new SchemaVersion(1),
                () => new ProfileV1Dto());
            var legacy = new ProfileV1Dto { Credits = 7, LastSeenUtcTicks = DateTimeOffset.UnixEpoch.UtcTicks };
            storage.Files[new DocumentLocation(ProfileDocumentId, new SaveSlotId("migration")).FileStem + ".json"] =
                SaveEnvelopeCodec.Create(legacyDefinition, legacy, serializer, DateTimeOffset.UnixEpoch);
            return await service.LoadAsync(CreateProfileDefinition(), new SaveSlotId("migration"), CancellationToken.None);
        }

        public sealed class ProfileDto
        {
            public long Credits;
            public long Parts;
            public long Experience;
            public bool HasStarterUnlock;
            public long LastSeenUtcTicks;
        }

        public sealed class ProfileV1Dto
        {
            public long Credits;
            public long LastSeenUtcTicks;
        }

        public sealed class SettingsDto
        {
            public float AudioVolume;
            public bool ReducedMotion;
        }

        public sealed class RunResumeDto
        {
            public string RunId;
            public int Tick;
            public string[] UpgradeIds = Array.Empty<string>();
            public int[] UpgradeRanks = Array.Empty<int>();
            public string[] BanishedUpgradeIds = Array.Empty<string>();
            public long LastOfflineClaimUtcTicks;

            public static RunResumeDto FromSnapshot(string runId, int tick, RunUpgradeSnapshot snapshot, long lastOfflineClaimUtcTicks)
            {
                var ids = new string[snapshot.Ranks.Count];
                var ranks = new int[snapshot.Ranks.Count];
                for (int i = 0; i < snapshot.Ranks.Count; i++)
                {
                    ids[i] = snapshot.Ranks[i].Id.Value;
                    ranks[i] = snapshot.Ranks[i].Rank;
                }

                var banished = new string[snapshot.Banished.Count];
                for (int i = 0; i < snapshot.Banished.Count; i++) banished[i] = snapshot.Banished[i].Value;

                return new RunResumeDto
                {
                    RunId = runId,
                    Tick = tick,
                    UpgradeIds = ids,
                    UpgradeRanks = ranks,
                    BanishedUpgradeIds = banished,
                    LastOfflineClaimUtcTicks = lastOfflineClaimUtcTicks
                };
            }

            public RunUpgradeSnapshot ToSnapshot()
            {
                int count = Math.Min(UpgradeIds == null ? 0 : UpgradeIds.Length, UpgradeRanks == null ? 0 : UpgradeRanks.Length);
                var ranks = new RunUpgradeRankSnapshot[count];
                for (int i = 0; i < count; i++) ranks[i] = new RunUpgradeRankSnapshot(new RunUpgradeId(UpgradeIds[i]), UpgradeRanks[i]);
                var banished = new RunUpgradeId[BanishedUpgradeIds == null ? 0 : BanishedUpgradeIds.Length];
                for (int i = 0; i < banished.Length; i++) banished[i] = new RunUpgradeId(BanishedUpgradeIds[i]);
                return new RunUpgradeSnapshot(ranks, banished);
            }
        }
    }

    public sealed class BasicAutoDefenseCompositionSmokeResult
    {
        public bool ProfileSavedAndLoaded;
        public bool RunSavedAndLoaded;
        public bool SettingsSavedAndLoaded;
        public bool RunRewardApplied;
        public bool RunUpgradeSnapshotRestored;
        public bool OfflineRewardCalculated;
        public bool MissingSaveDefaulted;
        public bool CorruptedPrimaryRecovered;
        public bool MigrationApplied;
        public long Credits;
        public long Parts;
        public long Experience;

        public bool Succeeded => ProfileSavedAndLoaded &&
            RunSavedAndLoaded &&
            SettingsSavedAndLoaded &&
            RunRewardApplied &&
            RunUpgradeSnapshotRestored &&
            OfflineRewardCalculated &&
            MissingSaveDefaulted &&
            CorruptedPrimaryRecovered &&
            MigrationApplied;
    }
}
