// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for validating <see cref="IInMemoryDeduplicator"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This test kit ensures all <see cref="IInMemoryDeduplicator"/> implementations correctly implement
/// the duplicate detection contract for message processing in light-mode inbox scenarios.
/// </para>
/// <para>
/// <strong>MESSAGING INFRASTRUCTURE PATTERN:</strong> IInMemoryDeduplicator provides lightweight
/// duplicate detection for message processing when persistent inbox storage is not required.
/// </para>
/// <para>
/// <strong>KEY PATTERN:</strong> DUPLICATE-CHECK - Mark as processed, then subsequent checks detect duplicates.
/// Unlike ROUND-TRIP patterns (ClaimCheckProvider, EncryptionProvider), this pattern validates
/// idempotency through boolean return values rather than data transformation.
/// </para>
/// <para>
/// <strong>METHODS TESTED (5 methods):</strong>
/// <list type="bullet">
/// <item><description><c>IsDuplicateAsync</c> - Check if message already processed (returns bool)</description></item>
/// <item><description><c>MarkProcessedAsync</c> - Mark message as processed</description></item>
/// <item><description><c>CleanupExpiredEntriesAsync</c> - Remove expired entries (returns count)</description></item>
/// <item><description><c>GetStatistics</c> - Get statistics (SYNC method)</description></item>
/// <item><description><c>ClearAsync</c> - Clear all tracked messages</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>EXCEPTION TYPES (Two types!):</strong>
/// <list type="bullet">
/// <item><description><c>ArgumentException</c> - For null/empty/whitespace messageId</description></item>
/// <item><description><c>ArgumentOutOfRangeException</c> - For zero/negative expiry TimeSpan</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyDeduplicatorConformanceTests : DeduplicatorConformanceTestKit
/// {
///     protected override IInMemoryDeduplicator CreateDeduplicator() =>
///         new MyDeduplicator(NullLogger&lt;MyDeduplicator&gt;.Instance);
///
///     [Fact]
///     public Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException_Test() =>
///         IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class DeduplicatorConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the deduplicator for testing.
	/// </summary>
	/// <returns>A new deduplicator instance.</returns>
	/// <remarks>
	/// Each test should get a fresh deduplicator instance to ensure isolation.
	/// Implementers should create a new instance with default configuration.
	/// </remarks>
	protected abstract IInMemoryDeduplicator CreateDeduplicator();

	#region IsDuplicateAsync Tests

	/// <summary>
	/// Verifies that <c>IsDuplicateAsync</c> throws <see cref="ArgumentException"/> when messageId is null.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Act
			_ = await deduplicator.IsDuplicateAsync(null!, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentException when messageId is null");
		}
		catch (ArgumentException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>IsDuplicateAsync</c> throws <see cref="ArgumentException"/> when messageId is empty.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Act
			_ = await deduplicator.IsDuplicateAsync(string.Empty, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentException when messageId is empty");
		}
		catch (ArgumentException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>IsDuplicateAsync</c> throws <see cref="ArgumentException"/> when messageId is whitespace.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Act
			_ = await deduplicator.IsDuplicateAsync("   ", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentException when messageId is whitespace");
		}
		catch (ArgumentException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>IsDuplicateAsync</c> throws <see cref="ArgumentOutOfRangeException"/> when expiry is zero.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Act
			_ = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.Zero, cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentOutOfRangeException when expiry is zero");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>IsDuplicateAsync</c> returns false for a message that was never processed.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task IsDuplicateAsync_NotProcessed_ShouldReturnFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Act
			var isDuplicate = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert
			if (isDuplicate)
			{
				throw new TestFixtureAssertionException(
					"Expected IsDuplicateAsync to return false for unprocessed message");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	#endregion

	#region MarkProcessedAsync Tests

	/// <summary>
	/// Verifies that <c>MarkProcessedAsync</c> throws <see cref="ArgumentException"/> when messageId is null.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Act
			await deduplicator.MarkProcessedAsync(null!, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentException when messageId is null");
		}
		catch (ArgumentException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>MarkProcessedAsync</c> throws <see cref="ArgumentOutOfRangeException"/> when expiry is zero.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Act
			await deduplicator.MarkProcessedAsync(messageId, TimeSpan.Zero, cts.Token).ConfigureAwait(false);

			// Assert - should not reach here
			throw new TestFixtureAssertionException(
				"Expected ArgumentOutOfRangeException when expiry is zero");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>MarkProcessedAsync</c> marks a message so subsequent <c>IsDuplicateAsync</c> returns true.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// This is the core DUPLICATE-CHECK pattern test:
	/// Mark → Check → Should be duplicate.
	/// </remarks>
	public virtual async Task MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Act
			await deduplicator.MarkProcessedAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			var isDuplicate = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert
			if (!isDuplicate)
			{
				throw new TestFixtureAssertionException(
					"Expected IsDuplicateAsync to return true after MarkProcessedAsync");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	#endregion

	#region Expiry Tests

	/// <summary>
	/// Verifies that a message with expired TTL is no longer detected as duplicate.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// Uses short TTL (50ms) and waits (100ms) to verify expiry behavior.
	/// After expiry, the message should be treated as new (not duplicate).
	/// </remarks>
	public virtual async Task IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();
		var shortExpiry = TimeSpan.FromMilliseconds(50);

		try
		{
			// Act - Mark with short TTL
			await deduplicator.MarkProcessedAsync(messageId, shortExpiry, cts.Token).ConfigureAwait(false);

			// Wait for expiry
			await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token).ConfigureAwait(false);

			// Check if still duplicate
			var isDuplicate = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Assert
			if (isDuplicate)
			{
				throw new TestFixtureAssertionException(
					"Expected IsDuplicateAsync to return false for expired entry");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>CleanupExpiredEntriesAsync</c> removes expired entries and returns the count.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var shortExpiry = TimeSpan.FromMilliseconds(50);

		try
		{
			// Mark multiple messages with short TTL
			await deduplicator.MarkProcessedAsync("msg-1", shortExpiry, cts.Token).ConfigureAwait(false);
			await deduplicator.MarkProcessedAsync("msg-2", shortExpiry, cts.Token).ConfigureAwait(false);
			await deduplicator.MarkProcessedAsync("msg-3", shortExpiry, cts.Token).ConfigureAwait(false);

			// Wait for expiry
			await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token).ConfigureAwait(false);

			// Act
			var removedCount = await deduplicator.CleanupExpiredEntriesAsync(cts.Token).ConfigureAwait(false);

			// Assert
			if (removedCount < 3)
			{
				throw new TestFixtureAssertionException(
					$"Expected CleanupExpiredEntriesAsync to return at least 3 but got {removedCount}");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	#endregion

	#region GetStatistics Tests (SYNC method)

	/// <summary>
	/// Verifies that <c>GetStatistics</c> returns valid statistics with correct structure.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// GetStatistics is a SYNC method - the second conformance kit with a synchronous method
	/// (first was ShouldUseClaimCheck in ClaimCheckProvider).
	/// </remarks>
	public virtual async Task GetStatistics_ShouldReturnValidStatistics()
	{
		// Arrange
		var deduplicator = CreateDeduplicator();

		try
		{
			// Act
			var stats = deduplicator.GetStatistics();

			// Assert
			if (stats == null)
			{
				throw new TestFixtureAssertionException("GetStatistics should not return null");
			}

			if (stats.TrackedMessageCount < 0)
			{
				throw new TestFixtureAssertionException(
					$"TrackedMessageCount should not be negative but was {stats.TrackedMessageCount}");
			}

			if (stats.TotalChecks < 0)
			{
				throw new TestFixtureAssertionException(
					$"TotalChecks should not be negative but was {stats.TotalChecks}");
			}

			if (stats.DuplicatesDetected < 0)
			{
				throw new TestFixtureAssertionException(
					$"DuplicatesDetected should not be negative but was {stats.DuplicatesDetected}");
			}

			if (stats.EstimatedMemoryUsageBytes < 0)
			{
				throw new TestFixtureAssertionException(
					$"EstimatedMemoryUsageBytes should not be negative but was {stats.EstimatedMemoryUsageBytes}");
			}

			if (stats.CapturedAt == default)
			{
				throw new TestFixtureAssertionException("CapturedAt should be set");
			}

			await Task.CompletedTask.ConfigureAwait(false);
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>GetStatistics</c> increments TotalChecks after IsDuplicateAsync calls.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task GetStatistics_AfterChecks_ShouldIncrementTotalChecks()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Get initial stats
			var initialStats = deduplicator.GetStatistics();
			var initialChecks = initialStats.TotalChecks;

			// Perform some checks
			_ = await deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			_ = await deduplicator.IsDuplicateAsync("msg-2", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			_ = await deduplicator.IsDuplicateAsync("msg-3", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Act
			var updatedStats = deduplicator.GetStatistics();

			// Assert
			if (updatedStats.TotalChecks != initialChecks + 3)
			{
				throw new TestFixtureAssertionException(
					$"Expected TotalChecks to be {initialChecks + 3} but was {updatedStats.TotalChecks}");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>GetStatistics</c> increments DuplicatesDetected when duplicates are found.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Mark a message
			await deduplicator.MarkProcessedAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Get initial stats
			var initialStats = deduplicator.GetStatistics();
			var initialDuplicates = initialStats.DuplicatesDetected;

			// Check for duplicate (should be detected)
			_ = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			_ = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Act
			var updatedStats = deduplicator.GetStatistics();

			// Assert
			if (updatedStats.DuplicatesDetected != initialDuplicates + 2)
			{
				throw new TestFixtureAssertionException(
					$"Expected DuplicatesDetected to be {initialDuplicates + 2} but was {updatedStats.DuplicatesDetected}");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	#endregion

	#region ClearAsync Tests

	/// <summary>
	/// Verifies that <c>ClearAsync</c> removes all tracked messages.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task ClearAsync_ShouldRemoveAllEntries()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		try
		{
			// Mark a message
			await deduplicator.MarkProcessedAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Verify it's tracked as duplicate
			var isDuplicateBefore =
				await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			if (!isDuplicateBefore)
			{
				throw new TestFixtureAssertionException(
					"Message should be duplicate before clear");
			}

			// Act
			await deduplicator.ClearAsync(cts.Token).ConfigureAwait(false);

			// Assert - message should no longer be duplicate
			var isDuplicateAfter = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			if (isDuplicateAfter)
			{
				throw new TestFixtureAssertionException(
					"Message should not be duplicate after clear");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies that <c>ClearAsync</c> resets TrackedMessageCount to zero.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task ClearAsync_ShouldResetTrackedMessageCount()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();

		try
		{
			// Mark some messages
			await deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
			await deduplicator.MarkProcessedAsync("msg-2", TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);

			// Verify messages are tracked
			var statsBefore = deduplicator.GetStatistics();
			if (statsBefore.TrackedMessageCount < 2)
			{
				throw new TestFixtureAssertionException(
					$"Expected at least 2 tracked messages before clear but got {statsBefore.TrackedMessageCount}");
			}

			// Act
			await deduplicator.ClearAsync(cts.Token).ConfigureAwait(false);

			// Assert
			var statsAfter = deduplicator.GetStatistics();
			if (statsAfter.TrackedMessageCount != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected TrackedMessageCount to be 0 after clear but was {statsAfter.TrackedMessageCount}");
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	#endregion

	#region Disposable Tests

	/// <summary>
	/// Verifies that operations throw <see cref="ObjectDisposedException"/> after the deduplicator is disposed.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	/// <remarks>
	/// The InMemoryDeduplicator implements <see cref="IDisposable"/> for cleanup timer disposal.
	/// After disposal, operations should throw ObjectDisposedException.
	/// Note: Some implementations may not throw, which is also acceptable.
	/// </remarks>
	public virtual async Task DisposedDeduplicator_ShouldThrowObjectDisposedException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var deduplicator = CreateDeduplicator();
		var messageId = Guid.NewGuid().ToString();

		// Dispose if IDisposable
		if (deduplicator is IDisposable disposable)
		{
			disposable.Dispose();

			// Act & Assert - operations after dispose may throw ObjectDisposedException
			// Note: Implementation-specific - some implementations may not throw
			try
			{
				_ = await deduplicator.IsDuplicateAsync(messageId, TimeSpan.FromMinutes(5), cts.Token).ConfigureAwait(false);
				// If no exception, that's also acceptable for some implementations
			}
			catch (ObjectDisposedException)
			{
				// Expected behavior
			}
		}
		else
		{
			// If not IDisposable, test passes (implementation doesn't need disposal)
			await Task.CompletedTask.ConfigureAwait(false);
		}
	}

	#endregion

	#region Concurrency Tests

	/// <summary>Callers released into one race, together.</summary>
	private const int RaceParticipants = 8;

	/// <summary>Independent races the arm runs, each on keys of its own.</summary>
	private const int RaceIterations = 48;

	/// <summary>
	/// Long enough that nothing marked during a race can expire before that race is over.
	/// </summary>
	/// <remarks>
	/// An entry that expired mid-race would make its removal by the concurrent sweep legitimate, and the
	/// arm could then no longer tell a deduplicator that lost a marker from one whose marker simply aged
	/// out. Pinning the window well past the race removes that reading entirely.
	/// </remarks>
	private static readonly TimeSpan RaceRetention = TimeSpan.FromMinutes(5);

	/// <summary>The operation each participant in a race attempts.</summary>
	private enum RaceOperation
	{
		/// <summary>Atomically claim the contested id. The only operation that reports a winner.</summary>
		Claim,

		/// <summary>Unconditionally record a bystander id, so an insert is in flight during the sweep.</summary>
		MarkBystander,

		/// <summary>Sweep expired entries across the whole set while the inserts above are landing.</summary>
		Sweep,

		/// <summary>Read the contested id, which on some implementations evicts as a side effect.</summary>
		Probe,
	}

	/// <summary>What one participant in a race was asked to do, and what happened to it.</summary>
	private readonly record struct RaceOutcome(RaceOperation Operation, bool Won, Exception? Failure);

	/// <summary>
	/// SAFETY and LIVENESS: concurrent callers racing to claim one message id must elect exactly one winner,
	/// and a marker written during a race must survive the sweep that ran alongside it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Check-then-act is the entire risk of a deduplicator, and it is the one thing no sequential arm can
	/// observe. Every other arm in this kit calls a method, waits for it, then calls the next — the single
	/// condition under which no deduplicator can fail, because there is never a second caller to lose to. A
	/// duplicate is produced by two callers being told, at the same instant, that each of them is the first
	/// to see this message.
	/// </para>
	/// <para>
	/// The winner election is asserted through <see cref="IClaimableDeduplicator.TryClaimAsync"/>, because
	/// that is the only operation on this contract that reports a winner at all.
	/// <see cref="IInMemoryDeduplicator.MarkProcessedAsync"/> returns no value, so two callers that both
	/// observed <see cref="IInMemoryDeduplicator.IsDuplicateAsync"/> as <see langword="false"/> and then
	/// both marked are indistinguishable from one caller doing it twice — there is nothing for the arm to
	/// count. A deduplicator that does not implement the claim capability is therefore raced on the marker
	/// half alone rather than being held to a property its contract cannot express.
	/// </para>
	/// <para>
	/// The race deliberately MIXES the operations rather than repeating one call. The pair that destroys
	/// state here is <em>insert against sweep</em>: a cleanup that enumerates, decides what has expired,
	/// and then applies its removals is holding a decision taken before the concurrent insert landed, so it
	/// deletes an entry that was never expired. The same pair reached through a non-thread-safe map instead
	/// throws mid-enumeration, and reached through a read that evicts lazily deletes on the probe's behalf.
	/// All three end the same way: a marker for a message that was genuinely handled is gone, the next
	/// redelivery finds nothing, and the handler runs a second time — the single outcome a deduplicator
	/// exists to prevent. N identical claims could never reach any of them, which is why the participants
	/// here are drawn across claim, mark, sweep, and probe.
	/// </para>
	/// <para>
	/// <b>Exactly one winner</b> is asserted rather than at most one. At-most-one is satisfied perfectly by a
	/// deduplicator that refuses every caller, which would suppress every message as a duplicate and pass
	/// this arm while doing so. The two uncontested checks taken before the race — a claim on a fresh id
	/// must be granted, and an id nobody has marked must not read as a duplicate — fail such an
	/// implementation deterministically, by a message naming its own cause, so a refusing deduplicator is
	/// never reported as a concurrency failure. The second of those is what fails the opposite overreach: an
	/// implementation that answers "duplicate" to everything would otherwise satisfy the marker assertions
	/// without storing anything at all.
	/// </para>
	/// <para>
	/// <b>What this arm cannot prove.</b> It is sound but not complete. A correct deduplicator passes it
	/// under every interleaving, so a failure here is always a real defect and never a scheduling artefact.
	/// The converse does not hold: the arm cannot force an interleaving from outside the implementation, so
	/// one carrying this fault passes on any run where the insert and the sweep happen not to overlap inside
	/// the offending window. A barrier releasing every participant at one instant, and many independent
	/// races, make that outcome progressively less likely — they do not make it impossible. Read a pass here
	/// as evidence, not proof; read a failure as conclusive.
	/// </para>
	/// </remarks>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task ConcurrentClaimAndSweep_MustElectExactlyOneWinner_AndKeepTheMarker()
	{
		var deduplicator = CreateDeduplicator();

		try
		{
			var claimable = deduplicator as IClaimableDeduplicator;

			await AssertUncontestedBehaviourIsLiveAsync(deduplicator, claimable).ConfigureAwait(false);

			for (var iteration = 1; iteration <= RaceIterations; iteration++)
			{
				var contestedId = GenerateRaceMessageId();
				var bystanderIds = new string[RaceParticipants];
				for (var slot = 0; slot < RaceParticipants; slot++)
				{
					bystanderIds[slot] = GenerateRaceMessageId();
				}

				var outcomes = await RunOneRaceAsync(deduplicator, claimable, contestedId, bystanderIds)
					.ConfigureAwait(false);

				AssertNoParticipantThrew(outcomes, iteration, contestedId);

				if (claimable is not null)
				{
					AssertExactlyOneWinner(outcomes, iteration, contestedId);

					await AssertContestedMarkerSurvivedAsync(deduplicator, outcomes, iteration, contestedId)
						.ConfigureAwait(false);
				}

				await AssertBystanderMarkersSurvivedAsync(deduplicator, outcomes, bystanderIds, iteration)
					.ConfigureAwait(false);
			}
		}
		finally
		{
			TryDispose(deduplicator);
		}
	}

	/// <summary>
	/// Verifies the deduplicator both grants an uncontested claim and reports an unknown id as new, before
	/// any race is run.
	/// </summary>
	/// <param name="deduplicator">The deduplicator under test.</param>
	/// <param name="claimable">The same deduplicator through its claim capability, or <see langword="null"/>.</param>
	/// <returns>A task that completes when both directions have been shown live.</returns>
	/// <remarks>
	/// These two checks fail the two implementations that would otherwise satisfy every concurrency
	/// assertion vacuously, and they do it deterministically — no schedule can make either flap. One that
	/// grants nothing can never elect two winners; one that answers "duplicate" to everything can never
	/// lose a marker. Neither half detects the other's defect, so both are taken, and both are reported
	/// before the race so a refusing or over-reporting implementation is never misread as contention.
	/// </remarks>
	private static async Task AssertUncontestedBehaviourIsLiveAsync(
		IInMemoryDeduplicator deduplicator,
		IClaimableDeduplicator? claimable)
	{
		var unknownId = GenerateRaceMessageId();

		var reportedDuplicate = await deduplicator
			.IsDuplicateAsync(unknownId, RaceRetention, CancellationToken.None)
			.ConfigureAwait(false);

		if (reportedDuplicate)
		{
			throw new TestFixtureAssertionException(
				$"IsDuplicateAsync reported id '{unknownId}' as a duplicate on an id the deduplicator had "
				+ "never seen, with no competing caller. Every message would be suppressed as a duplicate "
				+ "and no handler would ever run. This is reported before the concurrency assertions "
				+ "because an implementation that calls everything a duplicate can never lose a marker, and "
				+ "would otherwise satisfy them without storing anything.");
		}

		if (claimable is null)
		{
			return;
		}

		var claimId = GenerateRaceMessageId();

		var claimed = await claimable.TryClaimAsync(claimId, RaceRetention, CancellationToken.None)
			.ConfigureAwait(false);

		if (claimed is null)
		{
			throw new TestFixtureAssertionException(
				$"TryClaimAsync refused id '{claimId}' on an id the deduplicator had never seen, with no "
				+ "competing caller. Every message would be treated as already handled on its first "
				+ "delivery. Reported separately from the concurrency assertions because an implementation "
				+ "that grants no claims cannot elect two winners, and would pass a race by never electing "
				+ "anyone.");
		}
	}

	/// <summary>
	/// Runs one race: every participant released together, over one contested id and fresh bystanders.
	/// </summary>
	/// <param name="deduplicator">The deduplicator under test.</param>
	/// <param name="claimable">The claim capability, or <see langword="null"/> when unsupported.</param>
	/// <param name="contestedId">The id every claim participant competes for.</param>
	/// <param name="bystanderIds">A fresh id per slot, for the participants that mark rather than claim.</param>
	/// <returns>What each participant was asked to do and what happened to it, in slot order.</returns>
	private static async Task<RaceOutcome[]> RunOneRaceAsync(
		IInMemoryDeduplicator deduplicator,
		IClaimableDeduplicator? claimable,
		string contestedId,
		string[] bystanderIds)
	{
		var outcomes = new RaceOutcome[RaceParticipants];

		// Participants run on dedicated threads rather than pooled ones. A pool that injects threads on a
		// delay would release the participants seconds apart, and participants that do not overlap are a
		// sequence wearing the shape of a race.
		using var gate = new Barrier(RaceParticipants + 1);

		var racers = new Task[RaceParticipants];

		for (var slot = 0; slot < RaceParticipants; slot++)
		{
			var index = slot;
			var operation = SelectRaceOperation(index, claimable is not null);

			racers[index] = Task.Factory.StartNew(
				async () =>
				{
					// Parked until the last participant arrives, so the calls are issued at one instant
					// instead of in whatever order the threads happened to start.
					gate.SignalAndWait();

					try
					{
						var won = false;

						switch (operation)
						{
							case RaceOperation.Claim:
								won = await claimable!
									.TryClaimAsync(contestedId, RaceRetention, CancellationToken.None)
									.ConfigureAwait(false) is not null;
								break;

							case RaceOperation.MarkBystander:
								await deduplicator
									.MarkProcessedAsync(bystanderIds[index], RaceRetention, CancellationToken.None)
									.ConfigureAwait(false);
								break;

							case RaceOperation.Sweep:
								_ = await deduplicator
									.CleanupExpiredEntriesAsync(CancellationToken.None)
									.ConfigureAwait(false);
								break;

							default:
								_ = await deduplicator
									.IsDuplicateAsync(contestedId, RaceRetention, CancellationToken.None)
									.ConfigureAwait(false);
								break;
						}

						outcomes[index] = new RaceOutcome(operation, won, null);
					}
					catch (Exception ex)
					{
						// Recorded rather than rethrown. Awaiting the set surfaces one exception and
						// discards the others, and the discarded ones are the other half of the
						// interleaving — the half that says which operations were in flight together.
						outcomes[index] = new RaceOutcome(operation, false, ex);
					}
				},
				CancellationToken.None,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default).Unwrap();
		}

		gate.SignalAndWait();

		await Task.WhenAll(racers).ConfigureAwait(false);

		return outcomes;
	}

	/// <summary>
	/// Spreads the participants across claim, mark, sweep, and probe.
	/// </summary>
	/// <param name="slot">The participant's position in the race.</param>
	/// <param name="claimSupported">Whether the claim capability is implemented.</param>
	/// <returns>The operation this participant attempts.</returns>
	/// <remarks>
	/// A deduplicator without the claim capability has no winner to elect, so its claim slots become marks:
	/// the insert-against-sweep pair is still raced at full width, and only the assertion the contract
	/// cannot express is dropped.
	/// </remarks>
	private static RaceOperation SelectRaceOperation(int slot, bool claimSupported) => (slot % 4) switch
	{
		0 or 1 => claimSupported ? RaceOperation.Claim : RaceOperation.MarkBystander,
		2 => RaceOperation.MarkBystander,
		_ => slot < RaceParticipants / 2 ? RaceOperation.Sweep : RaceOperation.Probe,
	};

	/// <summary>
	/// Fails when any participant threw.
	/// </summary>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <param name="iteration">Which race this was.</param>
	/// <param name="contestedId">The contested message id.</param>
	private static void AssertNoParticipantThrew(RaceOutcome[] outcomes, int iteration, string contestedId)
	{
		var thrown = Array.FindAll(outcomes, static o => o.Failure is not null);

		if (thrown.Length > 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} on id '{contestedId}': {thrown.Length} of "
				+ $"{RaceParticipants} concurrent callers threw. Losing a claim is a return value, not an "
				+ "exception, and a sweep running while entries are being inserted is ordinary operation, "
				+ "not an error: a throwing caller is indistinguishable to the host from an outage and is "
				+ "retried as one. An InvalidOperationException here is the signature of a sweep "
				+ "enumerating a collection another participant is writing to. " + DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails unless exactly one participant was told it created the claim.
	/// </summary>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <param name="iteration">Which race this was.</param>
	/// <param name="contestedId">The contested message id.</param>
	private static void AssertExactlyOneWinner(RaceOutcome[] outcomes, int iteration, string contestedId)
	{
		var claimants = Array.FindAll(outcomes, static o => o.Operation == RaceOperation.Claim).Length;
		var winners = Array.FindAll(outcomes, static o => o.Won).Length;

		if (winners > 1)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} told {winners} of {claimants} concurrent claimants "
				+ $"that each of them was the first to see id '{contestedId}'. Every one of them proceeds to "
				+ $"run the handler, so this message produces {winners} sets of side effects instead of one "
				+ "— the duplicate execution a deduplicator exists to prevent. TryClaimAsync is reading, "
				+ "deciding, and writing as three steps with nothing holding the record still in between. "
				+ "Make the decision and the write a single atomic step. " + DescribeRace(outcomes));
		}

		if (winners == 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} refused all {claimants} concurrent claimants of id "
				+ $"'{contestedId}', so no caller holds it and every one of them skips the message as a "
				+ "duplicate: the message is never handled by anyone. Contention must elect a winner, not "
				+ "annul the round. Note the uncontested claim taken before this race succeeded, so the "
				+ "deduplicator does grant claims — it loses the winner specifically under contention. "
				+ DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails when the winner's claim marker did not survive the sweep that ran alongside it.
	/// </summary>
	/// <param name="deduplicator">The deduplicator under test.</param>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <param name="iteration">Which race this was.</param>
	/// <param name="contestedId">The contested message id.</param>
	/// <returns>A task that completes when the marker has been shown to survive.</returns>
	/// <remarks>
	/// A successful claim doubles as the dedup marker, so the winner writes nothing further. That is what
	/// makes a destroyed marker visible here as the absence of the very record the winner was just
	/// promised, rather than as a second winner somewhere else.
	/// </remarks>
	private static async Task AssertContestedMarkerSurvivedAsync(
		IInMemoryDeduplicator deduplicator,
		RaceOutcome[] outcomes,
		int iteration,
		string contestedId)
	{
		var duplicate = await deduplicator
			.IsDuplicateAsync(contestedId, RaceRetention, CancellationToken.None)
			.ConfigureAwait(false);

		if (!duplicate)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} elected one winner for id '{contestedId}', and the "
				+ "deduplicator then reports that id as NOT a duplicate. A successful claim IS the marker, "
				+ "so the record the winner was promised is gone: the next redelivery of this message finds "
				+ "nothing and runs the handler again. The concurrent sweep removed an entry that had not "
				+ "expired — it applied a removal decided before the claim landed — or the concurrent read "
				+ "evicted it. Decide what to remove and remove it in one atomic step, against the live "
				+ "set rather than a snapshot of it. " + DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails when any marker written during the race is missing afterwards.
	/// </summary>
	/// <param name="deduplicator">The deduplicator under test.</param>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <param name="bystanderIds">The per-slot ids, of which the marking slots' were written.</param>
	/// <param name="iteration">Which race this was.</param>
	/// <returns>A task that completes when every written marker has been shown to survive.</returns>
	private static async Task AssertBystanderMarkersSurvivedAsync(
		IInMemoryDeduplicator deduplicator,
		RaceOutcome[] outcomes,
		string[] bystanderIds,
		int iteration)
	{
		for (var slot = 0; slot < outcomes.Length; slot++)
		{
			if (outcomes[slot].Operation != RaceOperation.MarkBystander)
			{
				continue;
			}

			var markedId = bystanderIds[slot];

			var duplicate = await deduplicator
				.IsDuplicateAsync(markedId, RaceRetention, CancellationToken.None)
				.ConfigureAwait(false);

			if (!duplicate)
			{
				throw new TestFixtureAssertionException(
					$"Race {iteration} of {RaceIterations}: id '{markedId}' was marked processed during the "
					+ "race, with a retention window far longer than the race itself, and the deduplicator "
					+ "now reports it as NOT a duplicate. The marker was written and is gone. Its handler's "
					+ "message will be executed a second time on redelivery. The insert landed while a "
					+ "concurrent sweep held a removal set computed before it, so the sweep deleted an "
					+ "entry it had never examined. " + DescribeRace(outcomes));
			}
		}
	}

	/// <summary>
	/// Renders what every participant was asked to do and what it was told.
	/// </summary>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <returns>A one-line account of the race.</returns>
	/// <remarks>
	/// A count alone names the symptom and discards the evidence. Which operations were in flight together,
	/// and which of them were told they had won, is the whole of what distinguishes an unguarded claim from
	/// a sweep working off a stale snapshot, and it cannot be recovered by re-running: the next run
	/// interleaves differently.
	/// </remarks>
	private static string DescribeRace(RaceOutcome[] outcomes) =>
		"Every caller in this race, in slot order: " + string.Join("; ", outcomes.Select(DescribeOutcome)) + ".";

	/// <summary>
	/// Renders one participant's operation and result.
	/// </summary>
	/// <param name="outcome">The participant's result.</param>
	/// <param name="slot">The participant's position in the race.</param>
	/// <returns>A short account of that participant.</returns>
	private static string DescribeOutcome(RaceOutcome outcome, int slot)
	{
		var result = outcome.Failure is not null
			? $"THREW {outcome.Failure.GetType().Name}: {outcome.Failure.Message}"
			: outcome.Operation == RaceOperation.Claim ? outcome.Won ? "WON" : "lost" : "done";

		return $"slot {slot} {outcome.Operation} -> {result}";
	}

	/// <summary>
	/// Generates a message id for one participant in a race.
	/// </summary>
	/// <returns>A unique message identifier.</returns>
	private static string GenerateRaceMessageId() => Guid.NewGuid().ToString("N");

	#endregion Concurrency Tests

	#region Helper Methods

	/// <summary>
	/// Safely disposes the deduplicator if it implements <see cref="IDisposable"/>.
	/// </summary>
	/// <param name="deduplicator">The deduplicator to dispose.</param>
	private static void TryDispose(IInMemoryDeduplicator deduplicator)
	{
		if (deduplicator is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	#endregion
}
