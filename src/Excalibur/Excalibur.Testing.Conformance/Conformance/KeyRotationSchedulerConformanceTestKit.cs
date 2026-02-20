// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IKeyRotationScheduler conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateScheduler"/> to verify that
/// your key rotation scheduler implementation conforms to the IKeyRotationScheduler contract.
/// </para>
/// <para>
/// The test kit verifies core scheduling operations including:
/// <list type="bullet">
/// <item><description>CheckAndRotateAsync batch processing of due keys</description></item>
/// <item><description>IsRotationDueAsync policy-based rotation checks</description></item>
/// <item><description>ForceRotateAsync immediate rotation with audit reason</description></item>
/// <item><description>GetNextRotationTimeAsync calculated rotation time</description></item>
/// <item><description>Null parameter validation (ArgumentException)</description></item>
/// <item><description>Graceful handling of non-existent keys</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IKeyRotationScheduler implements automatic key rotation
/// for SOC 2 and GDPR compliance:
/// <list type="bullet">
/// <item><description>Policy-based rotation (90-day default, 30-day high-security, 365-day archival)</description></item>
/// <item><description>ForceRotateAsync requires audit reason for compliance trail</description></item>
/// <item><description>Batch processing with concurrency control</description></item>
/// <item><description>Zero-downtime rotation with key versioning</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>FIRST BackgroundService CONFORMANCE KIT:</strong> KeyRotationService extends BackgroundService.
/// Tests use IKeyRotationScheduler interface methods directly rather than starting the background loop.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class KeyRotationSchedulerConformanceTests : KeyRotationSchedulerConformanceTestKit
/// {
///     protected override (IKeyRotationScheduler Scheduler, IKeyManagementProvider KeyProvider, KeyRotationOptions Options) CreateScheduler()
///     {
///         var keyProvider = new InMemoryKeyManagementProvider(...);
///         var options = new KeyRotationOptions { DefaultPolicy = KeyRotationPolicy.Default };
///         var scheduler = new KeyRotationService(keyProvider, Options.Create(options), NullLogger&lt;KeyRotationService&gt;.Instance);
///         return (scheduler, keyProvider, options);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class KeyRotationSchedulerConformanceTestKit
{
	/// <summary>
	/// Creates a fresh key rotation scheduler instance with its dependencies for testing.
	/// </summary>
	/// <returns>
	/// A tuple containing:
	/// <list type="bullet">
	/// <item><description>Scheduler - The IKeyRotationScheduler implementation to test</description></item>
	/// <item><description>KeyProvider - The IKeyManagementProvider used by the scheduler</description></item>
	/// <item><description>Options - The KeyRotationOptions configuration</description></item>
	/// </list>
	/// </returns>
	/// <remarks>
	/// <para>
	/// The KeyProvider is returned to allow tests to pre-populate keys before testing rotation.
	/// Tests should use KeyProvider.RotateKeyAsync to create test keys.
	/// </para>
	/// <para>
	/// For KeyRotationService, the typical implementation:
	/// </para>
	/// <code>
	/// protected override (IKeyRotationScheduler, IKeyManagementProvider, KeyRotationOptions) CreateScheduler()
	/// {
	///     var keyProvider = new InMemoryKeyManagementProvider(...);
	///     var options = new KeyRotationOptions { DefaultPolicy = KeyRotationPolicy.Default };
	///     var scheduler = new KeyRotationService(keyProvider, Options.Create(options), NullLogger&lt;KeyRotationService&gt;.Instance);
	///     return (scheduler, keyProvider, options);
	/// }
	/// </code>
	/// </remarks>
	protected abstract (IKeyRotationScheduler Scheduler, IKeyManagementProvider KeyProvider, KeyRotationOptions Options) CreateScheduler();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique key ID for test isolation.
	/// </summary>
	/// <returns>A unique key identifier.</returns>
	protected virtual string GenerateKeyId() => $"test-key-{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a test key in the key provider.
	/// </summary>
	/// <param name="keyProvider">The key provider.</param>
	/// <param name="keyId">The key ID to create.</param>
	/// <param name="purpose">Optional key purpose.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The created key metadata.</returns>
	protected virtual async Task<KeyMetadata> CreateTestKeyAsync(
		IKeyManagementProvider keyProvider,
		string keyId,
		string? purpose,
		CancellationToken cancellationToken)
	{
		var result = await keyProvider.RotateKeyAsync(
			keyId,
			EncryptionAlgorithm.Aes256Gcm,
			purpose,
			expiresAt: null,
			cancellationToken).ConfigureAwait(false);

		if (!result.Success || result.NewKey is null)
		{
			throw new TestFixtureAssertionException(
				$"Failed to create test key: {result.ErrorMessage ?? "Unknown error"}");
		}

		return result.NewKey;
	}

	#region CheckAndRotateAsync Tests

	/// <summary>
	/// Verifies that CheckAndRotateAsync returns empty result when no keys exist.
	/// </summary>
	protected virtual async Task CheckAndRotateAsync_NoKeys_ShouldReturnEmptyResult()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act
			var result = await scheduler.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result.KeysChecked != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeysChecked to be 0 with no keys, but got {result.KeysChecked}.");
			}

			if (result.KeysRotated != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeysRotated to be 0 with no keys, but got {result.KeysRotated}.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that CheckAndRotateAsync checks existing keys.
	/// </summary>
	protected virtual async Task CheckAndRotateAsync_WithRecentKeys_ShouldCheckButNotRotate()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Create a recent key (not due for rotation)
			var keyId = GenerateKeyId();
			_ = await CreateTestKeyAsync(keyProvider, keyId, null, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await scheduler.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result.KeysChecked < 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeysChecked to be at least 1, but got {result.KeysChecked}.");
			}

			// Recent key should not be rotated
			if (result.KeysRotated != 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeysRotated to be 0 for recent key, but got {result.KeysRotated}.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that CheckAndRotateAsync includes timestamps.
	/// </summary>
	protected virtual async Task CheckAndRotateAsync_ShouldIncludeTimestamps()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			var beforeCheck = DateTimeOffset.UtcNow;

			// Act
			var result = await scheduler.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

			var afterCheck = DateTimeOffset.UtcNow;

			// Assert
			if (result.StartedAt < beforeCheck || result.StartedAt > afterCheck)
			{
				throw new TestFixtureAssertionException(
					$"Expected StartedAt to be between {beforeCheck} and {afterCheck}, but got {result.StartedAt}.");
			}

			if (result.CompletedAt < result.StartedAt)
			{
				throw new TestFixtureAssertionException(
					$"Expected CompletedAt ({result.CompletedAt}) to be >= StartedAt ({result.StartedAt}).");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region IsRotationDueAsync Tests

	/// <summary>
	/// Verifies that IsRotationDueAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task IsRotationDueAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.IsRotationDueAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected IsRotationDueAsync to throw ArgumentException for null keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that IsRotationDueAsync throws ArgumentException for empty keyId.
	/// </summary>
	protected virtual async Task IsRotationDueAsync_EmptyKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.IsRotationDueAsync(string.Empty, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected IsRotationDueAsync to throw ArgumentException for empty keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that IsRotationDueAsync returns false for non-existent key.
	/// </summary>
	protected virtual async Task IsRotationDueAsync_NonExistentKey_ShouldReturnFalse()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act
			var result = await scheduler.IsRotationDueAsync("non-existent-key", CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"Expected IsRotationDueAsync to return false for non-existent key.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that IsRotationDueAsync returns false for recently created key.
	/// </summary>
	protected virtual async Task IsRotationDueAsync_RecentKey_ShouldReturnFalse()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			var keyId = GenerateKeyId();
			_ = await CreateTestKeyAsync(keyProvider, keyId, null, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await scheduler.IsRotationDueAsync(keyId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"Expected IsRotationDueAsync to return false for recently created key.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region ForceRotateAsync Tests

	/// <summary>
	/// Verifies that ForceRotateAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task ForceRotateAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.ForceRotateAsync(null!, "test reason", CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected ForceRotateAsync to throw ArgumentException for null keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that ForceRotateAsync throws ArgumentException for empty keyId.
	/// </summary>
	protected virtual async Task ForceRotateAsync_EmptyKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.ForceRotateAsync(string.Empty, "test reason", CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected ForceRotateAsync to throw ArgumentException for empty keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that ForceRotateAsync throws ArgumentException for null reason.
	/// </summary>
	protected virtual async Task ForceRotateAsync_NullReason_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			var keyId = GenerateKeyId();

			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.ForceRotateAsync(keyId, null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected ForceRotateAsync to throw ArgumentException for null reason.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that ForceRotateAsync throws ArgumentException for empty reason.
	/// </summary>
	protected virtual async Task ForceRotateAsync_EmptyReason_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			var keyId = GenerateKeyId();

			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.ForceRotateAsync(keyId, string.Empty, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected ForceRotateAsync to throw ArgumentException for empty reason.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that ForceRotateAsync returns failed result for non-existent key.
	/// </summary>
	protected virtual async Task ForceRotateAsync_NonExistentKey_ShouldReturnFailedResult()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act
			var result = await scheduler.ForceRotateAsync(
				"non-existent-key",
				"test reason",
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result.Success)
			{
				throw new TestFixtureAssertionException(
					"Expected ForceRotateAsync to return failed result for non-existent key.");
			}

			if (string.IsNullOrEmpty(result.ErrorMessage))
			{
				throw new TestFixtureAssertionException(
					"Expected failed result to include an error message.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that ForceRotateAsync successfully rotates an existing key.
	/// </summary>
	protected virtual async Task ForceRotateAsync_ExistingKey_ShouldRotateSuccessfully()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			var keyId = GenerateKeyId();
			var originalKey = await CreateTestKeyAsync(keyProvider, keyId, null, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await scheduler.ForceRotateAsync(
				keyId,
				"Security incident response",
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (!result.Success)
			{
				throw new TestFixtureAssertionException(
					$"Expected ForceRotateAsync to succeed, but got error: {result.ErrorMessage}");
			}

			if (result.NewKey is null)
			{
				throw new TestFixtureAssertionException(
					"Expected successful rotation to include NewKey.");
			}

			if (result.NewKey.Version <= originalKey.Version)
			{
				throw new TestFixtureAssertionException(
					$"Expected NewKey.Version ({result.NewKey.Version}) to be greater than original version ({originalKey.Version}).");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region GetNextRotationTimeAsync Tests

	/// <summary>
	/// Verifies that GetNextRotationTimeAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task GetNextRotationTimeAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.GetNextRotationTimeAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected GetNextRotationTimeAsync to throw ArgumentException for null keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetNextRotationTimeAsync throws ArgumentException for empty keyId.
	/// </summary>
	protected virtual async Task GetNextRotationTimeAsync_EmptyKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await scheduler.GetNextRotationTimeAsync(string.Empty, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected GetNextRotationTimeAsync to throw ArgumentException for empty keyId.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetNextRotationTimeAsync returns null for non-existent key.
	/// </summary>
	protected virtual async Task GetNextRotationTimeAsync_NonExistentKey_ShouldReturnNull()
	{
		// Arrange
		var (scheduler, keyProvider, _) = CreateScheduler();
		try
		{
			// Act
			var result = await scheduler.GetNextRotationTimeAsync(
				"non-existent-key",
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result is not null)
			{
				throw new TestFixtureAssertionException(
					$"Expected GetNextRotationTimeAsync to return null for non-existent key, but got {result}.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that GetNextRotationTimeAsync returns calculated time for existing key.
	/// </summary>
	protected virtual async Task GetNextRotationTimeAsync_ExistingKey_ShouldReturnCalculatedTime()
	{
		// Arrange
		var (scheduler, keyProvider, options) = CreateScheduler();
		try
		{
			var keyId = GenerateKeyId();
			var key = await CreateTestKeyAsync(keyProvider, keyId, null, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await scheduler.GetNextRotationTimeAsync(keyId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result is null)
			{
				// AutoRotateEnabled may be false - this is acceptable
				if (options.DefaultPolicy.AutoRotateEnabled)
				{
					throw new TestFixtureAssertionException(
						"Expected GetNextRotationTimeAsync to return a time when AutoRotateEnabled is true.");
				}
			}
			else
			{
				// Expected time should be approximately: CreatedAt + MaxKeyAge
				var expectedTime = key.CreatedAt.Add(options.DefaultPolicy.MaxKeyAge);
				var tolerance = TimeSpan.FromSeconds(5);

				if (Math.Abs((result.Value - expectedTime).TotalSeconds) > tolerance.TotalSeconds)
				{
					throw new TestFixtureAssertionException(
						$"Expected next rotation time around {expectedTime}, but got {result.Value}.");
				}
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
			(scheduler as IDisposable)?.Dispose();
			(keyProvider as IDisposable)?.Dispose();
		}
	}

	#endregion
}
