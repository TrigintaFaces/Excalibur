// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

#pragma warning disable CA1812 // Internal class is never instantiated
#pragma warning disable CA2100 // SQL strings are safe - schema/table names are fixture constants

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqlServerInboxStore"/> using the
/// Inbox Conformance Test Kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// These tests verify that the SQL Server implementation correctly implements the
/// <see cref="IInboxStore"/> (and <see cref="IInboxStoreAdmin"/>) contract using TestContainers.
/// They are never skipped: when Docker is unavailable the fixture fails fast, so a missing
/// container surfaces as a failure rather than a silent pass.
/// </remarks>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<SqlServerInboxStoreContainerFixture>
{
	private readonly SqlServerInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerInboxStoreConformanceShould(SqlServerInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => new ConformanceTenantContext();

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Bind the options-only constructor (the default surface most consumers use); the store
		// derives its connection factory from the configured connection string.
		var options = Options.Create(new SqlServerInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName
		});

		var logger = NullLogger<SqlServerInboxStore>.Instance;

		// An ambient tenant context is REQUIRED here, and omitting it is what broke this suite.
		//
		// The fixture creates the MULTI-TENANT schema -- PRIMARY KEY (MessageId, HandlerType, TenantId)
		// with TenantId NOT NULL. Constructing the store without a context puts it in SINGLE-tenant mode,
		// and InboxSchemaContract.Verify then correctly refuses to run: a single-tenant store against a
		// tenanted table would ignore TenantId entirely and read across partitions.
		//
		// The contract is right and must not be relaxed to make this pass. The store is brought into
		// agreement with the table instead, which is also the configuration a multi-tenant consumer runs.
		// BOTH arguments are required, and the second is the one that switches the mode.
		// The store computes its deployment mode from TenantContextOptions.RequireTenant -- which
		// AddMultiTenancy() sets -- and NOT from the presence of an ITenantContext. Passing only the
		// context leaves the store single-tenant against a tenanted table.
		var tenancy = Options.Create(new TenantContextOptions { RequireTenant = true });

		return new SqlServerInboxStore(options, logger, new ConformanceTenantContext(), tenancy);
	}

	/// <summary>
	/// A fixed ambient tenant for the conformance run.
	/// </summary>
	/// <remarks>
	/// Implements <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base. Cross-tenant
	/// isolation is proven by the dedicated isolation suites, which construct two of these; this run
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

	// 2mek4x: a real, provider-side persistence rejection -- never a mocked client. Renaming the backing
	// table out from under the store makes every statement referencing it fail with a genuine "Invalid
	// object name" from the server, then renames it back -- the same shape of failure a consumer would
	// see from an out-of-band schema change or a botched migration, and portable across SQL engines
	// without depending on the connecting user's privilege level (the container's default user is
	// typically sysadmin, so a permission-revoke fault would not actually block a sysadmin's own writes).
	private const string FaultTableName = "inbox_messages__2mek4x_fault";

	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			$"EXEC sp_rename '[{_fixture.SchemaName}].[{_fixture.TableName}]', '{FaultTableName}';",
			connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			$"EXEC sp_rename '[{_fixture.SchemaName}].[{FaultTableName}]', '{_fixture.TableName}';",
			connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
