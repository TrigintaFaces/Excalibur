// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds <see cref="OracleSagaTimeoutStore"/> to the shared <see cref="ISagaTimeoutStore"/> conformance kit
/// against a live Oracle container.
/// </summary>
/// <remarks>
/// <para>
/// Before this existed, <c>OracleSagaTimeoutStore</c> had <b>zero</b> tests. Its claim path is hand-written
/// PL/SQL — <c>FETCH … BULK COLLECT INTO … LIMIT :BatchSize</c> under <c>FOR UPDATE SKIP LOCKED</c> — and no
/// test had ever executed it. A mocked Oracle client cannot: the batch bound and the skip-locked lease are
/// enforced by the database, not by the driver, so a unit test would certify PL/SQL that never ran.
/// </para>
/// <para>
/// Never skipped. When Docker is unavailable the fixture fails rather than passing silently, because a
/// skip-gated infrastructure test that "never ran" is how an untested store ships.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "STORE")]
public sealed class OracleSagaTimeoutStoreConformanceShould
	: SagaTimeoutStoreConformanceTestBase, IClassFixture<OracleSagaTimeoutStoreContainerFixture>
{
	private readonly OracleSagaTimeoutStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaTimeoutStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Oracle container fixture.</param>
	public OracleSagaTimeoutStoreConformanceShould(OracleSagaTimeoutStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaTimeoutStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// The single-tenant host shape. This suite used to pass no context at all, so the store resolved its
		// partition by folding an unset ambient value through the storage fold and stamped the reserved
		// untenanted sentinel -- a partition no other component in the framework addresses. A single-tenant
		// deployment operates as the one canonical tenant identity, and that is what these arms round-trip.
		return new OracleSagaTimeoutStore(
			_fixture.ConnectionString,
			NullLogger<OracleSagaTimeoutStore>.Instance,
			SingleTenantTestContext.Instance);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
