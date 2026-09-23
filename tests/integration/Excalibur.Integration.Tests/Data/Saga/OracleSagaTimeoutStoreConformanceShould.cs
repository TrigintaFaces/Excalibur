// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// The lease must be judged against the SAME clock it was stamped with. Regression lock for
	/// <c>OracleSagaTimeoutStore.ClaimDueTimeoutsAsync</c>, which used to compare <c>ClaimedAt</c> (stamped
	/// with <c>SYSTIMESTAMP</c> -- the Oracle server's clock) against <c>:AsOf - LeaseTimeoutSeconds</c>
	/// (the CALLER's clock). A second claimant whose wall clock leads the server's judged a lease it had
	/// just taken as already expired and reclaimed it immediately -- not a race, a steady state: with the
	/// shipped default 120s lease, a claimant only two minutes ahead of the database saw every claim expire
	/// before the claiming statement returned.
	/// </summary>
	/// <remarks>
	/// This does not need real elapsed time or a short lease to discriminate. The first claim stamps
	/// <c>ClaimedAt</c> to the server's actual "now". The second call passes an <c>asOf</c> two days in the
	/// future, simulating a claimant whose clock is skewed forward by that much -- the exact shape of the
	/// defect, since <c>DueAt &lt;= :AsOf</c> must still admit the row for the second call to reach the
	/// lease check at all. Judged against <c>:AsOf</c> (pre-fix), <c>:AsOf - 120s</c> is ~2 days ahead of the
	/// just-stamped <c>ClaimedAt</c>, so the row reads as expired and is reclaimed -- RED. Judged against
	/// <c>SYSTIMESTAMP</c> (post-fix), the server's actual now is milliseconds past the stamp, so the lease
	/// still holds -- GREEN.
	/// </remarks>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_StillExcludesASecondClaimant_WhenTheSecondClaimantsClockIsSkewedForward()
	{
		var realNow = DateTimeOffset.UtcNow;

		await Store.ScheduleTimeoutAsync(
			new SagaTimeout(
				TimeoutId: Guid.NewGuid().ToString(),
				SagaId: Guid.NewGuid().ToString(),
				SagaType: "ConformanceSaga",
				TimeoutType: "ConformanceTimeout",
				TimeoutData: null,
				DueAt: realNow.AddSeconds(-1),
				ScheduledAt: realNow.AddMinutes(-1)),
			CancellationToken.None).ConfigureAwait(false);

		// Claimant 1: an honest clock. Stamps ClaimedAt = the server's SYSTIMESTAMP.
		var firstClaim = await Store.ClaimDueTimeoutsAsync(realNow, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false);
		firstClaim.Count.ShouldBe(1, "the due timeout must be claimable before either claimant touches it");

		// Claimant 2: a clock skewed two days ahead of the server's -- but its own claim just landed
		// milliseconds ago on the server's real clock, so the lease must still exclude it.
		var skewedClaim = await Store.ClaimDueTimeoutsAsync(
			realNow.AddDays(2), batchSize: 10, CancellationToken.None).ConfigureAwait(false);

		skewedClaim.ShouldBeEmpty(
			"a lease taken moments ago must still exclude a second claimant, regardless of what clock that "
			+ "claimant's caller supplies -- the lease is judged against the server's clock, not the caller's");
	}
}
