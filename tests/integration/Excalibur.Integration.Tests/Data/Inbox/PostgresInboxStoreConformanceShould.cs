// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="PostgresInboxStore"/> using the
/// Inbox Conformance Test Kit against a live Postgres container.
/// </summary>
/// <remarks>
/// These tests verify that the Postgres implementation correctly implements the
/// <see cref="IInboxStore"/> (and <see cref="IInboxStoreAdmin"/>) contract using TestContainers.
/// They are never skipped: when Docker is unavailable the fixture fails fast, so a missing
/// container surfaces as a failure rather than a silent pass.
/// </remarks>
[Collection(PostgresInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<PostgresInboxStoreContainerFixture>
{
	private readonly PostgresInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresInboxStoreConformanceShould(PostgresInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Bind the options constructor (the default surface most consumers use); the store derives its
		// connection factory from the configured connection string.
		var options = Options.Create(new PostgresInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName
		});

		var logger = NullLogger<PostgresInboxStore>.Instance;

		// An ambient tenant context is REQUIRED here, and omitting it is what broke this suite.
		//
		// The fixture creates the MULTI-TENANT schema -- PRIMARY KEY (message_id, handler_type, tenant_id)
		// with tenant_id NOT NULL. Constructing the store without a context puts it in SINGLE-tenant mode,
		// and InboxSchemaContract.Verify then correctly refuses to run: a single-tenant store against a
		// tenanted table would ignore tenant_id entirely and read across partitions.
		//
		// The contract is right and must not be relaxed to make this pass. The store is brought into
		// agreement with the table instead, which is also the configuration a multi-tenant consumer runs.
		// BOTH arguments are required, and the second is the one that switches the mode.
		// The store computes its deployment mode from TenantContextOptions.RequireTenant -- which
		// AddMultiTenancy() sets -- and NOT from the presence of an ITenantContext. Its own source says so
		// in terms. Passing only the context leaves the store single-tenant against a tenanted table, which
		// the schema contract then correctly refuses.
		var tenancy = Options.Create(new TenantContextOptions { RequireTenant = true });

		return new PostgresInboxStore(options, logger, new ConformanceTenantContext(), tenancy);
	}

	/// <summary>
	/// A fixed ambient tenant for the conformance run.
	/// </summary>
	/// <remarks>
	/// Implements <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base, so the arms bind
	/// the interface's own requirement rather than a shared helper's behaviour. Cross-tenant isolation is
	/// proven by the dedicated isolation suites, which construct two of these; this conformance run
	/// exercises one tenant's own behaviour and needs only a stable identity.
	/// </remarks>
	private sealed class ConformanceTenantContext : ITenantContext
	{
		public string? TenantId => "conformance-tenant";

		public bool HasTenant => true;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
