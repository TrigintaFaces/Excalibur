// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

#pragma warning disable CA1812 // Internal class is never instantiated

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

		return new SqlServerInboxStore(options, logger);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
