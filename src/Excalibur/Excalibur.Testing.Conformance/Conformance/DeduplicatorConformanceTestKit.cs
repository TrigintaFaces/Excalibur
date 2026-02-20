// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Abstractions;

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
public abstract class DeduplicatorConformanceTestKit
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
	protected virtual async Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException()
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
	protected virtual async Task IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException()
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
	protected virtual async Task IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException()
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
	protected virtual async Task IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException()
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
	protected virtual async Task IsDuplicateAsync_NotProcessed_ShouldReturnFalse()
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
	protected virtual async Task MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException()
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
	protected virtual async Task MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException()
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
	protected virtual async Task MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue()
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
	protected virtual async Task IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse()
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
	protected virtual async Task CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount()
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
	protected virtual async Task GetStatistics_ShouldReturnValidStatistics()
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
	protected virtual async Task GetStatistics_AfterChecks_ShouldIncrementTotalChecks()
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
	protected virtual async Task GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected()
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
	protected virtual async Task ClearAsync_ShouldRemoveAllEntries()
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
			await deduplicator.ClearAsync().ConfigureAwait(false);

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
	protected virtual async Task ClearAsync_ShouldResetTrackedMessageCount()
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
			await deduplicator.ClearAsync().ConfigureAwait(false);

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
	protected virtual async Task DisposedDeduplicator_ShouldThrowObjectDisposedException()
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
