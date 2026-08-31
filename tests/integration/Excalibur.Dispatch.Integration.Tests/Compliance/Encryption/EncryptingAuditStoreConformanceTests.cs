// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging;
using Excalibur.AuditLogging.Encryption;
using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Encryption;

/// <summary>
/// Runs the shipped audit-store conformance kit against <see cref="EncryptingAuditEventStore"/> wrapping
/// the REAL Postgres audit store, with REAL AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. There are exactly five <c>IAuditStore</c> implementations in the source tree. Three
/// were bound to this kit -- in-memory, Postgres, SQL Server. The two that were not are both DECORATORS,
/// and a decorator is the case a conformance kit exists for: <c>EncryptingAuditEventStore</c> declares
/// <c>IAuditStore</c> and therefore claims the whole contract, including the parts it forwards to a store
/// whose columns it has just replaced with ciphertext.
/// </para>
/// <para>
/// NOTHING HERE IS SIMULATED except key custody. The store beneath is the shipped
/// <c>PostgresAuditStore</c> against a real Postgres container; the cipher is the shipped
/// <c>AesGcmEncryptionProvider</c>; the keys come from the shipped <c>InMemoryKeyManagementProvider</c>,
/// which is what a consumer gets before they configure a KMS. The subject under test is the decorator's
/// conformance, so the collaborator that must be real is the store, and it is.
/// </para>
/// <para>
/// THE TABLE IS THIS SUITE'S OWN. The sibling Postgres suite TRUNCATEs <c>audit.audit_events</c> at the
/// start of every arm; sharing that table would have each suite deleting the other's rows whenever the
/// collection interleaved them. Its columns are also deliberately WIDER than the sibling's -- see
/// <see cref="EnsureSchemaAsync"/>, where the reason is a finding rather than a convenience.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class EncryptingAuditStoreConformanceTests : AuditStoreConformanceTestKit, IAsyncLifetime
{
	private const string TableName = "audit_events_encrypted";
	private const string QualifiedTable = "audit." + TableName;

	/// <summary>The purpose the decorator names when it resolves an encryption key.</summary>
	private static readonly string EncryptionPurpose = new AuditEncryptionOptions().EncryptionPurpose;

	private readonly PostgresFixture _fixture;
	private readonly InMemoryKeyManagementProvider _keyManagement =
		new(NullLogger<InMemoryKeyManagementProvider>.Instance);

	private AesGcmEncryptionProvider? _encryption;

	public EncryptingAuditStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_encryption = new AesGcmEncryptionProvider(
			_keyManagement,
			NullLogger<AesGcmEncryptionProvider>.Instance);

		// Provision the key the decorator will ask for. AesGcmEncryptionProvider resolves by PURPOSE
		// (AesGcmEncryptionProvider.cs:386-393) and the decorator names AuditEncryptionOptions.EncryptionPurpose
		// (EncryptingAuditEventStore.cs:139), while the in-memory provider's auto-generated default key carries
		// no purpose -- so without this every arm dies on "No suitable key found for encryption" before it ever
		// reaches the store. This is the step a consumer performs, not a relaxation of anything the kit asserts.
		_ = await _keyManagement.RotateKeyAsync(
			"audit-conformance-key",
			EncryptionAlgorithm.Aes256Gcm,
			EncryptionPurpose,
			expiresAt: null,
			CancellationToken.None).ConfigureAwait(false);

		await EnsureSchemaAsync().ConfigureAwait(false);
	}

	public ValueTask DisposeAsync()
	{
		_encryption?.Dispose();
		_keyManagement.Dispose();
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	protected override IAuditStore CreateStore() =>
		Wrap(new TestTenantContext(TenantScope.UntenantedSentinel));

	/// <inheritdoc/>
	/// <remarks>
	/// Same decorator, over an inner store that resolves the ambient tenant. The tenant arms establish
	/// <c>TenantContextHolder.Current</c>, and the decorator keys encryption by the EVENT's tenant id, so
	/// both halves of the tenant story are exercised through the real predicates.
	/// </remarks>
	protected override IAuditStore CreateTenantAwareStore() => Wrap(new AmbientAuditTenantContext());

	private EncryptingAuditEventStore Wrap(ITenantContext tenantContext)
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = TableName,
			CommandTimeoutSeconds = 30,
		};

		var inner = new PostgresAuditStore(
			Microsoft.Extensions.Options.Options.Create(options),
			AuditIntegrityTestStrategy.Create(),
			tenantContext,
			EnabledTestLogger.Create<PostgresAuditStore>());

		return new EncryptingAuditEventStore(
			inner,
			_encryption ?? throw new InvalidOperationException("Encryption provider not initialised"),
			// Defaults verbatim: EncryptActorId and EncryptIpAddress are ON out of the box
			// (AuditEncryptionOptions.cs:27,33), so this is the configuration a consumer gets by calling
			// the decorator with no options of their own. Turning either off to make an arm pass would
			// test a configuration nobody runs.
			Microsoft.Extensions.Options.Options.Create(new AuditEncryptionOptions()));
	}

	private sealed class AmbientAuditTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Raw SQL against this suite's own table, bypassing both the decorator and the store -- which is what
	/// a party with database access has. Throws when it removed nothing: a delete that deleted no row would
	/// let the tamper-detection arm pass against a store that detects nothing at all.
	/// </remarks>
	protected override async Task DeleteRecordOutOfBandAsync(
		IAuditStore store,
		string eventId,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(
			new CommandDefinition(
				"DELETE FROM " + QualifiedTable + " WHERE event_id = @EventId",
				new { EventId = eventId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (affected != 1)
		{
			throw new InvalidOperationException(
				$"Expected to delete exactly one audit row for '{eventId}', deleted {affected}.");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Touches only the action column, which the decorator does not encrypt. Both hash columns are left
	/// exactly as written, so the trail stays self-consistent on linkage and the arm establishes that
	/// verification recomputes from live content rather than re-checking a stored digest.
	/// </remarks>
	protected override async Task RewriteRecordActionOutOfBandAsync(
		IAuditStore store,
		string eventId,
		string newAction,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(
			new CommandDefinition(
				"UPDATE " + QualifiedTable + " SET action = @NewAction WHERE event_id = @EventId",
				new { EventId = eventId, NewAction = newAction },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (affected != 1)
		{
			throw new InvalidOperationException(
				$"Expected to rewrite exactly one audit row for '{eventId}', rewrote {affected}.");
		}
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync("TRUNCATE TABLE " + QualifiedTable + " RESTART IDENTITY")
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Creates this suite's own audit table.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The column list mirrors the store's INSERT list, because the package ships no DDL and the write
	/// statement is the only authority on the schema it needs.
	/// </para>
	/// <para>
	/// THE WIDTHS ARE NOT THE SIBLING'S, AND THAT IS A FINDING. The sibling suite declares
	/// <c>actor_id VARCHAR(256)</c> and <c>ip_address VARCHAR(45)</c> -- the natural widths for the
	/// plaintext those columns hold. This decorator replaces both with base64 of the JSON serialisation of
	/// an <c>EncryptedData</c>, which carries ciphertext, IV, auth tag, key id, key version, algorithm and
	/// a timestamp, so the stored value is far longer than the plaintext and overflows both. Widening them
	/// here is what lets the other arms run and report on the decorator's behaviour instead of every arm
	/// dying on one string truncation; it is NOT a claim that a consumer's schema will hold.
	/// </para>
	/// </remarks>
	private async Task EnsureSchemaAsync()
	{
		const string CreateSchemaAndTableSql = """
			CREATE SCHEMA IF NOT EXISTS audit;

			CREATE TABLE IF NOT EXISTS audit.audit_events_encrypted (
				sequence_number         BIGSERIAL PRIMARY KEY,
				event_id                VARCHAR(64)  NOT NULL UNIQUE,
				event_type              INT          NOT NULL,
				action                  VARCHAR(100) NOT NULL,
				outcome                 INT          NOT NULL,
				timestamp               TIMESTAMPTZ  NOT NULL,
				actor_id                TEXT         NOT NULL,
				actor_type              VARCHAR(50),
				resource_id             VARCHAR(256),
				resource_type           VARCHAR(100),
				resource_classification INT,
				tenant_id               VARCHAR(64),
				application_name        VARCHAR(256),
				correlation_id          VARCHAR(64),
				session_id              VARCHAR(64),
				ip_address              TEXT,
				user_agent              VARCHAR(500),
				reason                  VARCHAR(1000),
				metadata                JSONB,
				previous_event_hash     VARCHAR(512),
				event_hash              VARCHAR(512) NOT NULL
			);

			TRUNCATE TABLE audit.audit_events_encrypted RESTART IDENTITY;
			""";

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync(CreateSchemaAndTableSql).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// THE KIT'S ARM CANNOT HOLD HERE, AND THE REASON IS THE SUBJECT UNDER TEST. This decorator stores the
	/// actor id as randomized authenticated ciphertext, so two records holding the same actor id hold
	/// different bytes and the inner store's <c>actor_id = @ActorId</c> can match neither. No amount of
	/// forwarding fixes that; the comparison the kit asks for does not exist for this column.
	/// </para>
	/// <para>
	/// What the arm asserts instead is the behaviour the contract requires when a filter cannot be served:
	/// a refusal that names the field. It is deliberately STRONGER than the base arm, not weaker -- it
	/// pins the exception type and the field name, AND it goes on to prove the records are present and
	/// readable by a filter that is servable. That second half is the whole finding: before this, the call
	/// succeeded and returned an empty list, so an operator asking what an actor did was told nothing
	/// while these very rows sat in the table. The arm now fails if that empty answer ever comes back.
	/// </para>
	/// <para>
	/// A consumer who needs this query turns off <c>AuditEncryptionOptions.EncryptActorId</c> and stores
	/// the actor id in the clear. That trade is the consumer's to make, and the store no longer makes it
	/// for them by answering nothing.
	/// </para>
	/// </remarks>
	public override async Task QueryAsync_ByActorId_ShouldFilter()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var actor1Event = CreateAuditEvent(actorId: "actor-1");
		var actor2Event = CreateAuditEvent(actorId: "actor-2");

		_ = await store.StoreAsync(actor1Event, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(actor2Event, CancellationToken.None).ConfigureAwait(false);

		try
		{
			var results = await store
				.QueryAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None)
				.ConfigureAwait(false);

			throw new TestFixtureAssertionException(
				"Filtering by an encrypted actor id returned "
				+ results.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ " events instead of refusing. The comparison cannot match randomized ciphertext, so any "
				+ "answer here is a wrong answer -- and an empty one is the dangerous kind, because it "
				+ "reads as 'this actor did nothing'.");
		}
		catch (NotSupportedException ex)
		{
			if (!ex.Message.Contains(nameof(AuditQuery.ActorId), StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					"The refusal must name the field that could not be served so the caller knows which part "
					+ "of their query to change. Message was: " + ex.Message);
			}
		}

		// The events are present. Establishing this is what separates "cannot be filtered" from "was never
		// stored" -- and it is the claim the silent empty result used to make falsely.
		var byEventType = await store
			.QueryAsync(new AuditQuery { EventTypes = [actor1Event.EventType] }, CancellationToken.None)
			.ConfigureAwait(false);

		if (!byEventType.Any(e => e.EventId == actor1Event.EventId)
			|| !byEventType.Any(e => e.EventId == actor2Event.EventId))
		{
			throw new TestFixtureAssertionException(
				"Both events must still be retrievable by a servable filter. If they are not, the refusal "
				+ "above is masking a store that did not persist them at all.");
		}

		// And they decrypt back to the plaintext the caller supplied, so the actor id is recoverable by
		// reading -- just not by asking the database to compare it.
		if (!byEventType.Any(e => string.Equals(e.ActorId, "actor-1", StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"The encrypted actor id must round-trip to its plaintext on retrieval.");
		}
	}

	/// <summary>
	/// Asserts that a count over an encrypted field is refused rather than answered with zero.
	/// </summary>
	/// <remarks>
	/// The kit has no count-by-actor arm, and the omission mattered: <c>CountAsync</c> carried the same
	/// defect as <c>QueryAsync</c> and nothing would have caught it. A zero is worse than an empty list.
	/// An empty list at least invites the question "did the filter work"; a zero is a number, it looks like
	/// a measurement, and it is the value a compliance report would print next to an actor's name.
	/// </remarks>
	/// <returns>A task that completes when the refusal has been asserted.</returns>
	[Fact]
	public async Task CountAsync_ByEncryptedActorId_ShouldRefuseRatherThanCountZero()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		_ = await store.StoreAsync(CreateAuditEvent(actorId: "actor-1"), CancellationToken.None)
			.ConfigureAwait(false);

		try
		{
			var count = await store
				.CountAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None)
				.ConfigureAwait(false);

			throw new TestFixtureAssertionException(
				"Counting by an encrypted actor id returned "
				+ count.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ " instead of refusing. The stored event exists; a count that cannot see it must say so.");
		}
		catch (NotSupportedException ex)
		{
			if (!ex.Message.Contains(nameof(AuditQuery.ActorId), StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					"The refusal must name the field that could not be counted. Message was: " + ex.Message);
			}
		}
	}

	/// <summary>
	/// Asserts the same refusal for the other field this decorator encrypts by default.
	/// </summary>
	/// <remarks>
	/// Actor id is the field the failure happened to name; it is not the only field with this shape.
	/// <c>IpAddress</c> is encrypted by the same defaults and filtered by the same equality predicate in
	/// every leaf store, so it fails identically and is asserted here rather than left to be discovered
	/// the same way the first one was.
	/// </remarks>
	/// <returns>A task that completes when the refusal has been asserted.</returns>
	[Fact]
	public async Task QueryAsync_ByEncryptedIpAddress_ShouldRefuseRatherThanReturnEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		try
		{
			var results = await store
				.QueryAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None)
				.ConfigureAwait(false);

			throw new TestFixtureAssertionException(
				"Filtering by an encrypted IP address returned "
				+ results.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ " events instead of refusing.");
		}
		catch (NotSupportedException ex)
		{
			if (!ex.Message.Contains(nameof(AuditQuery.IpAddress), StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					"The refusal must name the field that could not be served. Message was: " + ex.Message);
			}
		}
	}

	[Fact]
	public Task StoreAsync_ShouldPersistEvent_Test() => StoreAsync_ShouldPersistEvent();

	[Fact]
	public Task StoreAsync_WithNullEvent_ShouldThrow_Test() => StoreAsync_WithNullEvent_ShouldThrow();

	[Fact]
	public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() => StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() => GetByIdAsync_ExistingEvent_ShouldReturnEvent();

	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() => GetByIdAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() => GetByIdAsync_NullOrEmpty_ShouldThrow();

	[Fact]
	public Task QueryAsync_ByDateRange_ShouldReturnMatching_Test() => QueryAsync_ByDateRange_ShouldReturnMatching();

	[Fact]
	public Task QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents_Test() => QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents();

	[Fact]
	public Task GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt_Test() => GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt();

	[Fact]
	public Task QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents_Test() => QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents();

	[Fact]
	public Task QueryAsync_ByEventType_ShouldFilter_Test() => QueryAsync_ByEventType_ShouldFilter();

	[Fact]
	public Task QueryAsync_ByActorId_ShouldFilter_Test() => QueryAsync_ByActorId_ShouldFilter();

	[Fact]
	public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() => QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

	[Fact]
	public Task CountAsync_WithFilters_ShouldReturnCount_Test() => CountAsync_WithFilters_ShouldReturnCount();

	[Fact]
	public Task CountAsync_EmptyResult_ShouldReturnZero_Test() => CountAsync_EmptyResult_ShouldReturnZero();

	[Fact]
	public Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified_Test() => VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified();

	[Fact]
	public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope_Test() => VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope();

	[Fact]
	public Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations_Test() => VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations();

	[Fact]
	public Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations_Test() => VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations();

	[Fact]
	public Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified_Test() => VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified();

	[Fact]
	public Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant_Test() => GetLastEventAsync_WithTenant_ShouldReturnLastForTenant();

	[Fact]
	public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() => GetLastEventAsync_DefaultTenant_ShouldReturnLast();

	[Fact]
	public Task StoreAsync_ShouldSetPreviousEventHash_Test() => StoreAsync_ShouldSetPreviousEventHash();

	[Fact]
	public Task StoreAsync_ShouldComputeEventHash_Test() => StoreAsync_ShouldComputeEventHash();

	[Fact]
	public Task StoreAsync_WithApplicationName_ShouldPersistApplicationName_Test() => StoreAsync_WithApplicationName_ShouldPersistApplicationName();

	[Fact]
	public Task StoreAsync_WithNullApplicationName_ShouldPersistNull_Test() => StoreAsync_WithNullApplicationName_ShouldPersistNull();

	[Fact]
	public Task QueryAsync_ByApplicationName_ShouldFilter_Test() => QueryAsync_ByApplicationName_ShouldFilter();

	[Fact]
	public Task CountAsync_ByApplicationName_ShouldCount_Test() => CountAsync_ByApplicationName_ShouldCount();

	[Fact]
	public Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash_Test() => StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
