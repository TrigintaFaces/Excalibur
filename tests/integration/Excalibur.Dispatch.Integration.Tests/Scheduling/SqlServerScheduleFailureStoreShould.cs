// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Testing.Conformance;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Integration.Tests.Scheduling;

[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerScheduleFailureStoreShould(SqlServerFixture fixture) : ScheduleFailureStoreConformanceTestKit, IAsyncLifetime
{
	private readonly string _database = "schedule_failure_" + Guid.NewGuid().ToString("N");
	private string _connectionString = null!;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Database name is generated locally as a fixed prefix and GUID; scripts are shipped embedded resources.")]
	public async ValueTask InitializeAsync()
	{
		await using (var master = new SqlConnection(fixture.ConnectionString))
		{
			await master.OpenAsync(Ct);
			await using var create = new SqlCommand($"CREATE DATABASE [{_database}]", master);
			_ = await create.ExecuteNonQueryAsync(Ct);
		}
		_connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = _database }.ConnectionString;
		foreach (var file in new[] { "004_CreateScheduleSchema.sql", "005_CreateScheduleFailureSchema.sql", "006_AddScheduleCronRecurrenceProfile.sql" })
		{
			var assembly = typeof(SqlServerScheduleFailureStoreShould).Assembly;
			var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("SqlServer." + file, StringComparison.Ordinal));
			await using var stream = assembly.GetManifestResourceStream(resource)!;
			using var text = new StreamReader(stream);
			await ExecuteAsync(await text.ReadToEndAsync(Ct));
		}
	}

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Database identifier is fixed prefix plus locally generated GUID.")]
	public async ValueTask DisposeAsync()
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await using var master = new SqlConnection(fixture.ConnectionString);
		await master.OpenAsync(deadline.Token);
		await using var drop = new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", master);
		_ = await drop.ExecuteNonQueryAsync(deadline.Token);
	}

	[Fact]
	public async Task CaptureBudgetBeforeGrantAndNeverWidenItThroughEitherAdmissionPath()
	{
		var store = NewStore();
		var message = Message("tenant");
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var first = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 2, Ct)).ShouldNotBeNull();
		first.PolicySelection.ShouldBe(ScheduleAdmissionPolicySelection.CapturedNew);
		first.CapturedMaxAdmissionAttempts.ShouldBe(2);
		first.PolicyDispositionAllowed.ShouldBeTrue();
		(await store.TryRecordFailureAsync(first.Lease, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), Ct)).ShouldBeFalse();
		(await store.TryUpdateProgressAsync(first.Lease, new ScheduleExecutionResult(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), Ct)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(first.Lease, Ct)).ShouldBeTrue();
		var legacy = (await NewStore().TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, Ct)).ShouldNotBeNull();
		legacy.AdmissionAttempt.ShouldBe(2);
		(await store.TryRecordFailureAsync(legacy, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), Ct)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(legacy, Ct)).ShouldBeTrue();
		var exhausted = (await NewStore().TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, 999, Ct)).ShouldNotBeNull();
		exhausted.PolicySelection.ShouldBe(ScheduleAdmissionPolicySelection.UsedExisting);
		exhausted.CapturedMaxAdmissionAttempts.ShouldBe(2);
		exhausted.PolicyDispositionAllowed.ShouldBeFalse();
		exhausted.Lease.AdmissionAttempt.ShouldBe(3, "consumed authority grants are not proven handler calls");
		(await store.TryRecordFailureAsync(exhausted.Lease, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), Ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task RefuseConsumedUnbudgetedHistoryWithoutGuessingOrResettingIt()
	{
		var store = NewStore();
		var message = Message(null);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var legacy = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, Ct)).ShouldNotBeNull();
		(await store.ReleaseExecutionAsync(legacy, Ct)).ShouldBeTrue();
		await Should.ThrowAsync<InvalidOperationException>(() => NewStore().TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, 2, Ct));
		var second = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, Ct)).ShouldNotBeNull();
		second.AdmissionAttempt.ShouldBe(2);
	}

	[Fact]
	public async Task RetainFailureAcrossAdministrativeCompleteAndRecoverAnIdempotentNewBoundedCycle()
	{
		var store = NewStore();
		var message = Message("Tenant-A");
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var admission = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 2, Ct)).ShouldNotBeNull();
		var failure = new ScheduleOccurrenceFailure(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying);
		(await store.TryRecordFailureAsync(admission.Lease, failure, Ct)).ShouldBeTrue();
		var first = (await NewStore().GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, "Tenant-A", Ct)).ShouldNotBeNull();
		(await store.TryRecordFailureAsync(admission.Lease, failure, Ct)).ShouldBeTrue();
		(await store.TryRecordFailureAsync(admission.Lease, failure with { Reason = ScheduleFailureReason.HandlerTimeout }, Ct)).ShouldBeFalse();
		var repeat = (await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, "Tenant-A", Ct)).ShouldNotBeNull();
		repeat.Token.ShouldBe(first.Token);
		repeat.RecordedUtc.ShouldBe(first.RecordedUtc);
		await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(message, Ct));
		await store.CompleteAsync(message.Id, Ct);
		var quarantine = (await NewStore().GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, "Tenant-A", Ct)).ShouldNotBeNull();
		quarantine.Disposition.ShouldBe(ScheduleFailureDisposition.Quarantined);
		quarantine.Message.MessageBody.ShouldBe(message.MessageBody);
		quarantine.Message.TenantId.ShouldBe("Tenant-A");
		(await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation.ShouldBe(generation);
		var command = Guid.NewGuid();
		var concurrent = await Task.WhenAll(
			NewStore().TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, Ct),
			NewStore().TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, Ct));
		var receipt = concurrent[0].ShouldNotBeNull();
		concurrent[1].ShouldBe(receipt);
		receipt.FreshGeneration.ShouldNotBe(generation);
		receipt.MaxAdmissionAttempts.ShouldBe(2);
		(await NewStore().TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, Ct)).ShouldBe(receipt);
		(await store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token with { Version = quarantine.Token.Version + 1 }, command, Ct)).ShouldBeNull();
		(await store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token, Guid.NewGuid(), Ct)).ShouldBeNull();
		(await store.ReleaseExecutionAsync(admission.Lease, Ct)).ShouldBeFalse();
		(await ((IScheduleExecutionLeaseRenewal)store).TryRenewExecutionAsync(admission.Lease, Ct)).ShouldBeFalse();
		var recovered = (await store.TryAcquireExecutionAsync(message.Id, receipt.FreshGeneration, receipt.OccurrenceUtc, 999, Ct)).ShouldNotBeNull();
		recovered.PolicySelection.ShouldBe(ScheduleAdmissionPolicySelection.UsedExisting);
		recovered.CapturedMaxAdmissionAttempts.ShouldBe(2);
		recovered.Lease.AdmissionAttempt.ShouldBe(1);
		recovered.PolicyDispositionAllowed.ShouldBeFalse("recovered work retains failure lineage even at a fresh first grant");
		var skipped = new ScheduleExecutionResult(message.LastExecutionUtc, receipt.OccurrenceUtc.AddHours(1));
		(await store.TryCompleteExecutionAsync(recovered.Lease, skipped, ScheduleExecutionCompletionDisposition.PolicySkipped, Ct)).ShouldBeFalse();
		(await store.TryCompleteExecutionAsync(recovered.Lease, new(null, null, true), ScheduleExecutionCompletionDisposition.PolicyDisabled, Ct)).ShouldBeFalse();
		(await store.TryCompleteExecutionAsync(recovered.Lease, new(null, receipt.OccurrenceUtc), ScheduleExecutionCompletionDisposition.Fulfilled, Ct)).ShouldBeFalse();
		(await store.TryCompleteExecutionAsync(recovered.Lease, new(DateTimeOffset.UtcNow, receipt.OccurrenceUtc.AddHours(1)), ScheduleExecutionCompletionDisposition.Fulfilled, Ct)).ShouldBeTrue();
		(await NewStore().TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, Ct)).ShouldBe(receipt);
		await store.CompleteAsync(message.Id, Ct);
		await store.StoreAsync(message, Ct);
		(await NewStore().TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, Ct)).ShouldBe(receipt);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("Tenant")]
	[InlineData("Tenant ")]
	[InlineData("tenant")]
	[InlineData("__untenanted__")]
	[InlineData("")]
	public async Task ApplyExactTenantBeforeBoundedOrdinalQuarantinePaging(string? tenant)
	{
		var store = NewStore();
		foreach (var current in new string?[] { null, "Tenant", "Tenant ", "__untenanted__", "", "tenant" })
		{
			for (var index = 0; index < 2; index++)
			{
				var message = Message(current);
				await store.StoreAsync(message, Ct);
				var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single(snapshot => snapshot.Message.Id == message.Id).Generation;
				var lease = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
				(await store.TryRecordFailureAsync(lease.Lease, new(ScheduleFailureReason.MalformedPayload, ScheduleFailureDisposition.Retrying), Ct)).ShouldBeTrue();
				await store.CompleteAsync(message.Id, Ct);
			}
		}
		var first = (await NewStore().GetQuarantinesAsync(tenant, null, 1, Ct)).ShouldHaveSingleItem();
		var second = (await NewStore().GetQuarantinesAsync(tenant, first.Token.QuarantineId, 1, Ct)).ShouldHaveSingleItem();
		first.Message.TenantId.ShouldBe(tenant);
		second.Message.TenantId.ShouldBe(tenant);
		var exact = (await NewStore().GetOccurrenceFailureAsync(first.Token.ScheduleId,
			first.Token.Generation, first.Token.OccurrenceUtc, tenant, Ct)).ShouldNotBeNull();
		exact.Token.ShouldBe(first.Token);
		exact.Message.TenantId.ShouldBe(tenant);
		foreach (var other in new string?[] { null, "Tenant", "Tenant ", "__untenanted__", "", "tenant" })
		{
			if (string.Equals(other, tenant, StringComparison.Ordinal)) { continue; }
			(await NewStore().GetOccurrenceFailureAsync(first.Token.ScheduleId,
				first.Token.Generation, first.Token.OccurrenceUtc, other, Ct)).ShouldBeNull();
		}
		StringComparer.Ordinal.Compare(first.Token.QuarantineId.ToString("N"), second.Token.QuarantineId.ToString("N")).ShouldBeLessThan(0);
		(await store.GetQuarantinesAsync(tenant, second.Token.QuarantineId, 1, Ct)).ShouldBeEmpty();
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => store.GetQuarantinesAsync(tenant, null, 257, Ct));
	}

	[Fact]
	public async Task RollBackFailureAndRecoveryStateTogetherWhenRealSqlRejectsTheirWrites()
	{
		var store = NewStore();
		var message = Message(null);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var grant = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
		var terminal = new ScheduleOccurrenceFailure(ScheduleFailureReason.MalformedPayload, ScheduleFailureDisposition.Quarantined);
		var retrying = terminal with { Disposition = ScheduleFailureDisposition.Retrying };
		await ExecuteAsync("CREATE TRIGGER dispatch.FailFailureWrite ON dispatch.ScheduleOccurrenceFailure AFTER INSERT AS THROW 51000, 'controlled failure persistence rejection', 1;");
		try
		{
			var rejectedFailure = await Should.ThrowAsync<SqlException>(() => store.TryRecordFailureAsync(grant.Lease, retrying, Ct));
			rejectedFailure.Number.ShouldBe(51000);
			rejectedFailure.Message.ShouldContain("controlled failure persistence rejection");
			(await NewStore().GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldBeNull();
			await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(message, Ct));
			var rejectedCancellation = await Should.ThrowAsync<SqlException>(() => store.CompleteAsync(message.Id, Ct));
			rejectedCancellation.Number.ShouldBe(51000);
			rejectedCancellation.Message.ShouldContain("controlled failure persistence rejection");
			(await store.GetAllAsync(Ct)).Single().Enabled.ShouldBeTrue();
			(await store.GetAllAsync(Ct)).Single().NextExecutionUtc.ShouldBe(message.NextExecutionUtc);
		}
		finally { await ExecuteAsync("DROP TRIGGER dispatch.FailFailureWrite"); }
		(await store.TryRecordFailureAsync(grant.Lease, retrying, Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(grant.Lease, Ct)).ShouldBeTrue();
		var exhausted = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, 999, Ct)).ShouldNotBeNull();
		exhausted.Lease.AdmissionAttempt.ShouldBe(2);
		await ExecuteAsync("CREATE TRIGGER dispatch.FailQuarantineWrite ON dispatch.ScheduleOccurrenceFailure AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE Disposition=1) THROW 51002, 'controlled eligible quarantine rejection', 1; END;");
		try
		{
			var rejectedQuarantine = await Should.ThrowAsync<SqlException>(() => store.TryRecordFailureAsync(exhausted.Lease, terminal, Ct));
			rejectedQuarantine.Number.ShouldBe(51002);
			rejectedQuarantine.Message.ShouldContain("controlled eligible quarantine rejection");
			var unchanged = (await NewStore().GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
			unchanged.Disposition.ShouldBe(ScheduleFailureDisposition.Retrying);
			(await store.GetAllAsync(Ct)).Single().Enabled.ShouldBeTrue();
		}
		finally { await ExecuteAsync("DROP TRIGGER dispatch.FailQuarantineWrite"); }
		(await store.TryRecordFailureAsync(exhausted.Lease, terminal, Ct)).ShouldBeTrue();
		var retained = (await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
		var command = Guid.NewGuid();
		await ExecuteAsync("CREATE TRIGGER dispatch.FailRecoveryWrite ON dispatch.ScheduleOccurrenceFailure AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE RecoveryCommandId IS NOT NULL) THROW 51001, 'controlled recovery persistence rejection', 1; END;");
		try
		{
			var rejectedRecovery = await Should.ThrowAsync<SqlException>(() => store.TryResumeQuarantinedOccurrenceAsync(retained.Token, command, Ct));
			rejectedRecovery.Number.ShouldBe(51001);
			rejectedRecovery.Message.ShouldContain("controlled recovery persistence rejection");
			var unchanged = (await NewStore().GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
			unchanged.Token.ShouldBe(retained.Token);
			unchanged.RecoveryReceipt.ShouldBeNull();
			var current = (await store.GetExecutionSnapshotsAsync(Ct)).Single();
			current.Generation.ShouldBe(generation);
			current.Message.Enabled.ShouldBeFalse();
		}
		finally { await ExecuteAsync("DROP TRIGGER dispatch.FailRecoveryWrite"); }
		(await NewStore().TryResumeQuarantinedOccurrenceAsync(retained.Token, command, Ct)).ShouldNotBeNull();
	}

	[Fact]
	public async Task QuarantineARecoveredUnadmittedCycleWithoutErasingItsZeroCountOrBudget()
	{
		var store = NewStore();
		var message = Message("admin");
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var first = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 2, Ct)).ShouldNotBeNull();
		(await store.TryRecordFailureAsync(first.Lease, new(ScheduleFailureReason.MalformedPayload, ScheduleFailureDisposition.Retrying), Ct)).ShouldBeTrue();
		await store.CompleteAsync(message.Id, Ct);
		var original = (await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, "admin", Ct)).ShouldNotBeNull();
		var command = Guid.NewGuid();
		var resumed = (await store.TryResumeQuarantinedOccurrenceAsync(original.Token, command, Ct)).ShouldNotBeNull();
		await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(message, Ct));
		await store.CompleteAsync(message.Id, Ct);
		var cancelled = (await NewStore().GetOccurrenceFailureAsync(message.Id, resumed.FreshGeneration, resumed.OccurrenceUtc, "admin", Ct)).ShouldNotBeNull();
		cancelled.AdmissionAttempts.ShouldBe(0);
		cancelled.MaxAdmissionAttempts.ShouldBe(2);
		cancelled.Reason.ShouldBe(ScheduleFailureReason.AdministrativeCancellation);
		cancelled.Message.MessageBody.ShouldBe(message.MessageBody);
		cancelled.Token.Generation.ShouldBe(resumed.FreshGeneration);
		await store.CompleteAsync(message.Id, Ct);
		var repeat = (await store.GetOccurrenceFailureAsync(message.Id, resumed.FreshGeneration, resumed.OccurrenceUtc, "admin", Ct)).ShouldNotBeNull();
		repeat.Token.ShouldBe(cancelled.Token);
		repeat.RecordedUtc.ShouldBe(cancelled.RecordedUtc);
		(await store.TryResumeQuarantinedOccurrenceAsync(original.Token, command, Ct)).ShouldBe(resumed);
		var second = (await NewStore().TryResumeQuarantinedOccurrenceAsync(cancelled.Token, Guid.NewGuid(), Ct)).ShouldNotBeNull();
		var grant = (await store.TryAcquireExecutionAsync(message.Id, second.FreshGeneration, second.OccurrenceUtc, 999, Ct)).ShouldNotBeNull();
		grant.CapturedMaxAdmissionAttempts.ShouldBe(2);
		grant.PolicyDispositionAllowed.ShouldBeFalse();
	}

	[Fact]
	public async Task PreserveTheKnownFailureCauseWhenBudgetQuarantineAndSameOwnerReplaysOccur()
	{
		var store = NewStore();
		var message = Message(null);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var first = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
		(await store.TryRecordFailureAsync(first.Lease, new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(first.Lease, Ct)).ShouldBeTrue();
		var exhausted = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, 999, Ct)).ShouldNotBeNull();
		var budget = new ScheduleOccurrenceFailure(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined);
		(await store.TryRecordFailureAsync(exhausted.Lease, budget, Ct)).ShouldBeTrue();
		var final = (await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
		final.Reason.ShouldBe(ScheduleFailureReason.HandlerFailed);
		final.AdmissionAttempts.ShouldBe(2);
		(await store.TryRecordFailureAsync(exhausted.Lease, budget, Ct)).ShouldBeTrue();
		(await store.TryRecordFailureAsync(exhausted.Lease, budget with { Reason = ScheduleFailureReason.HandlerFailed }, Ct)).ShouldBeFalse();
		(await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct))!.Token.ShouldBe(final.Token);
	}

	[Fact]
	public async Task RoundTripExplicitCronProfileThroughRetainedFailureAndRecovery()
	{
		var store = NewStore();
		var message = Message(null);
		message.CronExpression = "* * * * *";
		message.TimeZoneId = TransitionZoneId();
		message.CronRecurrenceProfileId = CronRecurrenceProfile.CronosCompatible.Id;
		message.CronRecurrenceProfileVersion = CronRecurrenceProfile.CronosCompatible.Version;
		await store.StoreAsync(message, Ct);
		var snapshot = (await NewStore().GetExecutionSnapshotsAsync(Ct)).Single();
		AssertProfile(snapshot.Message, message.TimeZoneId, message.CronRecurrenceProfileId, message.CronRecurrenceProfileVersion);
		snapshot.Message.CronRecurrenceProfileId = "mutated-copy";
		var first = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
		(await store.TryRecordFailureAsync(first.Lease, new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(first.Lease, Ct)).ShouldBeTrue();
		var exhausted = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc.Value, 999, Ct)).ShouldNotBeNull();
		(await store.TryRecordFailureAsync(exhausted.Lease, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), Ct)).ShouldBeTrue();
		var history = (await NewStore().GetOccurrenceFailureAsync(message.Id, snapshot.Generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
		AssertProfile(history.Message, message.TimeZoneId, message.CronRecurrenceProfileId, message.CronRecurrenceProfileVersion);
		var receipt = (await store.TryResumeQuarantinedOccurrenceAsync(history.Token, Guid.NewGuid(), Ct)).ShouldNotBeNull();
		var recovered = (await NewStore().GetExecutionSnapshotsAsync(Ct)).Single();
		recovered.Generation.ShouldBe(receipt.FreshGeneration);
		AssertProfile(recovered.Message, message.TimeZoneId, message.CronRecurrenceProfileId, message.CronRecurrenceProfileVersion);
	}

	[Theory]
	[InlineData(null, null, true, true)]
	[InlineData("cronos-compatible", null, true, false)]
	[InlineData(null, 1, true, false)]
	[InlineData("unknown", 1, true, false)]
	[InlineData("cronos-compatible", 2, true, false)]
	[InlineData("unknown", 1, false, false)]
	public async Task RefuseInvalidStoreConfigurationBeforeReplacingCurrentState(string? profileId, int? version, bool cron, bool transition)
	{
		var store = NewStore();
		var original = Message(null);
		await store.StoreAsync(original, Ct);
		var before = (await store.GetExecutionSnapshotsAsync(Ct)).Single();
		var replacement = Message(null);
		replacement.Id = original.Id;
		replacement.MessageBody = "must-not-replace";
		replacement.CronExpression = cron ? "* * * * *" : null!;
		replacement.TimeZoneId = transition ? TransitionZoneId() : TimeZoneInfo.Utc.Id;
		replacement.CronRecurrenceProfileId = profileId;
		replacement.CronRecurrenceProfileVersion = version;
		await Should.ThrowAsync<ArgumentException>(() => store.StoreAsync(replacement, Ct));
		var after = (await NewStore().GetExecutionSnapshotsAsync(Ct)).Single();
		after.Generation.ShouldBe(before.Generation);
		after.Message.MessageBody.ShouldBe(original.MessageBody);
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(true, 0)]
	[InlineData(false, 1)]
	[InlineData(true, 1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	public async Task RefuseRawLegacyInvalidCronOnFirstAdmissionWithoutMutation(bool budgeted, int invalidConfiguration)
	{
		var store = NewStore();
		var message = Message(null);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var zone = invalidConfiguration == 0 ? TransitionZoneId() : "missing-installed-zone-for-regression";
		var profile = invalidConfiguration == 2 ? "unknown" : invalidConfiguration == 1 ? "cronos-compatible" : null;
		int? version = invalidConfiguration == 2 ? 99 : invalidConfiguration == 1 ? 1 : null;
		await SetRawCronAsync(message.Id, zone, profile, version);
		var before = await ReadRawAdmissionAsync(message.Id);
		if (budgeted)
		{
			await Should.ThrowAsync<ArgumentException>(() => store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 2, Ct));
		}
		else
		{
			await Should.ThrowAsync<ArgumentException>(() => store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, Ct));
		}
		(await ReadRawAdmissionAsync(message.Id)).ShouldBe(before);
		var retained = (await NewStore().GetExecutionSnapshotsAsync(Ct)).Single();
		AssertProfile(retained.Message, zone, profile, version);
	}

	[Fact]
	public async Task PreserveCapturedLegacyOccurrenceThroughRetryRecoveryAndSettlementButRejectItsFutureAdmission()
	{
		var store = NewStore();
		var message = Message(null);
		message.NextExecutionUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var first = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
		await SetRawCronAsync(message.Id, "missing-installed-zone-for-regression", "legacy-unknown", 17);
		(await ((IScheduleExecutionLeaseRenewal)store).TryRenewExecutionAsync(first.Lease, Ct)).ShouldBeTrue();
		(await store.TryRecordFailureAsync(first.Lease, new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(first.Lease, Ct)).ShouldBeTrue();
		var exhausted = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc.Value, 999, Ct)).ShouldNotBeNull();
		exhausted.Lease.AdmissionAttempt.ShouldBe(2);
		(await store.TryRecordFailureAsync(exhausted.Lease, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), Ct)).ShouldBeTrue();
		var history = (await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
		AssertProfile(history.Message, "missing-installed-zone-for-regression", "legacy-unknown", 17);
		var receipt = (await store.TryResumeQuarantinedOccurrenceAsync(history.Token, Guid.NewGuid(), Ct)).ShouldNotBeNull();
		var recovered = (await store.TryAcquireExecutionAsync(message.Id, receipt.FreshGeneration, receipt.OccurrenceUtc, 999, Ct)).ShouldNotBeNull();
		recovered.CapturedMaxAdmissionAttempts.ShouldBe(1);
		(await ((IScheduleExecutionLeaseRenewal)store).TryRenewExecutionAsync(recovered.Lease, Ct)).ShouldBeTrue();
		var future = receipt.OccurrenceUtc.AddMinutes(1);
		(await store.TryCompleteExecutionAsync(recovered.Lease, new(DateTimeOffset.UtcNow, future), ScheduleExecutionCompletionDisposition.Fulfilled, Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(recovered.Lease, Ct)).ShouldBeTrue();
		var before = await ReadRawAdmissionAsync(message.Id);
		await Should.ThrowAsync<ArgumentException>(() => store.TryAcquireExecutionAsync(message.Id, receipt.FreshGeneration, future, 1, Ct));
		(await ReadRawAdmissionAsync(message.Id)).ShouldBe(before);
		(await store.GetOccurrenceFailureAsync(message.Id, generation, message.NextExecutionUtc.Value, null, Ct)).ShouldNotBeNull();
	}

	[Fact]
	public async Task SettleCapturedLegacyInvalidConfigurationAsFulfilledAndDisabledWithoutReinterpretingIt()
	{
		var store = NewStore();
		var message = Message(null);
		await store.StoreAsync(message, Ct);
		var generation = (await store.GetExecutionSnapshotsAsync(Ct)).Single().Generation;
		var admission = (await store.TryAcquireExecutionAsync(message.Id, generation, message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
		await SetRawCronAsync(message.Id, "missing-installed-zone-for-regression", "legacy-unknown", 17);
		(await store.TryCompleteExecutionAsync(admission.Lease, new(DateTimeOffset.UtcNow, null, true), ScheduleExecutionCompletionDisposition.Fulfilled, Ct)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(admission.Lease, Ct)).ShouldBeTrue();
		var retained = (await NewStore().GetExecutionSnapshotsAsync(Ct)).Single();
		retained.Message.Enabled.ShouldBeFalse();
		retained.Message.NextExecutionUtc.ShouldBeNull();
		retained.Message.LastExecutionUtc.ShouldNotBeNull();
		AssertProfile(retained.Message, "missing-installed-zone-for-regression", "legacy-unknown", 17);
		(await ReadRawAdmissionAsync(message.Id)).Budget.ShouldBeNull();
	}

	[Fact]
	public async Task PreserveOpaqueNonCronZoneAndAllowUnqualifiedUtcCron()
	{
		var store = NewStore();
		var nonCron = Message(null);
		nonCron.TimeZoneId = "unresolved-but-preserved";
		await store.StoreAsync(nonCron, Ct);
		var utcCron = Message(null);
		utcCron.CronExpression = "* * * * *";
		utcCron.TimeZoneId = null;
		await store.StoreAsync(utcCron, Ct);
		foreach (var snapshot in await store.GetExecutionSnapshotsAsync(Ct))
		{
			var admission = (await store.TryAcquireExecutionAsync(snapshot.Message.Id, snapshot.Generation, snapshot.Message.NextExecutionUtc!.Value, 1, Ct)).ShouldNotBeNull();
			(await store.ReleaseExecutionAsync(admission.Lease, Ct)).ShouldBeTrue();
		}
		(await NewStore().GetExecutionSnapshotsAsync(Ct)).Single(x => x.Message.Id == nonCron.Id).Message.TimeZoneId.ShouldBe(nonCron.TimeZoneId);
	}

	private static void AssertProfile(IScheduledMessage message, string? zone, string? profileId, int? version)
	{
		message.TimeZoneId.ShouldBe(zone);
		message.CronRecurrenceProfileId.ShouldBe(profileId);
		message.CronRecurrenceProfileVersion.ShouldBe(version);
	}

	private static string TransitionZoneId()
	{
		try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York").Id; }
		catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").Id; }
	}

	private async Task SetRawCronAsync(Guid id, string zone, string? profileId, int? version)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct);
		await using var command = new SqlCommand("UPDATE dispatch.ScheduledMessage SET CronExpression=N'* * * * *',TimeZoneId=@Zone,CronRecurrenceProfileId=@Profile,CronRecurrenceProfileVersion=@Version WHERE Id=@Id", connection);
		command.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier).Value = id;
		command.Parameters.Add("@Zone", System.Data.SqlDbType.NVarChar, -1).Value = zone;
		command.Parameters.Add("@Profile", System.Data.SqlDbType.NVarChar, -1).Value = (object?)profileId ?? DBNull.Value;
		command.Parameters.Add("@Version", System.Data.SqlDbType.Int).Value = (object?)version ?? DBNull.Value;
		(await command.ExecuteNonQueryAsync(Ct)).ShouldBe(1);
	}

	private async Task<(Guid Generation, int Attempts, int? Budget, Guid? Owner)> ReadRawAdmissionAsync(Guid id)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct);
		await using var command = new SqlCommand("SELECT Generation,AdmissionAttempts,CapturedMaxAdmissionAttempts,OwnerToken FROM dispatch.ScheduledMessage WHERE Id=@Id", connection);
		command.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier).Value = id;
		await using var reader = await command.ExecuteReaderAsync(Ct);
		(await reader.ReadAsync(Ct)).ShouldBeTrue();
		return (reader.GetGuid(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetGuid(3));
	}

	protected override IScheduleExecutionFailureStore CreateStore() => NewStore();

	[Fact]
	public override Task CapturedBudgetCannotBeWidenedOrErased() => base.CapturedBudgetCannotBeWidenedOrErased();

	[Fact]
	public override Task QuarantineRecoveryPreservesHistoryAndDeduplicates() => base.QuarantineRecoveryPreservesHistoryAndDeduplicates();

	[Fact]
	public override Task UnsettledWorkAndTenantInventoryRemainProtected() => base.UnsettledWorkAndTenantInventoryRemainProtected();

	[Fact]
	public override Task FailureMutationRequiresCurrentUncancelledAuthority() => base.FailureMutationRequiresCurrentUncancelledAuthority();

	[Fact]
	public Task ExposeEveryFailureCapabilityArmToRunner() => ConformanceSuite_ShouldWireEveryArm();

	private IScheduleExecutionFailureStore NewStore() => new SqlServerScheduleStore(global::Microsoft.Extensions.Options.Options.Create(new SqlServerScheduleStoreOptions
	{
		ConnectionString = _connectionString, LeaseDuration = TimeSpan.FromSeconds(60),
	})).ShouldBeAssignableTo<IScheduleExecutionFailureStore>();
	private static ScheduledMessage Message(string? tenant) => new()
	{
		Id = Guid.NewGuid(), MessageName = "FailureDocument", MessageBody = "opaque\noriginal-payload", TenantId = tenant,
		NextExecutionUtc = DateTimeOffset.UtcNow.AddMinutes(-1), Enabled = true, Interval = TimeSpan.FromHours(1),
	};
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Only internal shipped embedded schema text is accepted by this private fixture helper.")]
	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct);
		await using var command = new SqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync(Ct);
	}
}
