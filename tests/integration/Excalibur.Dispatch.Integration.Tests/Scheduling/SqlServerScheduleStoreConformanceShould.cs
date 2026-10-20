// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;

using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Scheduling;

/// <summary>Exercises the shipped SQL schema and renewable authority against real SQL Server.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "STORE")]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Inherited conformance arm naming.")]
public sealed class SqlServerScheduleStoreConformanceShould(SqlServerFixture fixture)
	: ScheduleExecutionLeaseRenewalConformanceTestKit, IAsyncLifetime
{
	private readonly string _database = "schedule_" + Guid.NewGuid().ToString("N");
	private string _connectionString = null!;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <inheritdoc />
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Database identifier is an internally generated GUID; schema is the package's embedded shipped script.")]
	public async ValueTask InitializeAsync()
	{
		await using (var master = new SqlConnection(fixture.ConnectionString))
		{
			await master.OpenAsync(Ct).ConfigureAwait(false);
			await using var create = new SqlCommand($"CREATE DATABASE [{_database}]", master) { CommandTimeout = 30 };
			_ = await create.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
		}
		_connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = _database }.ConnectionString;
		var assembly = typeof(SqlServerScheduleStoreConformanceShould).Assembly;
		var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("SqlServer.004_CreateScheduleSchema.sql", StringComparison.Ordinal));
		await using var stream = assembly.GetManifestResourceStream(resource)
			?? throw new InvalidOperationException("The shipped schedule schema is not embedded.");
		using var text = new StreamReader(stream);
		var script = await text.ReadToEndAsync(Ct).ConfigureAwait(false);
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct).ConfigureAwait(false);
		await using var command = new SqlCommand(script, connection) { CommandTimeout = 30 };
		_ = await command.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Database identifier is an internally generated GUID, never caller input.")]
	public async ValueTask DisposeAsync()
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await using var master = new SqlConnection(fixture.ConnectionString);
		await master.OpenAsync(deadline.Token).ConfigureAwait(false);
		await using var command = new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", master)
		{ CommandTimeout = 30 };
		_ = await command.ExecuteNonQueryAsync(deadline.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override IScheduleExecutionLeaseRenewal CreateStore() => NewStore();

	/// <inheritdoc />
	protected override async Task ResetDataAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct).ConfigureAwait(false);
		await using var command = new SqlCommand("DELETE FROM dispatch.ScheduledMessage", connection) { CommandTimeout = 30 };
		_ = await command.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("__untenanted__")]
	[InlineData("Tenant-A")]
	public async Task PreserveAllFourteenFieldsWithoutCoercingEnvelopeMetadata(string? tenant)
	{
		var store = NewStore();
		var message = new ScheduledMessage
		{
			Id = Guid.NewGuid(), MessageName = "Document", MessageBody = "opaque\nnot JSON",
			CronExpression = null!, TimeZoneId = "unresolved-but-preserved", Interval = TimeSpan.FromTicks(long.MinValue),
			NextExecutionUtc = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(5)).AddTicks(7),
			LastExecutionUtc = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.FromHours(-7)).AddTicks(3),
			Enabled = false, MissedExecutionBehavior = (MissedExecutionBehavior)int.MaxValue,
			CorrelationId = "", TraceParent = "trace\"\n", TenantId = tenant, UserId = "用户",
		};
		await store.StoreAsync(message, Ct).ConfigureAwait(false);
		var actual = (await NewStore().GetAllAsync(Ct).ConfigureAwait(false)).Single();
		actual.Id.ShouldBe(message.Id);
		actual.MessageName.ShouldBe(message.MessageName);
		actual.MessageBody.ShouldBe(message.MessageBody);
		actual.CronExpression.ShouldBeNull();
		actual.TimeZoneId.ShouldBe(message.TimeZoneId);
		actual.Interval.ShouldBe(message.Interval);
		actual.NextExecutionUtc!.Value.EqualsExact(message.NextExecutionUtc!.Value).ShouldBeTrue();
		actual.LastExecutionUtc!.Value.EqualsExact(message.LastExecutionUtc!.Value).ShouldBeTrue();
		actual.Enabled.ShouldBeFalse();
		actual.MissedExecutionBehavior.ShouldBe(message.MissedExecutionBehavior);
		actual.CorrelationId.ShouldBe(message.CorrelationId);
		actual.TraceParent.ShouldBe(message.TraceParent);
		actual.TenantId.ShouldBe(tenant);
		actual.UserId.ShouldBe(message.UserId);
	}

	[Fact]
	public async Task LockAndWriteTheSameDetachedIdentifierWithoutReadingCallerAgain()
	{
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		var reads = 0;
		var message = A.Fake<IScheduledMessage>();
		A.CallTo(() => message.Id).ReturnsLazily(() => ++reads == 1 ? first : second);
		A.CallTo(() => message.MessageName).Returns("Action");
		A.CallTo(() => message.MessageBody).Returns("opaque");
		A.CallTo(() => message.Enabled).Returns(true);
		await NewStore().StoreAsync(message, Ct).ConfigureAwait(false);
		reads.ShouldBe(1, "Caller identity must be read only when taking the detached snapshot.");
		(await NewStore().GetAllAsync(Ct).ConfigureAwait(false)).Single().Id.ShouldBe(first);
	}

	[Fact]
	public async Task SampleExpiryAfterWaitingForTheAuthorityRowLock()
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		var token = deadline.Token;
		var store = NewStore();
		var message = Due();
		await store.StoreAsync(message, token).ConfigureAwait(false);
		var snapshot = (await store.GetExecutionSnapshotsAsync(token).ConfigureAwait(false)).Single();
		var lease = await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, token).ConfigureAwait(false);
		lease.ShouldNotBeNull();
		await using var blocker = new SqlConnection(_connectionString);
		await blocker.OpenAsync(token).ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync(token).ConfigureAwait(false);
		await using var lockRow = new SqlCommand("SELECT @@SPID; SELECT Id FROM dispatch.ScheduledMessage WITH (UPDLOCK,HOLDLOCK) WHERE Id=@Id", blocker, transaction);
		lockRow.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = message.Id;
		int blockerId;
		await using (var reader = await lockRow.ExecuteReaderAsync(token).ConfigureAwait(false))
		{
			(await reader.ReadAsync(token).ConfigureAwait(false)).ShouldBeTrue();
			blockerId = reader.GetInt16(0);
			(await reader.NextResultAsync(token).ConfigureAwait(false)).ShouldBeTrue();
			(await reader.ReadAsync(token).ConfigureAwait(false)).ShouldBeTrue();
		}
		var pending = store.TryRenewExecutionAsync(lease, token);
		try
		{
			await using var observer = new SqlConnection(_connectionString);
			await observer.OpenAsync(token).ConfigureAwait(false);
			await using var waiting = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Blocker AND database_id=DB_ID()", observer);
			waiting.Parameters.Add("@Blocker", SqlDbType.Int).Value = blockerId;
			using var admissionDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			admissionDeadline.CancelAfter(TimeSpan.FromSeconds(5));
			while ((int)(await waiting.ExecuteScalarAsync(admissionDeadline.Token).ConfigureAwait(false))! == 0)
			{
				await Task.Delay(20, admissionDeadline.Token).ConfigureAwait(false); // delay-ok: poll pacing; the loop exits on the row being admitted, bounded by admissionDeadline
			}
			// The blocker already owns this row: query through its transaction so the proof
			// cannot itself wait behind the authority lock. A slow setup must fail, not
			// accidentally certify a provider that sampled an already-expired grant.
			await using var stillLive = new SqlCommand(
				"SELECT CAST(CASE WHEN LeaseExpiresUtc > TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00') THEN 1 ELSE 0 END AS bit) FROM dispatch.ScheduledMessage WHERE Id=@Id",
				blocker, transaction) { CommandTimeout = 5 };
			stillLive.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = message.Id;
			((bool)(await stillLive.ExecuteScalarAsync(token).ConfigureAwait(false))!).ShouldBeTrue(
				"The original DB grant must still be live after the renewal RPC is observed waiting on its row lock.");
			// An actual SQL wait and a still-live DB grant are observed before expiry.
			await Task.Delay(TimeSpan.FromMilliseconds(2250), token).ConfigureAwait(false); // delay-ok: the lease TTL must actually elapse -- that expiry IS the behaviour under test, not a wait for a background task
		}
		finally
		{
			await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
		}
		(await pending.ConfigureAwait(false)).ShouldBeFalse("A time sample taken before lock waiting would resurrect this expired grant.");
		var successor = await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
		successor.ShouldNotBeNull();
		successor.OwnerToken.ShouldNotBe(lease.OwnerToken);
		(await store.ReleaseExecutionAsync(lease, token).ConfigureAwait(false)).ShouldBeFalse();
		(await store.TryRenewExecutionAsync(successor, token).ConfigureAwait(false)).ShouldBeTrue();
	}

	[Fact]
	public async Task PreserveLogicalAuthorityAcrossProviderInstances()
	{
		var first = NewStore();
		var message = Due();
		await first.StoreAsync(message, Ct).ConfigureAwait(false);
		var snapshot = (await first.GetExecutionSnapshotsAsync(Ct).ConfigureAwait(false)).Single();
		var lease = await first.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, Ct).ConfigureAwait(false);
		lease.ShouldNotBeNull();
		var second = NewStore();
		(await second.TryRenewExecutionAsync(lease, Ct).ConfigureAwait(false)).ShouldBeTrue();
		(await second.TryAcquireExecutionAsync(message.Id, snapshot.Generation, lease.OccurrenceUtc, Ct).ConfigureAwait(false)).ShouldBeNull();
		(await second.ReleaseExecutionAsync(lease, Ct).ConfigureAwait(false)).ShouldBeTrue();
		var successor = await second.TryAcquireExecutionAsync(message.Id, snapshot.Generation, lease.OccurrenceUtc, Ct).ConfigureAwait(false);
		successor.ShouldNotBeNull();
		successor.StoreId.ShouldBe(lease.StoreId);
		successor.OwnerToken.ShouldNotBe(lease.OwnerToken);
	}

	[Fact]
	public async Task RejectAmbientTransactionsBeforeOpeningAuthority()
	{
		using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
		await Should.ThrowAsync<InvalidOperationException>(() => NewStore().StoreAsync(Due(), Ct)).ConfigureAwait(false);
		await Should.ThrowAsync<InvalidOperationException>(() => NewStore().GetAllAsync(Ct)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RejectExpiryOverflowWithoutClaimingTheRow()
	{
		var store = NewStore();
		var message = Due();
		await store.StoreAsync(message, Ct).ConfigureAwait(false);
		var snapshot = (await store.GetExecutionSnapshotsAsync(Ct).ConfigureAwait(false)).Single();
		var huge = NewStore(TimeSpan.MaxValue);
		await Should.ThrowAsync<InvalidOperationException>(() => huge.TryAcquireExecutionAsync(message.Id, snapshot.Generation,
			message.NextExecutionUtc!.Value, Ct)).ConfigureAwait(false);
		(await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, Ct).ConfigureAwait(false)).ShouldNotBeNull();
	}

	[Fact]
	public async Task SupportLongGrantsWithoutSqlDateAddIntegerLimits()
	{
		var store = NewStore(TimeSpan.FromDays(100));
		var message = Due();
		await store.StoreAsync(message, Ct).ConfigureAwait(false);
		var snapshot = (await store.GetExecutionSnapshotsAsync(Ct).ConfigureAwait(false)).Single();
		var lease = await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, Ct).ConfigureAwait(false);
		lease.ShouldNotBeNull();
		(await store.TryRenewExecutionAsync(lease, Ct).ConfigureAwait(false)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(lease, Ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	[Fact]
	public async Task RotateOfflineEpochAndRejectEveryOldBearer()
	{
		var store = NewStore();
		var message = Due();
		await store.StoreAsync(message, Ct).ConfigureAwait(false);
		var snapshot = (await store.GetExecutionSnapshotsAsync(Ct).ConfigureAwait(false)).Single();
		var old = await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, Ct).ConfigureAwait(false);
		old.ShouldNotBeNull();
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(Ct).ConfigureAwait(false);
		await using var rotation = new SqlCommand("SET XACT_ABORT ON; BEGIN TRANSACTION; UPDATE dispatch.ScheduleAuthority WITH (UPDLOCK,HOLDLOCK) SET StoreId=NEWID() WHERE SingletonId=1; UPDATE dispatch.ScheduledMessage SET OwnerToken=NULL, AdmittedOccurrence=NULL, LeaseExpiresUtc=NULL; COMMIT;", connection);
		_ = await rotation.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
		(await store.TryRenewExecutionAsync(old, Ct).ConfigureAwait(false)).ShouldBeFalse();
		(await store.TryUpdateProgressAsync(old, new(null, null, true), Ct).ConfigureAwait(false)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(old, Ct).ConfigureAwait(false)).ShouldBeFalse();
		var fresh = await NewStore().TryAcquireExecutionAsync(message.Id, snapshot.Generation, old.OccurrenceUtc, Ct).ConfigureAwait(false);
		fresh.ShouldNotBeNull();
		fresh.StoreId.ShouldNotBe(old.StoreId);
		(await store.TryRenewExecutionAsync(fresh, Ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterTheSameConfiguredDurableAuthority()
	{
		var services = new ServiceCollection();
		services.AddDispatchScheduling();
		services.AddSqlServerScheduleStore(options => options.ConnectionString = _connectionString);
		using var provider = services.BuildServiceProvider();
		var configured = provider.GetRequiredService<IScheduleStore>();
		configured.ShouldBeOfType<SqlServerScheduleStore>();
		configured.ShouldBeSameAs(provider.GetRequiredService<IScheduleStore>());
		configured.ShouldBeAssignableTo<IScheduleExecutionLeaseRenewal>();
	}

	private SqlServerScheduleStore NewStore(TimeSpan? duration = null) => new(Microsoft.Extensions.Options.Options.Create(new SqlServerScheduleStoreOptions
	{
		ConnectionString = _connectionString, LeaseDuration = duration ?? TimeSpan.FromSeconds(2),
		CommandTimeoutSeconds = 15, LockTimeoutMilliseconds = 10_000,
	}));

	private static ScheduledMessage Due() => new()
	{
		MessageName = "Action", MessageBody = "{}", NextExecutionUtc = DateTimeOffset.UtcNow, Enabled = true,
	};

	[Fact]
	public override Task StoreAsync_ShouldPersistMessage() => base.StoreAsync_ShouldPersistMessage();

	[Fact]
	public override Task StoreAsync_WithNullMessage_ShouldThrow() => base.StoreAsync_WithNullMessage_ShouldThrow();

	[Fact]
	public override Task StoreAsync_SameId_ShouldUpsert() => base.StoreAsync_SameId_ShouldUpsert();

	[Fact]
	public override Task StoreAsync_MultipleMessages_ShouldPersistAll() => base.StoreAsync_MultipleMessages_ShouldPersistAll();

	[Fact]
	public override Task GetAllAsync_EmptyStore_ShouldReturnEmpty() => base.GetAllAsync_EmptyStore_ShouldReturnEmpty();

	[Fact]
	public override Task GetAllAsync_AfterStore_ShouldReturnMessage() => base.GetAllAsync_AfterStore_ShouldReturnMessage();

	[Fact]
	public override Task GetAllAsync_ShouldReturnAllMessages() => base.GetAllAsync_ShouldReturnAllMessages();

	[Fact]
	public override Task CompleteAsync_ShouldSetEnabledFalse() => base.CompleteAsync_ShouldSetEnabledFalse();

	[Fact]
	public override Task CompleteAsync_NonExistent_ShouldBeIdempotent() => base.CompleteAsync_NonExistent_ShouldBeIdempotent();

	[Fact]
	public override Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent() => base.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();

	[Fact]
	public override Task StoreAsync_ThenComplete_MessageRemainsPersisted() => base.StoreAsync_ThenComplete_MessageRemainsPersisted();

	[Fact]
	public override Task MultipleMessages_CompleteOne_OthersUnaffected() => base.MultipleMessages_CompleteOne_OthersUnaffected();

	[Fact]
	public override Task StoreInputRemainsDetached() => base.StoreInputRemainsDetached();

	[Fact]
	public override Task ReturnedSnapshotsRemainDetached() => base.ReturnedSnapshotsRemainDetached();

	[Fact]
	public override Task SimultaneousAdmissionHasOneWinner() => base.SimultaneousAdmissionHasOneWinner();

	[Fact]
	public override Task AdministrativeChangesInvalidateOldAuthority() => base.AdministrativeChangesInvalidateOldAuthority();

	[Fact]
	public override Task ProgressAndReleaseRespectCurrentAuthority() => base.ProgressAndReleaseRespectCurrentAuthority();

	[Fact]
	public override Task CanceledOperationsLeaveAuthorityUnchanged() => base.CanceledOperationsLeaveAuthorityUnchanged();

	[Fact]
	public override Task RenewalExtendsCurrentOwnership() => base.RenewalExtendsCurrentOwnership();

	[Fact]
	public override Task ExpiredAuthorityCannotRenewOrProgress() => base.ExpiredAuthorityCannotRenewOrProgress();

	[Fact]
	public override Task ReleasedAndForeignAuthorityCannotRenewSuccessor() => base.ReleasedAndForeignAuthorityCannotRenewSuccessor();

	[Fact]
	public override Task AdministrativeMutationInvalidatesRenewal() => base.AdministrativeMutationInvalidatesRenewal();

	[Fact]
	public override Task RenewalPreservesProgressOnceIncludingAfterCommit() => base.RenewalPreservesProgressOnceIncludingAfterCommit();

	[Fact]
	public override Task CanceledRenewalLeavesOwnershipUnchanged() => base.CanceledRenewalLeavesOwnershipUnchanged();

	[Fact]
	public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();
}
