// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging;
using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Rbac;

/// <summary>
/// Runs the shipped audit-store conformance kit against <see cref="RbacAuditStore"/> wrapping the REAL
/// Postgres audit store.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. <c>RbacAuditStore</c> was the last <c>IAuditStore</c> implementation not bound to this
/// kit. It declares the interface and therefore claims the whole contract, including the parts it forwards
/// to a store whose results it has just filtered.
/// </para>
/// <para>
/// <b>THE PRINCIPAL IS THE LOAD-BEARING CHOICE HERE, AND IT IS DELIBERATE.</b> Unlike the encrypting
/// decorator, this one is not transparent: it resolves the caller's role and can refuse before the inner
/// store is reached at all. A conformance run under an unauthorised principal would therefore measure the
/// refusal and never touch the tenant boundary — <b>and it would pass for a decorator with no tenant
/// confinement whatsoever</b>, because nothing would ever get far enough to leak. So the kit runs here as
/// <see cref="AuditLogRole.Administrator"/>.
/// </para>
/// <para>
/// That is not "the role that makes the arms pass". <c>ApplyRoleFilters</c> returns the query
/// <i>unchanged</i> for <c>ComplianceOfficer</c> and above, so the most privileged caller is the one that
/// bypasses every filter this decorator applies — leaving the tenant boundary as the only thing still
/// standing. If confinement holds for the caller who bypasses all role filtering, it holds for every
/// lesser role by construction. Choosing a weaker role would let a role filter mask a tenant leak and the
/// suite could not tell the difference.
/// </para>
/// <para>
/// <b>THE REFUSAL IS A SEPARATE ARM, NEVER FOLDED INTO THE TENANT ONES.</b> See
/// <see cref="Refuse_a_read_from_an_unauthorised_principal"/>. A single arm carrying both properties is
/// satisfied by a decorator that refuses everyone — the cheapest way to be safe and the most expensive
/// way to be wrong.
/// </para>
/// <para>
/// <b>THE TABLE IS THIS SUITE'S OWN.</b> The sibling Postgres suites TRUNCATE their table at the start of
/// every arm; sharing one would have each suite deleting the other's rows whenever the collection
/// interleaved them.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class RbacAuditStoreConformanceTests : AuditStoreConformanceTestKit, IAsyncLifetime
{
	private const string TableName = "audit_events_rbac";
	private const string QualifiedTable = "audit." + TableName;

	private readonly PostgresFixture _fixture;

	public RbacAuditStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync() => await EnsureSchemaAsync().ConfigureAwait(false);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	protected override IAuditStore CreateStore() =>
		Wrap(new TestTenantContext(TenantScope.UntenantedSentinel), AuditLogRole.Administrator);

	/// <inheritdoc />
	protected override IAuditStore CreateTenantAwareStore() =>
		Wrap(new AmbientAuditTenantContext(), AuditLogRole.Administrator);

	/// <summary>
	/// SAFETY, and the arm that must stay separate from every tenant arm above.
	/// </summary>
	/// <remarks>
	/// The kit's arms all run as an administrator so the read reaches the store. That leaves the refusal
	/// itself unasserted, and a decorator that had lost its access control would pass all of them. This is
	/// the liveness/safety pair completed: the arms above prove a permitted read still happens and stays
	/// tenant-confined; this one proves a forbidden read is still refused.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_read_from_an_unauthorised_principal()
	{
		var store = Wrap(new TestTenantContext(TenantScope.UntenantedSentinel), AuditLogRole.None);

		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			() => store.QueryAsync(new AuditQuery(), CancellationToken.None)).ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS for the arm above — without it, "refuses the unauthorised caller" is satisfied by a
	/// decorator that refuses everybody, which is exactly what the administrator arms would then be unable
	/// to distinguish from working access control.
	/// </summary>
	[Fact]
	public async Task Permit_a_read_from_an_authorised_principal()
	{
		var store = Wrap(new TestTenantContext(TenantScope.UntenantedSentinel), AuditLogRole.Administrator);

		var results = await store.QueryAsync(new AuditQuery(), CancellationToken.None).ConfigureAwait(false);

		results.ShouldNotBeNull();
	}

	// ---- Kit arms. Each derived suite attributes the kit's virtual arms itself; the kit ships the
	// bodies and no [Fact], so a suite that omits a wrapper simply does not run that arm. That is the
	// failure this block exists to prevent, and ConformanceSuite_ShouldWireEveryArm_Test below is what
	// makes the omission RED instead of invisible.

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task CountAsync_ByApplicationName_ShouldCount_Test() => CountAsync_ByApplicationName_ShouldCount();

	[Fact]
	public Task CountAsync_EmptyResult_ShouldReturnZero_Test() => CountAsync_EmptyResult_ShouldReturnZero();

	[Fact]
	public Task CountAsync_WithFilters_ShouldReturnCount_Test() => CountAsync_WithFilters_ShouldReturnCount();

	[Fact]
	public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() => GetByIdAsync_ExistingEvent_ShouldReturnEvent();

	[Fact]
	public Task GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt_Test() => GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt();

	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() => GetByIdAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() => GetByIdAsync_NullOrEmpty_ShouldThrow();

	[Fact]
	public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() => GetLastEventAsync_DefaultTenant_ShouldReturnLast();

	[Fact]
	public Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant_Test() => GetLastEventAsync_WithTenant_ShouldReturnLastForTenant();

	[Fact]
	public Task QueryAsync_ByActorId_ShouldFilter_Test() => QueryAsync_ByActorId_ShouldFilter();

	[Fact]
	public Task QueryAsync_ByApplicationName_ShouldFilter_Test() => QueryAsync_ByApplicationName_ShouldFilter();

	[Fact]
	public Task QueryAsync_ByDateRange_ShouldReturnMatching_Test() => QueryAsync_ByDateRange_ShouldReturnMatching();

	[Fact]
	public Task QueryAsync_ByEventType_ShouldFilter_Test() => QueryAsync_ByEventType_ShouldFilter();

	[Fact]
	public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() => QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

	[Fact]
	public Task QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents_Test() => QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents();

	[Fact]
	public Task QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents_Test() => QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents();

	[Fact]
	public Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash_Test() => StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash();

	[Fact]
	public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() => StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task StoreAsync_ShouldComputeEventHash_Test() => StoreAsync_ShouldComputeEventHash();

	[Fact]
	public Task StoreAsync_ShouldPersistEvent_Test() => StoreAsync_ShouldPersistEvent();

	[Fact]
	public Task StoreAsync_ShouldSetPreviousEventHash_Test() => StoreAsync_ShouldSetPreviousEventHash();

	[Fact]
	public Task StoreAsync_WithApplicationName_ShouldPersistApplicationName_Test() => StoreAsync_WithApplicationName_ShouldPersistApplicationName();

	[Fact]
	public Task StoreAsync_WithNullApplicationName_ShouldPersistNull_Test() => StoreAsync_WithNullApplicationName_ShouldPersistNull();

	[Fact]
	public Task StoreAsync_WithNullEvent_ShouldThrow_Test() => StoreAsync_WithNullEvent_ShouldThrow();

	[Fact]
	public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope_Test() => VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope();

	[Fact]
	public Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified_Test() => VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified();

	[Fact]
	public Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations_Test() => VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations();

	[Fact]
	public Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations_Test() => VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations();

	[Fact]
	public Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified_Test() => VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified();

	private RbacAuditStore Wrap(ITenantContext tenantContext, AuditLogRole role)
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

		// A REAL scope factory, because the decorator opens a scope per operation and resolves the role
		// provider from it. Handing it a stub factory would test a resolution path the production
		// decorator does not take.
		var services = new ServiceCollection();
		_ = services.AddSingleton<IAuditRoleProvider>(new FixedRoleProvider(role));

		// The meta-audit logger is resolved with GetRequiredService OUTSIDE the decorator's try, so a host
		// that never registered one fails loudly rather than silently disabling a segregation-of-duties
		// control. Registering a fake here supplies the host's obligation; it does not relax the control,
		// and omitting it would make every arm die on resolution rather than on its own assertion.
		_ = services.AddSingleton(A.Fake<IAuditLogger>());

		return new RbacAuditStore(
			inner,
			services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
			EnabledTestLogger.Create<RbacAuditStore>());
	}

	private sealed class FixedRoleProvider(AuditLogRole role) : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken) =>
			Task.FromResult(role);
	}

	private sealed class AmbientAuditTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <inheritdoc />
	protected override async Task CleanupAsync()
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync("TRUNCATE TABLE " + QualifiedTable + " RESTART IDENTITY")
			.ConfigureAwait(false);
	}

	private async Task EnsureSchemaAsync()
	{
		const string CreateSchemaAndTableSql = """
			CREATE SCHEMA IF NOT EXISTS audit;

			CREATE TABLE IF NOT EXISTS audit.audit_events_rbac (
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

			TRUNCATE TABLE audit.audit_events_rbac RESTART IDENTITY;
			""";

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync(CreateSchemaAndTableSql).ConfigureAwait(false);
	}
}
