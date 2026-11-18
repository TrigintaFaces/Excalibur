// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;
using System.Transactions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Binds the guarantee that a changed data-subject token function is REFUSED rather than silently
/// orphaning every token-keyed record in the store.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms run against a real PostgreSQL instance and are never skipped.</b> The property under
/// test is substrate behaviour — an atomic insert-if-absent, a primary-key conflict, and a trigger that
/// refuses a rewrite — none of which a mocked connection can reproduce. A mock here would report PASS
/// over a provider that does not work.
/// </para>
/// <para>
/// <b>Safety is paired with liveness throughout.</b> A store that refused every startup would satisfy
/// every refusal arm below and be useless, so each is accompanied by an arm proving the permitted case
/// still works.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresTokenFingerprintShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresTokenFingerprintShould(PostgresFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task RefuseToStartWhenTheHashingKeyChangedBetweenTwoRuns()
	{
		// THE BEAD'S OWN ACCEPTANCE CRITERION: change the key between two runs against one store, and the
		// second must refuse rather than write records nothing will ever find again.
		var tables = TableSet();

		using (var first = CreateStore(tables, KeyedHasher("key-one")))
		{
			_ = await first.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var second = CreateStore(tables, KeyedHasher("key-two"));

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => second.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

		// The message is bound, not just the type: an operator who cannot tell WHAT to restore has been
		// handed a puzzle instead of an instruction.
		thrown.Message.ShouldContain(
			nameof(DataSubjectHashingOptions.Pepper),
			Case.Sensitive,
			$"the refusal must name the setting to restore. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartOnEveryRunWhenTheHashingKeyIsUnchanged()
	{
		// LIVENESS for the arm above, and the one that fails if the check is simply always-refuse.
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
		// INVARIANT J, ENFORCED AT THE DATABASE RATHER THAN TRUSTED TO THIS CLASS. The claim is an
		// insert-if-absent followed by a read, which is only sound while the value can never change: if a
		// rewrite were possible, a process with the WRONG key could overwrite the record and then match
		// it, while the process with the RIGHT key refused. This arm proves an administrator at a prompt
		// cannot do that either.
		var tables = TableSet();
		using (var store = CreateStore(tables, KeyedHasher("key-one")))
		{
			_ = await store.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var update = $@"UPDATE ""compliance"".""{tables.Fingerprint}"" SET fingerprint = 'tampered'";
		_ = await Should.ThrowAsync<PostgresException>(
			() => connection.ExecuteAsync(new CommandDefinition(update, cancellationToken: TestCancellationToken)));

		var delete = $@"DELETE FROM ""compliance"".""{tables.Fingerprint}""";
		_ = await Should.ThrowAsync<PostgresException>(
			() => connection.ExecuteAsync(new CommandDefinition(delete, cancellationToken: TestCancellationToken)));
	}

	[Fact]
	public async Task RecordExactlyOneFingerprintWhenTwoProcessesStartTogether()
	{
		// Two hosts with DIFFERENT keys racing to claim an unclaimed store. Exactly one may proceed; the
		// loser must refuse. "Both proceed" is the outcome that would make the whole mechanism decorative.
		//
		// THE SCHEMA IS PROVISIONED FIRST, AND THE RACERS DO NOT AUTO-CREATE. That is not a convenience:
		// `CREATE SCHEMA IF NOT EXISTS` is not atomic in PostgreSQL, so two stores provisioning at once
		// collide on pg_namespace. Racing the auto-create path would therefore measure PostgreSQL's
		// catalogue concurrency instead of this claim's atomicity, and fail for a reason unrelated to the
		// property under test. The fingerprint table is then TRUNCATEd — row triggers do not fire on
		// TRUNCATE, which is the only way to return a provisioned store to the unclaimed state the
		// write-once guard otherwise makes unreachable.
		var tables = TableSet();

		using (var provisioner = CreateStore(tables, KeyedHasher("provisioner")))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await using (var setup = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await setup.OpenAsync(TestCancellationToken);
			_ = await setup.ExecuteAsync(new CommandDefinition(
				$@"TRUNCATE TABLE ""compliance"".""{tables.Fingerprint}""",
				cancellationToken: TestCancellationToken));
		}

		using var one = CreateStore(tables, KeyedHasher("racer-one"), autoCreate: false);
		using var two = CreateStore(tables, KeyedHasher("racer-two"), autoCreate: false);

		var first = Task.Run(() => one.GetStatusAsync(Guid.NewGuid(), TestCancellationToken), TestCancellationToken);
		var second = Task.Run(() => two.GetStatusAsync(Guid.NewGuid(), TestCancellationToken), TestCancellationToken);

		var outcomes = await Task.WhenAll(Settle(first), Settle(second));

		outcomes.Count(succeeded => succeeded).ShouldBe(
			1,
			"exactly one of two processes with different hashing keys may proceed against a fresh store; " +
			"two winners means the claim is not atomic, and none means it is always-refuse.");
	}

	[Fact]
	public async Task ReportTheSameFingerprintWhateverTenantIsAmbient()
	{
		// THE FINGERPRINT CARRIES NO TENANT TERM, and this is what reddens if any provider reintroduces
		// one. A per-tenant fingerprint is worse than none: a tenant onboarded after a rotation would
		// record the new key as correct while every earlier tenant was already orphaned, and the check
		// would then pass forever on exactly the deployment where the damage is worst.
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

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var alterGuard = disableGuard
			? $"ALTER TABLE \"compliance\".\"{tables.Fingerprint}\" DISABLE TRIGGER \"trg_{tables.Fingerprint}_write_once\""
			: $"DROP TRIGGER \"trg_{tables.Fingerprint}_write_once\" ON \"compliance\".\"{tables.Fingerprint}\"";
		_ = await connection.ExecuteAsync(new CommandDefinition(alterGuard, cancellationToken: TestCancellationToken));

		try
		{
			using var restarted = CreateStore(tables, hasher, autoCreate: false);
			var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
				() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

			thrown.TableName.ShouldBe($"\"compliance\".\"{tables.Fingerprint}\"");
			thrown.Message.ShouldContain("write-once", Case.Insensitive);
		}
		finally
		{
			// Restore only this arm's unique table guard, even when the refusal assertion fails.
			// Re-running the production provisioner also recreates a dropped guard without mirroring its body.
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

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var narrowGuard = $"DROP TRIGGER \"trg_{tables.Fingerprint}_write_once\" ON \"compliance\".\"{tables.Fingerprint}\"; "
			+ $"CREATE TRIGGER \"trg_{tables.Fingerprint}_write_once\" BEFORE UPDATE ON \"compliance\".\"{tables.Fingerprint}\" "
			+ $"FOR EACH ROW EXECUTE FUNCTION \"compliance\".{tables.Fingerprint}_is_write_once()";
		_ = await connection.ExecuteAsync(new CommandDefinition(narrowGuard, cancellationToken: TestCancellationToken));

		try
		{
			using var restarted = CreateStore(tables, hasher, autoCreate: false);
			var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
				() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
			thrown.TableName.ShouldBe($"\"compliance\".\"{tables.Fingerprint}\"");
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
	public async Task ResolveTheProvisionedGuardForAMixedCaseLongFingerprintTableName()
	{
		// PostgreSQL parses the quoted table name and unquoted generated function name differently,
		// including identifier truncation. Let its native resolver apply those same rules to both.
		var tables = TableSet() with { Fingerprint = $"TfFp_{Guid.NewGuid():N}_{new string('X', 40)}" };
		var hasher = KeyedHasher("stable-key");
		using (var provisioner = CreateStore(tables, hasher))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		using var restarted = CreateStore(tables, hasher, autoCreate: false);
		(await restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken)).ShouldBeNull();
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task InitializeDurablyOnFirstUseInsideAnAmbientTransactionThatRollsBack()
	{
		new NpgsqlConnectionStringBuilder(_fixture.ConnectionString).Enlist.ShouldBeTrue(
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
			// Deliberately no Complete: caller business work rolls back, initialization must remain durable.
		}

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		var enabledGuards = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			"SELECT COUNT(*) FROM pg_catalog.pg_trigger WHERE tgrelid = to_regclass(@TableName) "
			+ "AND tgname = @GuardName AND NOT tgisinternal AND tgenabled IN ('O', 'A')",
			new
			{
				TableName = $"\"compliance\".\"{tables.Fingerprint}\"",
				GuardName = $"trg_{tables.Fingerprint}_write_once",
			},
			cancellationToken: TestCancellationToken));
		enabledGuards.ShouldBe(1, "initialization's durable guard must not roll back with caller business work");
		var fingerprint = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
			$"SELECT fingerprint FROM \"compliance\".\"{tables.Fingerprint}\"", cancellationToken: TestCancellationToken));
		fingerprint.ShouldNotBeNullOrWhiteSpace();
		fingerprint.ShouldBe(claimedInside);
		(await store.GetStatusAsync(request.RequestId, TestCancellationToken)).ShouldBeNull(
			"business work must roll back while cached initialization continues to describe durable schema");

		using var changedHasher = CreateStore(tables, KeyedHasher("rotated-key"), owner, autoCreate: false);
		var refused = await Should.ThrowAsync<InvalidOperationException>(
			() => changedHasher.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));
		refused.Message.ShouldContain(nameof(DataSubjectHashingOptions.Pepper), Case.Sensitive);
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
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
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
			$"tf_req_{suffix}",
			$"tf_cert_{suffix}",
			$"tf_dk_{suffix}",
			$"tf_di_{suffix}",
			$"tf_mk_{suffix}",
			$"tf_fp_{suffix}");
	}

	private PostgresErasureStore CreateStore(
		TableNames tables,
		IDataSubjectHasher hasher,
		ITenantContext? tenantContext = null,
		bool autoCreate = true) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresErasureStoreOptions
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
		EnabledTestLogger.Create<PostgresErasureStore>(),
		tenantContext ?? UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// A keyed hasher implementing the interface DIRECTLY, inheriting no first-party base, so the arms bind
	/// the interface's contract. Two instances with different keys stand in for a rotated secret.
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
