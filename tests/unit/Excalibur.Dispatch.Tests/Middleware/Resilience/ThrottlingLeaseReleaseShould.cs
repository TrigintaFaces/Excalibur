// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.RateLimiting;

using Excalibur.Dispatch.Middleware.Resilience;

namespace Excalibur.Dispatch.Tests.Middleware.Resilience;

/// <summary>
/// The throttling middleware takes a permit from the per-key limiter, then reaches for one from the
/// global limiter. If that second acquire throws -- a cancelled request, or the limiter disposed during
/// shutdown -- the permit already held has to come back. Without that, the per-key limiter loses capacity
/// permanently, one failed request at a time, until it can never grant again.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class ThrottlingLeaseReleaseShould
{
	[Fact]
	public async Task ReturnTheFirstPermitWhenTheSecondLimiterThrows()
	{
		// Arrange -- a single-permit per-key limiter, and a global limiter that is already disposed so
		// its AcquireAsync throws. Disposal is used rather than cancellation because it fails
		// deterministically at exactly the point under test, with no timing window.
		using var specific = new ConcurrencyLimiter(
			new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });

		var global = new ConcurrencyLimiter(
			new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
		global.Dispose();

		// Act
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await ThrottlingMiddleware.AcquireLeaseAsync(specific, global, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		// Assert -- the permit taken from the specific limiter before the throw must have been released,
		// so the very next caller can still get one. Without the release this lease is not acquired.
		using var next = await specific.AcquireAsync(1, CancellationToken.None).ConfigureAwait(false);
		next.IsAcquired.ShouldBeTrue(
			"the permit held when the second acquire threw was never returned, so the per-key limiter " +
			"has permanently lost capacity");
	}

	[Fact]
	public async Task StillGrantWhenBothLimitersHaveCapacity()
	{
		// Liveness arm: the safety arm above must not be able to pass by refusing everything.
		using var specific = new ConcurrencyLimiter(
			new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
		using var global = new ConcurrencyLimiter(
			new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });

		using var lease = await ThrottlingMiddleware
			.AcquireLeaseAsync(specific, global, CancellationToken.None).ConfigureAwait(false);

		lease.IsAcquired.ShouldBeTrue();
	}
}
