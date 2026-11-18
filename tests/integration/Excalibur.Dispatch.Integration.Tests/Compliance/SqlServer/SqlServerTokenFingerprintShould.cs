// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;
using System.Transactions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the guarantee that a changed data-subject token function is REFUSED rather than silently
/// orphaning every token-keyed record in the store — the SQL Server half.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms run against a real SQL Server instance and are never skipped.</b> The two properties
/// under test are both substrate behaviour that no mock reproduces: the duplicate-key number this store
/// swallows (2627 / 2601, and ONLY those, so a transient fault cannot pass silently), and the trigger
/// that refuses a rewrite of a recorded fingerprint. A mocked connection would report PASS over a
/// provider that does not work — which is the failure this suite's whole shape exists to prevent.
/// </para>
/// <para>
/// <b>It mirrors the PostgreSQL suite deliberately.</b> The two providers implement the same contract and
/// the identical interleaving must hold for both; a property proven on one substrate says nothing about
/// the other, because what is being proven IS the substrate's behaviour.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerTokenFingerprintShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerTokenFingerprintShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task RefuseToStartWhenTheHashingKeyChangedBetweenTwoRuns()
	{
		var tables = TableSet();

		using (var first = CreateStore(tables, KeyedHasher("key-one")))
		{
			_ = await first.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var second = CreateStore(tables, KeyedHasher("key-two"));

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => second.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

		thrown.Message.ShouldContain(
			nameof(DataSubjectHashingOptions.Pepper),
			Case.Sensitive,
			$"the refusal must name the setting to restore. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartOnEveryRunWhenTheHashingKeyIsUnchanged()
	{
		// LIVENESS: without this, the arm above is satisfied by a store that refuses every startup.
		var tables = TableSet();

		using (var first = CreateStore(tables, KeyedHasher("stable-key")))
		{
			_ = await first.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var second = CreateStore(tables, KeyedHasher("stable-key"));

		var status = await second.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);

		status.ShouldBeNull("nothing was saved under this id, so a successful read returns no result.");
	}

	[Fact]
	public async Task RefuseAnAttemptToREWRITETheRecordedFingerprint()
	{
		// INVARIANT J, ENFORCED AT THE DATABASE. The claim is insert-if-absent then read, which is sound
		// only while the value can never change: otherwise a process with the WRONG key could overwrite
		// the record and then match it, while the process with the RIGHT key refused.
		var tables = TableSet();
		using (var store = CreateStore(tables, KeyedHasher("key-one")))
		{
			_ = await store.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var update = $"UPDATE [compliance].[{tables.Fingerprint}] SET [Fingerprint] = 'tampered'";
		_ = await Should.ThrowAsync<SqlException>(
			() => connection.ExecuteAsync(new CommandDefinition(update, cancellationToken: TestCancellationToken)));

		var delete = $"DELETE FROM [compliance].[{tables.Fingerprint}]";
		_ = await Should.ThrowAsync<SqlException>(
			() => connection.ExecuteAsync(new CommandDefinition(delete, cancellationToken: TestCancellationToken)));
	}

	[Fact]
	public async Task RecordExactlyOneFingerprintWhenTwoProcessesStartTogether()
	{
		// Two hosts with DIFFERENT keys racing to claim an unclaimed store. Exactly one may proceed.
		// This is the arm that proves the swallow is scoped to the duplicate-key numbers rather than
		// catching everything: a blanket catch would let both proceed.
		//
		// The schema is provisioned first and the racers do not auto-create, so this measures the claim's
		// atomicity rather than concurrent DDL. TRUNCATE returns the table to the unclaimed state that the
		// write-once trigger otherwise makes unreachable — AFTER triggers do not fire on TRUNCATE.
		var tables = TableSet();

		using (var provisioner = CreateStore(tables, KeyedHasher("provisioner")))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using (var setup = new SqlConnection(_fixture.ConnectionString))
		{
			await setup.OpenAsync(TestCancellationToken);
			_ = await setup.ExecuteAsync(new CommandDefinition(
				$"TRUNCATE TABLE [compliance].[{tables.Fingerprint}]",
				cancellationToken: TestCancellationToken));
		}

		using var one = CreateStore(tables, KeyedHasher("racer-one"), autoCreate: false);
		using var two = CreateStore(tables, KeyedHasher("racer-two"), autoCreate: false);

		var first = Task.Run(() => one.GetStatusAsync(Guid.NewGuid(), TestCancellationToken), TestCancellationToken);
		var second = Task.Run(() => two.GetStatusAsync(Guid.NewGuid(), TestCancellationToken), TestCancellationToken);

		var outcomes = await Task.WhenAll(Settle(first), Settle(second));

		outcomes.Count(succeeded => succeeded).ShouldBe(
			1,
			"exactly one of two processes with different hashing keys may proceed against an unclaimed " +
			"store; two winners means the claim is not atomic, and none means it is always-refuse.");
	}

	[Fact]
	public async Task ReportTheSameFingerprintWhateverTenantIsAmbient()
	{
		// The fingerprint carries NO tenant term. This reddens if a provider reintroduces one — which
		// would be worse than no check, recording a rotated key as correct for a tenant onboarded after
		// the rotation while every earlier tenant was already orphaned.
		var tables = TableSet();
		var hasher = KeyedHasher("one-key");

		using var underTenantA = CreateStore(tables, hasher, new NamedOwnerContext("tenant-a"));
		var claimed = await underTenantA.ClaimTokenFingerprintAsync("IGNORED-ALREADY-CLAIMED", TestCancellationToken);

		using var underTenantB = CreateStore(tables, hasher, new NamedOwnerContext("tenant-b"));
		var seenByB = await underTenantB.ClaimTokenFingerprintAsync("ALSO-IGNORED", TestCancellationToken);

		seenByB.ShouldBe(claimed, "the recorded fingerprint is a process fact, not a tenant fact.");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task RefuseExternallyProvisionedSchemaWithoutAnEnabledFingerprintWriteGuard(bool disableGuard)
	{
		var tables = TableSet();
		var hasher = KeyedHasher("stable-key");
		using (var provisioner = CreateStore(tables, hasher))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var alterGuard = disableGuard
			? $"DISABLE TRIGGER [compliance].[TR_{tables.Fingerprint}_WriteOnce] ON [compliance].[{tables.Fingerprint}]"
			: $"DROP TRIGGER [compliance].[TR_{tables.Fingerprint}_WriteOnce]";
		_ = await connection.ExecuteAsync(new CommandDefinition(alterGuard, cancellationToken: TestCancellationToken));

		try
		{
			using var restarted = CreateStore(tables, hasher, autoCreate: false);
			var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
				() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

			thrown.TableName.ShouldBe($"[compliance].[{tables.Fingerprint}]");
			thrown.Message.ShouldContain("write-once", Case.Insensitive);
		}
		finally
		{
			// Restore only this arm's unique table guard; use the production provisioner rather than a DDL mirror.
			using var repair = CreateStore(tables, hasher);
			_ = await repair.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var restored = CreateStore(tables, hasher, autoCreate: false);
		(await restored.GetStatusAsync(Guid.NewGuid(), TestCancellationToken)).ShouldBeNull(
			"restoring the write-once guard must allow the unchanged token function to initialize again");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task RefuseAFingerprintWriteGuardThatDoesNotCoverDeletes()
	{
		var tables = TableSet();
		var hasher = KeyedHasher("stable-key");
		using (var provisioner = CreateStore(tables, hasher))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var definition = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
			"SELECT OBJECT_DEFINITION(OBJECT_ID(@TriggerName, 'TR'))",
			new { TriggerName = $"[compliance].[TR_{tables.Fingerprint}_WriteOnce]" },
			cancellationToken: TestCancellationToken));
		definition.ShouldNotBeNullOrWhiteSpace();
		definition.ShouldContain("CREATE TRIGGER", Case.Sensitive);
		definition.ShouldContain("AFTER UPDATE, DELETE", Case.Sensitive);
		var narrowGuard = definition.Replace("CREATE TRIGGER", "ALTER TRIGGER", StringComparison.Ordinal)
			.Replace("AFTER UPDATE, DELETE", "AFTER UPDATE", StringComparison.Ordinal);
		_ = await connection.ExecuteAsync(new CommandDefinition(narrowGuard, cancellationToken: TestCancellationToken));

		try
		{
			using var restarted = CreateStore(tables, hasher, autoCreate: false);
			var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
				() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
			thrown.TableName.ShouldBe($"[compliance].[{tables.Fingerprint}]");
			thrown.Message.ShouldContain("write-once", Case.Insensitive);
		}
		finally
		{
			using var repair = CreateStore(tables, hasher);
			_ = await repair.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task InitializeDurablyOnFirstUseInsideAnAmbientTransactionThatRollsBack()
	{
		new SqlConnectionStringBuilder(_fixture.ConnectionString).Enlist.ShouldBeTrue(
			"the regression requires normal automatic enlistment rather than an unenlisted test connection");
		var tables = TableSet();
		var owner = new NamedOwnerContext("ambient-owner");
		using var store = CreateStore(tables, KeyedHasher("stable-key"), owner);
		var request = new ErasureRequest
		{
			DataSubjectId = "subject-in-business-transaction",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "ambient-transaction-test",
		};
		string? claimedInside = null;

		using (new TransactionScope(
			TransactionScopeOption.RequiresNew,
			new TransactionOptions
			{
				IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
				Timeout = TimeSpan.FromSeconds(30),
			},
			TransactionScopeAsyncFlowOption.Enabled))
		{
			var ambient = Transaction.Current.ShouldNotBeNull();
			(await store.GetStatusAsync(request.RequestId, TestCancellationToken)).ShouldBeNull();
			Transaction.Current.ShouldBeSameAs(ambient, "initialization must restore the caller's ambient scope");
			await store.SaveRequestAsync(request, DateTimeOffset.UtcNow, TestCancellationToken);
			(await store.GetStatusAsync(request.RequestId, TestCancellationToken)).ShouldNotBeNull();
			claimedInside = await store.ClaimTokenFingerprintAsync("IGNORED-ALREADY-CLAIMED", TestCancellationToken);
			claimedInside.ShouldNotBeNullOrWhiteSpace();
			Transaction.Current.ShouldBeSameAs(ambient, "ordinary store calls must retain the caller's transaction");
			// Deliberately no Complete: the caller's business transaction rolls back at the end of this block.
		}

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var enabledGuards = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			"SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(@GuardName, N'TR') "
			+ "AND parent_id = OBJECT_ID(@TableName, N'U') AND is_disabled = 0",
			new
			{
				GuardName = $"[compliance].[TR_{tables.Fingerprint}_WriteOnce]",
				TableName = $"[compliance].[{tables.Fingerprint}]",
			},
			cancellationToken: TestCancellationToken));
		enabledGuards.ShouldBe(1, "initialization's durable guard must not roll back with caller business work");
		var fingerprint = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
			$"SELECT [Fingerprint] FROM [compliance].[{tables.Fingerprint}]", cancellationToken: TestCancellationToken));
		fingerprint.ShouldNotBeNullOrWhiteSpace();
		fingerprint.ShouldBe(claimedInside);
		(await store.GetStatusAsync(request.RequestId, TestCancellationToken)).ShouldBeNull(
			"business work must roll back while cached initialization continues to describe durable schema");

		using var changedHasher = CreateStore(tables, KeyedHasher("rotated-key"), owner, autoCreate: false);
		var refused = await Should.ThrowAsync<InvalidOperationException>(
			() => changedHasher.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
		refused.Message.ShouldContain(nameof(DataSubjectHashingOptions.Pepper), Case.Sensitive);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task PreserveTheExistingWriteGuardWhenRecreationFails()
	{
		var tables = TableSet();
		var hasher = KeyedHasher("stable-key");
		using (var provisioner = CreateStore(tables, hasher))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var tableName = $"[compliance].[{tables.Fingerprint}]";
		var guardName = $"TR_{tables.Fingerprint}_WriteOnce";
		var faultTrigger = $"TfRecreationFault{Guid.NewGuid():N}";
		var originalFingerprint = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
			$"SELECT [Fingerprint] FROM {tableName}", cancellationToken: TestCancellationToken));
		originalFingerprint.ShouldNotBeNullOrWhiteSpace();
		await InstallRecreationFailureAsync(connection, faultTrigger, guardName);

		try
		{
			using var restarted = CreateStore(tables, hasher);
			var injected = await Should.ThrowAsync<SqlException>(
				() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
			injected.Errors.Cast<SqlError>().ShouldContain(error => error.Number == 51987);

			var enabledGuards = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
				"SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(@GuardName, N'TR') "
				+ "AND parent_id = OBJECT_ID(@TableName, N'U') AND is_disabled = 0",
				new { GuardName = $"[compliance].[{guardName}]", TableName = tableName },
				cancellationToken: TestCancellationToken));
			enabledGuards.ShouldBe(1, "failed guard recreation must roll back the preceding DROP");

			var update = await Should.ThrowAsync<SqlException>(() => connection.ExecuteAsync(new CommandDefinition(
				$"UPDATE {tableName} SET [Fingerprint] = 'tampered'", cancellationToken: TestCancellationToken)));
			update.Errors.Cast<SqlError>().ShouldContain(error => error.Number == 51004);
			var preservedFingerprint = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
				$"SELECT [Fingerprint] FROM {tableName}", cancellationToken: TestCancellationToken));
			preservedFingerprint.ShouldBe(originalFingerprint);
		}
		finally
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"DROP TRIGGER [{faultTrigger}] ON DATABASE", cancellationToken: TestCancellationToken));
		}
	}

	private async Task InstallRecreationFailureAsync(SqlConnection connection, string faultTrigger, string guardName)
	{
		// Database-scoped only because CREATE TRIGGER is DDL. The predicate makes the fault local to
		// this arm's unique fingerprint guard; unrelated trigger creation is left untouched.
		var faultSql = $"""
			CREATE TRIGGER [{faultTrigger}] ON DATABASE FOR CREATE_TRIGGER, ALTER_TRIGGER AS
			BEGIN
				SET NOCOUNT ON;
				DECLARE @event XML = EVENTDATA();
				IF @event.value('(/EVENT_INSTANCE/SchemaName)[1]', 'nvarchar(128)') = N'compliance'
					AND @event.value('(/EVENT_INSTANCE/ObjectName)[1]', 'nvarchar(128)') = N'{guardName}'
					THROW 51987, 'Injected failure recreating the test fingerprint guard.', 1;
			END
			""";
		_ = await connection.ExecuteAsync(new CommandDefinition(faultSql, cancellationToken: TestCancellationToken));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task RestoreCallerAmbientScopeAfterHashMismatchOrPreCanceledInitialization(bool preCanceled)
	{
		var tables = TableSet();
		using (var provisioner = CreateStore(tables, KeyedHasher("stable-key")))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var scope = new TransactionScope(
			TransactionScopeOption.RequiresNew,
			new TransactionOptions
			{
				IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
				Timeout = TimeSpan.FromSeconds(30),
			},
			TransactionScopeAsyncFlowOption.Enabled);
		var ambient = Transaction.Current.ShouldNotBeNull();
		using var attempted = CreateStore(tables, KeyedHasher(preCanceled ? "stable-key" : "rotated-key"), autoCreate: false);
		if (preCanceled)
		{
			// Pre-entry/pre-open cancellation only; this does not claim coverage of an in-flight SQL cancellation.
			using var cancellation = new CancellationTokenSource();
			await cancellation.CancelAsync();
			_ = await Should.ThrowAsync<OperationCanceledException>(
				() => attempted.GetStatusAsync(Guid.NewGuid(), cancellation.Token));
		}
		else
		{
			var refused = await Should.ThrowAsync<InvalidOperationException>(
				() => attempted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
			refused.Message.ShouldContain(nameof(DataSubjectHashingOptions.Pepper), Case.Sensitive);
		}

		Transaction.Current.ShouldBeSameAs(ambient, "failed initialization must restore the caller's ambient scope");
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var usable = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			"SELECT 1", cancellationToken: TestCancellationToken));
		usable.ShouldBe(1, "the caller's transaction must remain usable after initialization fails");
		Transaction.Current.ShouldBeSameAs(ambient);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task AcceptAnExternallyProvisionedSchemaWithItsFingerprintWriteGuardEnabled()
	{
		var tables = TableSet();
		var hasher = KeyedHasher("stable-key");
		using (var provisioner = CreateStore(tables, hasher))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var restarted = CreateStore(tables, hasher, autoCreate: false);
		(await restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken)).ShouldBeNull(
			"a correctly provisioned write-once ledger must remain usable with automatic provisioning disabled");
	}

	private static async Task<bool> Settle(Task task)
	{
		try
		{
			await task;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static TableNames TableSet()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		return new TableNames(
			$"TfReq{suffix}",
			$"TfCert{suffix}",
			$"TfDk{suffix}",
			$"TfDi{suffix}",
			$"TfMk{suffix}",
			$"TfFp{suffix}");
	}

	private SqlServerErasureStore CreateStore(
		TableNames tables,
		IDataSubjectHasher hasher,
		ITenantContext? tenantContext = null,
		bool autoCreate = true) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = tables.Requests,
			CertificatesTableName = tables.Certificates,
			DestroyedKeysTableName = tables.DestroyedKeys,
			DestructionIntentsTableName = tables.DestructionIntents,
			MintedKeysTableName = tables.MintedKeys,
			TokenFingerprintTableName = tables.Fingerprint,
			AutoCreateSchema = autoCreate,
		}),
		hasher,
		EnabledTestLogger.Create<SqlServerErasureStore>(),
		tenantContext ?? UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// A keyed hasher implementing the interface DIRECTLY, inheriting no first-party base, so the arms
	/// bind the interface's contract. Two instances with different keys stand in for a rotated secret.
	/// </summary>
	private static IDataSubjectHasher KeyedHasher(string key) => new KeyedTestHasher(key);

	private sealed record TableNames(
		string Requests,
		string Certificates,
		string DestroyedKeys,
		string DestructionIntents,
		string MintedKeys,
		string Fingerprint);

	private sealed class KeyedTestHasher(string key) : IDataSubjectHasher
	{
		private readonly byte[] _key = Encoding.UTF8.GetBytes(key);

		public string HashDataSubjectId(string dataSubjectId) =>
			Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(dataSubjectId)));
	}

	private sealed class NamedOwnerContext(string owner) : ITenantContext
	{
		public string? TenantId => owner;

		public bool HasTenant => true;
	}
}
