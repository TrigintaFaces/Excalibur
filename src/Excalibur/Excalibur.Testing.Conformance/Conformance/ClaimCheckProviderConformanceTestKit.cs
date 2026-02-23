// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IClaimCheckProvider conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateProvider"/> to verify that
/// your claim check provider implementation conforms to the IClaimCheckProvider contract.
/// </para>
/// <para>
/// The test kit verifies core claim check operations including:
/// <list type="bullet">
/// <item><description>Store payload and verify ClaimCheckReference metadata</description></item>
/// <item><description>Round-trip: Store → Retrieve → Compare (same as EncryptionProvider)</description></item>
/// <item><description>Delete existing payload returns true, non-existent returns false</description></item>
/// <item><description>ShouldUseClaimCheck threshold detection (SYNC method!)</description></item>
/// <item><description>Null parameter validation (ArgumentNullException)</description></item>
/// <item><description>TTL expiration throws InvalidOperationException on retrieve</description></item>
/// <item><description>Retrieve non-existent throws KeyNotFoundException</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>ENTERPRISE INTEGRATION PATTERN:</strong> IClaimCheckProvider implements the Claim Check
/// pattern for handling large message payloads in distributed messaging systems.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class InMemoryClaimCheckProviderConformanceTests : ClaimCheckProviderConformanceTestKit
/// {
///     protected override IClaimCheckProvider CreateProvider()
///     {
///         var options = Options.Create(new ClaimCheckOptions
///         {
///             PayloadThreshold = 1024,  // 1KB for easier testing
///             DefaultTtl = TimeSpan.Zero  // Disable TTL by default
///         });
///         return new InMemoryClaimCheckProvider(options);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class ClaimCheckProviderConformanceTestKit
{
	/// <summary>
	/// Creates a fresh claim check provider instance for testing.
	/// </summary>
	/// <returns>An IClaimCheckProvider implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// Configure with short TTL or zero TTL for test isolation.
	/// Set PayloadThreshold to a small value (e.g., 1024 bytes) for easier testing.
	/// </para>
	/// </remarks>
	protected abstract IClaimCheckProvider CreateProvider();

	/// <summary>
	/// Creates a provider with a specific TTL for expiration tests.
	/// </summary>
	/// <param name="ttl">The TTL duration.</param>
	/// <returns>An IClaimCheckProvider with the specified TTL.</returns>
	protected abstract IClaimCheckProvider CreateProviderWithTtl(TimeSpan ttl);

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates test payload data.
	/// </summary>
	/// <param name="size">The size of the payload in bytes.</param>
	/// <returns>Random payload bytes.</returns>
	protected virtual byte[] GeneratePayload(int size = 256)
	{
		var payload = new byte[size];
		System.Security.Cryptography.RandomNumberGenerator.Fill(payload);
		return payload;
	}

	#region Store Tests

	/// <summary>
	/// Verifies that StoreAsync throws ArgumentNullException for null payload.
	/// </summary>
	protected virtual async Task StoreAsync_NullPayload_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await provider.StoreAsync(null!, CancellationToken.None, null).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected StoreAsync to throw ArgumentNullException for null payload.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that StoreAsync populates all required ClaimCheckReference fields.
	/// </summary>
	protected virtual async Task StoreAsync_ShouldPopulateReferenceMetadata()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var payload = GeneratePayload();

			// Act
			var reference = await provider.StoreAsync(payload, CancellationToken.None, null).ConfigureAwait(false);

			// Assert
			if (reference is null)
			{
				throw new TestFixtureAssertionException("Expected StoreAsync to return ClaimCheckReference.");
			}

			if (string.IsNullOrEmpty(reference.Id))
			{
				throw new TestFixtureAssertionException("Expected Id to be populated.");
			}

			if (string.IsNullOrEmpty(reference.BlobName))
			{
				throw new TestFixtureAssertionException("Expected BlobName to be populated.");
			}

			if (string.IsNullOrEmpty(reference.Location))
			{
				throw new TestFixtureAssertionException("Expected Location to be populated.");
			}

			if (reference.Size != payload.Length)
			{
				throw new TestFixtureAssertionException(
					$"Expected Size to be {payload.Length}, but got {reference.Size}.");
			}

			if (reference.StoredAt == default)
			{
				throw new TestFixtureAssertionException("Expected StoredAt to be populated.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that StoreAsync preserves metadata.
	/// </summary>
	protected virtual async Task StoreAsync_WithMetadata_ShouldPreserveMetadata()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var payload = GeneratePayload();
			var metadata = new ClaimCheckMetadata
			{
				MessageId = "msg-123",
				MessageType = "TestMessage",
				ContentType = "application/octet-stream",
				CorrelationId = "corr-456"
			};

			// Act
			var reference = await provider.StoreAsync(payload, CancellationToken.None, metadata).ConfigureAwait(false);

			// Assert
			if (reference.Metadata is null)
			{
				throw new TestFixtureAssertionException("Expected Metadata to be preserved.");
			}

			if (reference.Metadata.MessageId != "msg-123")
			{
				throw new TestFixtureAssertionException(
					$"Expected MessageId 'msg-123', but got '{reference.Metadata.MessageId}'.");
			}

			if (reference.Metadata.CorrelationId != "corr-456")
			{
				throw new TestFixtureAssertionException(
					$"Expected CorrelationId 'corr-456', but got '{reference.Metadata.CorrelationId}'.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Retrieve Tests

	/// <summary>
	/// Verifies that RetrieveAsync throws ArgumentNullException for null reference.
	/// </summary>
	protected virtual async Task RetrieveAsync_NullReference_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await provider.RetrieveAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RetrieveAsync to throw ArgumentNullException for null reference.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that RetrieveAsync throws KeyNotFoundException for non-existent reference.
	/// </summary>
	protected virtual async Task RetrieveAsync_NonExistent_ShouldThrowKeyNotFoundException()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var fakeReference = new ClaimCheckReference
			{
				Id = "non-existent-claim-id",
				BlobName = "fake/blob",
				Location = "inmemory://fake/non-existent"
			};

			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await provider.RetrieveAsync(fakeReference, CancellationToken.None).ConfigureAwait(false);
			}
			catch (KeyNotFoundException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RetrieveAsync to throw KeyNotFoundException for non-existent reference.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Delete Tests

	/// <summary>
	/// Verifies that DeleteAsync throws ArgumentNullException for null reference.
	/// </summary>
	protected virtual async Task DeleteAsync_NullReference_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await provider.DeleteAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected DeleteAsync to throw ArgumentNullException for null reference.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that DeleteAsync returns true for existing payload.
	/// </summary>
	protected virtual async Task DeleteAsync_ExistingPayload_ShouldReturnTrue()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var payload = GeneratePayload();
			var reference = await provider.StoreAsync(payload, CancellationToken.None, null).ConfigureAwait(false);

			// Act
			var result = await provider.DeleteAsync(reference, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (!result)
			{
				throw new TestFixtureAssertionException(
					"Expected DeleteAsync to return true for existing payload.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that DeleteAsync returns false for non-existent payload.
	/// </summary>
	protected virtual async Task DeleteAsync_NonExistent_ShouldReturnFalse()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var fakeReference = new ClaimCheckReference
			{
				Id = "non-existent-claim-id",
				BlobName = "fake/blob",
				Location = "inmemory://fake/non-existent"
			};

			// Act
			var result = await provider.DeleteAsync(fakeReference, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"Expected DeleteAsync to return false for non-existent payload.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region ShouldUseClaimCheck Tests (SYNC!)

	/// <summary>
	/// Verifies that ShouldUseClaimCheck throws ArgumentNullException for null payload.
	/// </summary>
	protected virtual Task ShouldUseClaimCheck_NullPayload_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				_ = provider.ShouldUseClaimCheck(null!);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected ShouldUseClaimCheck to throw ArgumentNullException for null payload.");
			}

			return Task.CompletedTask;
		}
		finally
		{
			// Cleanup is async, but this test is sync
			_ = CleanupAsync();
		}
	}

	/// <summary>
	/// Verifies that ShouldUseClaimCheck returns false for small payloads.
	/// </summary>
	protected virtual Task ShouldUseClaimCheck_BelowThreshold_ShouldReturnFalse()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Default threshold is typically large (256KB)
			// For this test, we use a small payload that should be below any reasonable threshold
			var smallPayload = new byte[100];

			// Act
			var result = provider.ShouldUseClaimCheck(smallPayload);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"Expected ShouldUseClaimCheck to return false for payload below threshold.");
			}

			return Task.CompletedTask;
		}
		finally
		{
			_ = CleanupAsync();
		}
	}

	/// <summary>
	/// Verifies that ShouldUseClaimCheck returns true for large payloads.
	/// </summary>
	protected virtual Task ShouldUseClaimCheck_AboveThreshold_ShouldReturnTrue()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			// Create payload larger than default threshold (256KB)
			var largePayload = new byte[300_000];

			// Act
			var result = provider.ShouldUseClaimCheck(largePayload);

			// Assert
			if (!result)
			{
				throw new TestFixtureAssertionException(
					"Expected ShouldUseClaimCheck to return true for payload above threshold.");
			}

			return Task.CompletedTask;
		}
		finally
		{
			_ = CleanupAsync();
		}
	}

	#endregion

	#region Round-Trip Tests

	/// <summary>
	/// Verifies that Store then Retrieve returns the original payload.
	/// </summary>
	protected virtual async Task RoundTrip_StoreRetrieve_ShouldReturnOriginalPayload()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var originalPayload = GeneratePayload();

			// Act
			var reference = await provider.StoreAsync(originalPayload, CancellationToken.None, null).ConfigureAwait(false);
			var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (retrieved is null)
			{
				throw new TestFixtureAssertionException("Expected RetrieveAsync to return payload.");
			}

			if (retrieved.Length != originalPayload.Length)
			{
				throw new TestFixtureAssertionException(
					$"Expected retrieved length {originalPayload.Length}, but got {retrieved.Length}.");
			}

			for (var i = 0; i < originalPayload.Length; i++)
			{
				if (retrieved[i] != originalPayload[i])
				{
					throw new TestFixtureAssertionException(
						$"Retrieved payload differs from original at position {i}.");
				}
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that Store then Retrieve works with text data.
	/// </summary>
	protected virtual async Task RoundTrip_TextData_ShouldPreserveContent()
	{
		// Arrange
		var provider = CreateProvider();
		try
		{
			var originalText = "Hello, World! This is a test of the Claim Check pattern for large message handling.";
			var originalPayload = Encoding.UTF8.GetBytes(originalText);

			// Act
			var reference = await provider.StoreAsync(originalPayload, CancellationToken.None, null).ConfigureAwait(false);
			var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var retrievedText = Encoding.UTF8.GetString(retrieved);
			if (retrievedText != originalText)
			{
				throw new TestFixtureAssertionException(
					$"Expected '{originalText}', but got '{retrievedText}'.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Expiration Tests

	/// <summary>
	/// Verifies that RetrieveAsync throws InvalidOperationException for expired payload.
	/// </summary>
	protected virtual async Task RetrieveAsync_ExpiredPayload_ShouldThrowInvalidOperationException()
	{
		// Arrange - create provider with very short TTL
		var provider = CreateProviderWithTtl(TimeSpan.FromMilliseconds(50));
		try
		{
			var payload = GeneratePayload();
			var reference = await provider.StoreAsync(payload, CancellationToken.None, null).ConfigureAwait(false);

			// Wait for expiration
			await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);

			// Act & Assert
			var caughtException = false;
			try
			{
				_ = await provider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException ex) when (ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase))
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RetrieveAsync to throw InvalidOperationException for expired payload.");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion
}
