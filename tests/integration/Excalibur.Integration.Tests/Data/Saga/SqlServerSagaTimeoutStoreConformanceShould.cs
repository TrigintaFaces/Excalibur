// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds <see cref="SqlServerSagaTimeoutStore"/> to the shared <see cref="ISagaTimeoutStore"/> conformance
/// kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// The claim path is a single <c>UPDATE … OUTPUT INSERTED.*</c> over an ordered CTE under
/// <c>READPAST, UPDLOCK, ROWLOCK</c>. Its batch bound and its skip-locked lease are enforced by the
/// database, not by the driver, so only a real server can decide whether the contract holds. Never skipped:
/// when Docker is unavailable the fixture fails rather than passing silently.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
public sealed class SqlServerSagaTimeoutStoreConformanceShould
	: SagaTimeoutStoreConformanceTestBase, IClassFixture<SqlServerSagaTimeoutStoreContainerFixture>
{
	private readonly SqlServerSagaTimeoutStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server container fixture.</param>
	public SqlServerSagaTimeoutStoreConformanceShould(SqlServerSagaTimeoutStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaTimeoutStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// The single-tenant host shape. This suite used to pass no context at all, so the store resolved its
		// partition by folding an unset ambient value through the storage fold and stamped the reserved
		// untenanted sentinel -- a partition no other component in the framework addresses. A single-tenant
		// deployment operates as the one canonical tenant identity, and that is what these arms round-trip.
		return new SqlServerSagaTimeoutStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaTimeoutStore>.Instance,
			SingleTenantTestContext.Instance);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
