// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated
#pragma warning disable CA2100 // SQL strings are safe - schema/table names are fixture constants

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="OracleInboxStore"/> using the Inbox Conformance
/// Test Kit against a live Oracle (<c>gvenzl/oracle-free</c>) container.
/// </summary>
/// <remarks>
/// Verifies the Oracle implementation satisfies the <see cref="IInboxStore"/> (and admin/claim/transactional)
/// contract against real infrastructure. Never skipped: when Docker is unavailable the fixture fails fast,
/// so a missing container surfaces as a failure rather than a silent pass. Exercises the emitted behavior —
/// dedup on the unique key (ORA-00001), first-writer-wins claim, and the exactly-once transactional path —
/// not merely that a value was written.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Collection(OracleInboxTestCollection.CollectionName)]
public sealed class OracleInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly OracleInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleInboxStoreConformanceShould(OracleInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => new TestTenantContext();

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new OracleInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName
		});

		return new OracleInboxStore(options, NullLogger<OracleInboxStore>.Instance, tenantContext: new TestTenantContext(), tenantContextOptions: Options.Create(new TenantContextOptions()));
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	// 47ruyr (2mek4x follow-up): a real, provider-side persistence rejection -- never a mocked client.
	// Oracle's RENAME makes every statement referencing the table fail with a genuine ORA-00942
	// ("table or view does not exist"), then renames it back.
	private const string FaultTableName = "INBOX_MESSAGES_2MEK4X_FAULT";

	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = $"RENAME {_fixture.TableName} TO {FaultTableName}";
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = $"RENAME {FaultTableName} TO {_fixture.TableName}";
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
