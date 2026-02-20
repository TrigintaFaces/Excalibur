// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Security.Cryptography;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for validating <see cref="IKeyEscrowService"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This test kit ensures all <see cref="IKeyEscrowService"/> implementations correctly implement
/// the key escrow contract for secure key backup and disaster recovery.
/// </para>
/// <para>
/// <strong>COMPLIANCE INFRASTRUCTURE PATTERN:</strong> IKeyEscrowService provides secure key
/// backup using Shamir's Secret Sharing for split-knowledge recovery.
/// </para>
/// <para>
/// <strong>KEY PATTERN:</strong> ESCROW-RECOVERY - Backup → Generate Tokens → Recover → Revoke.
/// </para>
/// <para>
/// <strong>METHODS TESTED (5 methods):</strong>
/// <list type="bullet">
/// <item><description><c>BackupKeyAsync</c> - Create encrypted backup of key material</description></item>
/// <item><description><c>RecoverKeyAsync</c> - Recover key using recovery token</description></item>
/// <item><description><c>GenerateRecoveryTokensAsync</c> - Generate Shamir shares for custodians</description></item>
/// <item><description><c>RevokeEscrowAsync</c> - Revoke escrow and invalidate tokens</description></item>
/// <item><description><c>GetEscrowStatusAsync</c> - Get current escrow status</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>FIRST CRYPTOGRAPHIC ALGORITHM:</strong> Shamir's Secret Sharing for split-knowledge recovery.
/// </para>
/// <para>
/// <strong>STATE MACHINE:</strong> Active → Recovered → Expired → Revoked.
/// </para>
/// <para>
/// <strong>DUPLICATE PREVENTION:</strong> Throws KeyEscrowException without AllowOverwrite option.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyKeyEscrowServiceConformanceTests : KeyEscrowServiceConformanceTestKit
/// {
///     protected override IKeyEscrowService CreateService() =>
///         new MyKeyEscrowService(encryptionProvider, logger);
///
///     [Fact]
///     public Task BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt_Test() =>
///         BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class KeyEscrowServiceConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the key escrow service for testing.
	/// </summary>
	/// <returns>A new service instance.</returns>
	/// <remarks>
	/// Each test should get a fresh service instance to ensure isolation.
	/// Implementers must provide IEncryptionProvider and ILogger dependencies.
	/// </remarks>
	protected abstract IKeyEscrowService CreateService();

	/// <summary>
	/// Creates random key material for testing.
	/// </summary>
	/// <param name="size">The size in bytes. Default is 32 (256-bit).</param>
	/// <returns>Random byte array.</returns>
	protected static byte[] CreateKeyMaterial(int size = 32)
	{
		var keyMaterial = new byte[size];
		RandomNumberGenerator.Fill(keyMaterial);
		return keyMaterial;
	}

	#region BackupKeyAsync Tests

	/// <summary>
	/// Verifies that <c>BackupKeyAsync</c> with null keyId throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task BackupKeyAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		// Act & Assert
		try
		{
			_ = await service.BackupKeyAsync(null!, keyMaterial, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected BackupKeyAsync with null keyId to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>BackupKeyAsync</c> with empty key material throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task BackupKeyAsync_EmptyKeyMaterial_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.BackupKeyAsync("key-1", ReadOnlyMemory<byte>.Empty, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected BackupKeyAsync with empty key material to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>BackupKeyAsync</c> with valid key material returns a receipt.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		// Act
		var receipt = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Assert
		if (receipt == null)
		{
			throw new TestFixtureAssertionException("Expected receipt to be non-null");
		}

		if (string.IsNullOrEmpty(receipt.EscrowId))
		{
			throw new TestFixtureAssertionException("Expected EscrowId to be non-empty");
		}

		if (string.IsNullOrEmpty(receipt.KeyHash))
		{
			throw new TestFixtureAssertionException("Expected KeyHash to be non-empty");
		}
	}

	/// <summary>
	/// Verifies that <c>BackupKeyAsync</c> throws for duplicate key without AllowOverwrite.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task BackupKeyAsync_DuplicateKey_ShouldThrowKeyEscrowException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		// First backup
		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Act & Assert - second backup without AllowOverwrite
		try
		{
			_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected BackupKeyAsync with duplicate key to throw KeyEscrowException");
		}
		catch (KeyEscrowException ex)
		{
			if (ex.ErrorCode != KeyEscrowErrorCode.EscrowAlreadyExists)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode to be EscrowAlreadyExists, but got {ex.ErrorCode}");
			}
		}
	}

	#endregion

	#region RecoverKeyAsync Tests

	/// <summary>
	/// Verifies that <c>RecoverKeyAsync</c> with null keyId throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RecoverKeyAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var token = new RecoveryToken
		{
			TokenId = "token-1",
			KeyId = "key-1",
			EscrowId = "escrow-1",
			ShareIndex = 1,
			ShareData = new byte[32],
			TotalShares = 5,
			Threshold = 3,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
		};

		// Act & Assert
		try
		{
			_ = await service.RecoverKeyAsync(null!, token, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected RecoverKeyAsync with null keyId to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>RecoverKeyAsync</c> with non-existent key throws <see cref="KeyEscrowException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RecoverKeyAsync_NonExistentKey_ShouldThrowKeyEscrowException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var token = new RecoveryToken
		{
			TokenId = "token-1",
			KeyId = "non-existent-key",
			EscrowId = "escrow-1",
			ShareIndex = 1,
			ShareData = new byte[32],
			TotalShares = 5,
			Threshold = 3,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
		};

		// Act & Assert
		try
		{
			_ = await service.RecoverKeyAsync("non-existent-key", token, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected RecoverKeyAsync for non-existent key to throw KeyEscrowException");
		}
		catch (KeyEscrowException ex)
		{
			if (ex.ErrorCode != KeyEscrowErrorCode.KeyNotFound)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode to be KeyNotFound, but got {ex.ErrorCode}");
			}
		}
	}

	/// <summary>
	/// Verifies that <c>RecoverKeyAsync</c> with valid token returns original key material.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RecoverKeyAsync_ValidToken_ShouldReturnKeyMaterial()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		// Backup first
		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Generate tokens
		var tokens = await service.GenerateRecoveryTokensAsync("key-1", 5, 3, null, cts.Token).ConfigureAwait(false);

		// Act - use first token for recovery
		var recovered = await service.RecoverKeyAsync("key-1", tokens[0], cts.Token).ConfigureAwait(false);

		// Assert - ROUND-TRIP verification
		if (recovered.Length != keyMaterial.Length)
		{
			throw new TestFixtureAssertionException(
				$"Expected recovered key length {keyMaterial.Length}, but got {recovered.Length}");
		}

		if (!recovered.Span.SequenceEqual(keyMaterial))
		{
			throw new TestFixtureAssertionException(
				"Expected recovered key material to match original (ROUND-TRIP verification failed)");
		}
	}

	/// <summary>
	/// Verifies that <c>RecoverKeyAsync</c> with revoked escrow throws <see cref="KeyEscrowException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RecoverKeyAsync_RevokedEscrow_ShouldThrowKeyEscrowException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		// Backup and get token
		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);
		var tokens = await service.GenerateRecoveryTokensAsync("key-1", 5, 3, null, cts.Token).ConfigureAwait(false);

		// Revoke
		_ = await service.RevokeEscrowAsync("key-1", "test revocation", cts.Token).ConfigureAwait(false);

		// Act & Assert
		try
		{
			_ = await service.RecoverKeyAsync("key-1", tokens[0], cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected RecoverKeyAsync on revoked escrow to throw KeyEscrowException");
		}
		catch (KeyEscrowException ex)
		{
			if (ex.ErrorCode != KeyEscrowErrorCode.EscrowRevoked)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode to be EscrowRevoked, but got {ex.ErrorCode}");
			}
		}
	}

	#endregion

	#region GenerateRecoveryTokensAsync Tests

	/// <summary>
	/// Verifies that <c>GenerateRecoveryTokensAsync</c> with null keyId throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GenerateRecoveryTokensAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoveryTokensAsync(null!, 5, 3, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected GenerateRecoveryTokensAsync with null keyId to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>GenerateRecoveryTokensAsync</c> with threshold less than 2 throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GenerateRecoveryTokensAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoveryTokensAsync("key-1", 5, 1, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected GenerateRecoveryTokensAsync with threshold < 2 to throw ArgumentOutOfRangeException");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>GenerateRecoveryTokensAsync</c> with valid parameters generates correct count.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GenerateRecoveryTokensAsync_ValidParams_ShouldGenerateCorrectCount()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Act
		var tokens = await service.GenerateRecoveryTokensAsync("key-1", 5, 3, null, cts.Token).ConfigureAwait(false);

		// Assert
		if (tokens.Length != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected 5 tokens (custodianCount), but got {tokens.Length}");
		}

		if (!tokens.All(t => t.Threshold == 3))
		{
			throw new TestFixtureAssertionException(
				"Expected all tokens to have Threshold == 3");
		}

		if (!tokens.All(t => t.TotalShares == 5))
		{
			throw new TestFixtureAssertionException(
				"Expected all tokens to have TotalShares == 5");
		}

		// Verify unique share indices
		var uniqueIndices = tokens.Select(t => t.ShareIndex).Distinct().Count();
		if (uniqueIndices != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected 5 unique share indices, but got {uniqueIndices}");
		}
	}

	#endregion

	#region RevokeEscrowAsync Tests

	/// <summary>
	/// Verifies that <c>RevokeEscrowAsync</c> with null keyId throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RevokeEscrowAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.RevokeEscrowAsync(null!, null, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected RevokeEscrowAsync with null keyId to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>RevokeEscrowAsync</c> for non-existent key returns false.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RevokeEscrowAsync_NonExistentKey_ShouldReturnFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act
		var result = await service.RevokeEscrowAsync("non-existent-key", null, cts.Token).ConfigureAwait(false);

		// Assert
		if (result != false)
		{
			throw new TestFixtureAssertionException(
				"Expected RevokeEscrowAsync for non-existent key to return false");
		}
	}

	/// <summary>
	/// Verifies that <c>RevokeEscrowAsync</c> revokes existing escrow and returns true.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task RevokeEscrowAsync_ExistingEscrow_ShouldReturnTrue()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Act
		var result = await service.RevokeEscrowAsync("key-1", "test revocation", cts.Token).ConfigureAwait(false);

		// Assert
		if (result != true)
		{
			throw new TestFixtureAssertionException(
				"Expected RevokeEscrowAsync for existing escrow to return true");
		}

		// Verify status is Revoked
		var status = await service.GetEscrowStatusAsync("key-1", cts.Token).ConfigureAwait(false);
		if (status == null || status.State != EscrowState.Revoked)
		{
			throw new TestFixtureAssertionException(
				$"Expected escrow state to be Revoked, but got {status?.State}");
		}
	}

	#endregion

	#region GetEscrowStatusAsync Tests

	/// <summary>
	/// Verifies that <c>GetEscrowStatusAsync</c> with null keyId throws <see cref="ArgumentException"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetEscrowStatusAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.GetEscrowStatusAsync(null!, cts.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected GetEscrowStatusAsync with null keyId to throw ArgumentException");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <c>GetEscrowStatusAsync</c> for non-existent key returns null.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetEscrowStatusAsync_NonExistentKey_ShouldReturnNull()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();

		// Act
		var status = await service.GetEscrowStatusAsync("non-existent-key", cts.Token).ConfigureAwait(false);

		// Assert
		if (status != null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetEscrowStatusAsync for non-existent key to return null");
		}
	}

	/// <summary>
	/// Verifies that <c>GetEscrowStatusAsync</c> for existing escrow returns status.
	/// </summary>
	/// <returns>A task representing the asynchronous test operation.</returns>
	protected virtual async Task GetEscrowStatusAsync_ExistingEscrow_ShouldReturnStatus()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var service = CreateService();
		var keyMaterial = CreateKeyMaterial();

		_ = await service.BackupKeyAsync("key-1", keyMaterial, null, cts.Token).ConfigureAwait(false);

		// Act
		var status = await service.GetEscrowStatusAsync("key-1", cts.Token).ConfigureAwait(false);

		// Assert
		if (status == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetEscrowStatusAsync for existing escrow to return non-null status");
		}

		if (status.KeyId != "key-1")
		{
			throw new TestFixtureAssertionException(
				$"Expected KeyId to be 'key-1', but got '{status.KeyId}'");
		}

		if (status.State != EscrowState.Active)
		{
			throw new TestFixtureAssertionException(
				$"Expected State to be Active, but got {status.State}");
		}

		if (string.IsNullOrEmpty(status.EscrowId))
		{
			throw new TestFixtureAssertionException(
				"Expected EscrowId to be non-empty");
		}
	}

	#endregion
}
