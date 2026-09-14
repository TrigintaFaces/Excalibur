// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Caching;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for validating <see cref="ICacheTagTracker"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This test kit ensures all <see cref="ICacheTagTracker"/> implementations correctly implement the
/// per-tag version-stamp contract: a tag's current stamp can be resolved (creating one the first time
/// a tag is seen), and a tag can be invalidated by bumping its stamp to a new value.
/// </para>
/// <para>
/// <strong>KEY PATTERN:</strong> VERSION-STAMP — resolve a tag's current stamp, bump it to invalidate.
/// Unlike the key-set model this kit previously validated (register a key under tags, query keys by
/// tag, unregister a key), a tag tracker under this contract never knows which cache keys reference a
/// tag; it only ever answers "what is this tag's current stamp" and "make this tag's stamp different".
/// </para>
/// <para>
/// <strong>METHODS TESTED (2 methods):</strong>
/// <list type="bullet">
/// <item><description><c>GetOrCreateStampAsync</c> - Resolve (or create, if never seen) a tag's current stamp</description></item>
/// <item><description><c>BumpStampAsync</c> - Invalidate a tag by replacing its stamp</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit
/// {
///     protected override ICacheTagTracker CreateTracker() =>
///         new MyCacheTagTracker();
///
///     [Fact]
///     public Task GetOrCreateStampAsync_NewTag_ShouldCreateStamp_Test() =>
///         GetOrCreateStampAsync_NewTag_ShouldCreateStamp();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class CacheTagTrackerConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the cache tag tracker for testing.
	/// </summary>
	/// <returns>A new tracker instance.</returns>
	/// <remarks>
	/// Each test should get a fresh tracker instance to ensure isolation.
	/// Implementers should create a new instance with default configuration.
	/// </remarks>
	protected abstract ICacheTagTracker CreateTracker();

	/// <summary>
	/// Gets a value indicating whether this implementation claims that stamps are shared across
	/// instances through an external backend, so that two separately constructed trackers observe each
	/// other's bumps.
	/// </summary>
	/// <remarks>
	/// <see langword="false"/> by default. An in-process implementation (a private, per-instance
	/// dictionary) does not and must not claim this — two of its instances never share state, so the
	/// cross-instance arms below would fail for a reason that has nothing to do with a defect. Override
	/// to <see langword="true"/> only for an implementation backed by a store that is itself shared
	/// (e.g. Redis, SQL Server) across the instances <see cref="CreateTracker"/> returns.
	/// </remarks>
	protected virtual bool SupportsCrossInstanceSharing => false;

	/// <summary>
	/// The maximum time a cross-instance arm waits for a second instance to observe a change made by the
	/// first, polling in <see cref="CrossInstancePollInterval"/> steps.
	/// </summary>
	/// <remarks>
	/// Convergence is bounded by the implementation's own refresh interval (memoization), not by this
	/// kit, so the wait must comfortably exceed any implementation's configured bound rather than assume
	/// a specific one. Override for an implementation configured with an unusually large refresh window.
	/// </remarks>
	protected virtual TimeSpan CrossInstanceConvergenceTimeout => TimeSpan.FromSeconds(10);

	/// <summary>The interval between polls while waiting for cross-instance convergence.</summary>
	protected virtual TimeSpan CrossInstancePollInterval => TimeSpan.FromMilliseconds(100);

	#region GetOrCreateStampAsync Tests

	/// <summary>
	/// Verifies that <c>GetOrCreateStampAsync</c> creates a non-empty stamp for a tag never seen before.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task GetOrCreateStampAsync_NewTag_ShouldCreateStamp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();

		// Act
		var stamp = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);

		// Assert
		if (string.IsNullOrEmpty(stamp))
		{
			throw new TestFixtureAssertionException(
				"Expected GetOrCreateStampAsync to return a non-empty stamp for a new tag");
		}
	}

	/// <summary>
	/// Verifies that <c>GetOrCreateStampAsync</c> called twice for the same tag, with no bump between
	/// the calls, returns the SAME stamp both times.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task GetOrCreateStampAsync_SameTagNoBump_ShouldReturnSameStamp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();

		// Act
		var first = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);
		var second = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);

		// Assert
		if (!string.Equals(first, second, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Expected GetOrCreateStampAsync to return a stable stamp absent a bump, got '{first}' then '{second}'");
		}
	}

	/// <summary>
	/// Verifies that two DIFFERENT tags resolve to different stamps.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task GetOrCreateStampAsync_DifferentTags_ShouldReturnDifferentStamps()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();

		// Act
		var ordersStamp = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);
		var usersStamp = await tracker.GetOrCreateStampAsync("users", cts.Token).ConfigureAwait(false);

		// Assert
		if (string.Equals(ordersStamp, usersStamp, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Expected two different tags to resolve to different stamps");
		}
	}

	#endregion

	#region BumpStampAsync Tests

	/// <summary>
	/// Verifies that <c>BumpStampAsync</c> changes a tag's stamp to a different value.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task BumpStampAsync_ShouldChangeStamp()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		var before = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);

		// Act
		await tracker.BumpStampAsync("orders", cts.Token).ConfigureAwait(false);
		var after = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);

		// Assert
		if (string.Equals(before, after, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Expected BumpStampAsync to change the tag's stamp");
		}
	}

	/// <summary>
	/// Verifies that <c>BumpStampAsync</c> for a tag never previously resolved is safe and gives the
	/// tag a resolvable stamp afterward.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task BumpStampAsync_NeverResolvedTag_ShouldBeSafeAndResolvable()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();

		// Act - should not throw
		await tracker.BumpStampAsync("never-seen", cts.Token).ConfigureAwait(false);
		var stamp = await tracker.GetOrCreateStampAsync("never-seen", cts.Token).ConfigureAwait(false);

		// Assert
		if (string.IsNullOrEmpty(stamp))
		{
			throw new TestFixtureAssertionException(
				"Expected the tag to have a resolvable stamp after BumpStampAsync");
		}
	}

	/// <summary>
	/// Verifies that bumping one tag does not change a DIFFERENT tag's stamp.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task BumpStampAsync_ShouldNotAffectOtherTags()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tracker = CreateTracker();
		_ = await tracker.GetOrCreateStampAsync("orders", cts.Token).ConfigureAwait(false);
		var usersBefore = await tracker.GetOrCreateStampAsync("users", cts.Token).ConfigureAwait(false);

		// Act
		await tracker.BumpStampAsync("orders", cts.Token).ConfigureAwait(false);
		var usersAfter = await tracker.GetOrCreateStampAsync("users", cts.Token).ConfigureAwait(false);

		// Assert
		if (!string.Equals(usersBefore, usersAfter, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Expected bumping 'orders' to leave 'users' stamp unchanged");
		}
	}

	#endregion

	#region Cross-Instance Sharing Tests

	/// <summary>
	/// Verifies that a bump made through one tracker instance is eventually observed by a SECOND,
	/// independently constructed instance reading through the same shared backend — the property the
	/// key-set model this kit previously validated could not hold, and the reason
	/// <see cref="ICacheTagTracker"/> was redesigned around a per-tag version stamp.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// SAFETY arm. Skipped (never failed) via <see cref="ConformanceTestKit.OnArmSkipped"/> for an
	/// implementation that does not claim <see cref="SupportsCrossInstanceSharing"/> — two in-process
	/// trackers sharing nothing would fail here for a reason unrelated to any defect.
	/// </remarks>
	public virtual async Task CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance()
	{
		if (!SupportsCrossInstanceSharing)
		{
			SkipArm(
				nameof(CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance),
				typeof(ICacheTagTracker),
				"This implementation does not claim cross-instance stamp sharing (SupportsCrossInstanceSharing is false).");
			return;
		}

		RecordArmExecuted(nameof(CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance));

		// Arrange
		using var cts = new CancellationTokenSource(CrossInstanceConvergenceTimeout + TimeSpan.FromSeconds(5));
		const string tag = "cross-instance-bump";
		var instanceA = CreateTracker();
		var instanceB = CreateTracker();

		// instanceB resolves and memoizes the tag's stamp FIRST — this is what makes the arm meaningful.
		// A cold read after the bump would trivially see the new value from any implementation; the
		// property under test is that a STALE, already-cached view converges.
		var stampBeforeOnB = await instanceB.GetOrCreateStampAsync(tag, cts.Token).ConfigureAwait(false);

		// Act
		await instanceA.BumpStampAsync(tag, cts.Token).ConfigureAwait(false);
		var stampAfterOnA = await instanceA.GetOrCreateStampAsync(tag, cts.Token).ConfigureAwait(false);

		// Assert — poll instanceB until it converges to instanceA's post-bump stamp, bounded by
		// CrossInstanceConvergenceTimeout (an implementation may only refresh its memo periodically).
		var deadline = DateTime.UtcNow + CrossInstanceConvergenceTimeout;
		var observedOnB = stampBeforeOnB;
		while (DateTime.UtcNow < deadline)
		{
			observedOnB = await instanceB.GetOrCreateStampAsync(tag, cts.Token).ConfigureAwait(false);
			if (string.Equals(observedOnB, stampAfterOnA, StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(CrossInstancePollInterval, cts.Token).ConfigureAwait(false);
		}

		if (!string.Equals(observedOnB, stampAfterOnA, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Expected a bump on one tracker instance to be observed by a second instance sharing the "
				+ $"same backend within {CrossInstanceConvergenceTimeout}, so an entry written under the "
				+ $"pre-bump stamp on that second instance would be treated as stale. Instance B's stamp "
				+ $"stayed '{observedOnB}'; instance A's post-bump stamp was '{stampAfterOnA}'.");
		}
	}

	/// <summary>
	/// Verifies that a tag NOT bumped resolves to the SAME stamp on a second, independently constructed
	/// instance reading through the same shared backend — a bare cross-instance "hit", proving the two
	/// instances agree rather than each minting an unrelated stamp for the same tag.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// LIVENESS arm, paired with <see cref="CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance"/>:
	/// that arm alone would pass against a tracker that treats every entry as stale regardless of any
	/// bump, so this arm proves an unbumped tag still hits. Skipped for an implementation that does not
	/// claim <see cref="SupportsCrossInstanceSharing"/>, for the same reason as the safety arm.
	/// </remarks>
	public virtual async Task CrossInstanceNoBump_ShouldStillHitOnAnotherInstance()
	{
		if (!SupportsCrossInstanceSharing)
		{
			SkipArm(
				nameof(CrossInstanceNoBump_ShouldStillHitOnAnotherInstance),
				typeof(ICacheTagTracker),
				"This implementation does not claim cross-instance stamp sharing (SupportsCrossInstanceSharing is false).");
			return;
		}

		RecordArmExecuted(nameof(CrossInstanceNoBump_ShouldStillHitOnAnotherInstance));

		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		const string tag = "cross-instance-no-bump";
		var instanceA = CreateTracker();
		var instanceB = CreateTracker();

		// Act — resolve on A, then on a SEPARATE instance B; neither bumps the tag.
		var stampOnA = await instanceA.GetOrCreateStampAsync(tag, cts.Token).ConfigureAwait(false);
		var stampOnB = await instanceB.GetOrCreateStampAsync(tag, cts.Token).ConfigureAwait(false);

		// Assert
		if (!string.Equals(stampOnA, stampOnB, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Expected an unbumped tag to resolve to the SAME stamp on two instances sharing the same "
				+ $"backend, so a bump on either would be the only thing that changes it. Got '{stampOnA}' "
				+ $"on instance A and '{stampOnB}' on instance B.");
		}
	}

	#endregion
}
