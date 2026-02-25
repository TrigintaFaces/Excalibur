// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Security.Cryptography;
using Excalibur.Compliance.SqlServer;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.EndToEnd;

/// <summary>
/// End-to-end integration tests for compliance encryption scenarios.
/// Tests full workflows across multiple providers.
/// </summary>
[Collection(ComplianceMultiContainerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
public sealed class ComplianceEncryptionEndToEndShould : IAsyncLifetime, IDisposable
{
	private readonly ComplianceMultiContainerFixture _fixture;
	private readonly ILogger<SqlServerKeyEscrowService> _escrowLogger;
	private IEncryptionProvider? _encryptionProvider;
	private SqlServerKeyEscrowService? _escrowService;

	public ComplianceEncryptionEndToEndShould(ComplianceMultiContainerFixture fixture)
	{
		_fixture = fixture;
		_escrowLogger = A.Fake<ILogger<SqlServerKeyEscrowService>>();
	}

	public async Task InitializeAsync()
	{
		// Set up SQL Server tables
		await CreateKeyEscrowTablesAsync();

		// Create mock encryption provider for tests
		_encryptionProvider = CreateMockEncryptionProvider();

		// Create escrow service
		var escrowOptions = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _fixture.SqlServer.ConnectionString,
			Schema = "dbo",
			TableName = "KeyEscrow_E2E",
			TokensTableName = "KeyEscrowTokens_E2E",
			DefaultTokenExpiration = TimeSpan.FromHours(24),
			CommandTimeoutSeconds = 30
		});

		_escrowService = new SqlServerKeyEscrowService(escrowOptions, _encryptionProvider, _escrowLogger);
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_escrowService?.Dispose();
	}

	[Fact]
	public async Task PerformFullKeyEscrowWorkflow()
	{
		// Arrange
		var keyId = $"e2e-workflow-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		var escrowOptions = new EscrowOptions
		{
			TenantId = "tenant-e2e",
			Purpose = "e2e-testing"
		};

		// Act - Full workflow
		// 1. Backup key
		var receipt = await _escrowService.BackupKeyAsync(keyId, keyMaterial, escrowOptions, CancellationToken.None);

		// 2. Check status
		var status = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// 3. Generate recovery tokens
		var tokens = await _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		// 4. Verify status after token generation
		var statusWithTokens = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert
		_ = receipt.ShouldNotBeNull();
		receipt.KeyId.ShouldBe(keyId);

		_ = status.ShouldNotBeNull();
		status.State.ShouldBe(EscrowState.Active);
		status.IsRecoverable.ShouldBeTrue();

		_ = tokens.ShouldNotBeNull();
		tokens.Length.ShouldBe(3);

		_ = statusWithTokens.ShouldNotBeNull();
		statusWithTokens.ActiveTokenCount.ShouldBe(3);
	}

	[Fact]
	public async Task HandleMultipleTenantIsolation()
	{
		// Arrange
		var tenant1KeyId = $"tenant1-key-{Guid.NewGuid():N}";
		var tenant2KeyId = $"tenant2-key-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		var tenant1Options = new EscrowOptions { TenantId = "tenant-001", Purpose = "encryption" };
		var tenant2Options = new EscrowOptions { TenantId = "tenant-002", Purpose = "encryption" };

		// Act
		_ = await _escrowService.BackupKeyAsync(tenant1KeyId, keyMaterial, tenant1Options, CancellationToken.None);
		_ = await _escrowService.BackupKeyAsync(tenant2KeyId, keyMaterial, tenant2Options, CancellationToken.None);

		var tenant1Status = await _escrowService.GetEscrowStatusAsync(tenant1KeyId, CancellationToken.None);
		var tenant2Status = await _escrowService.GetEscrowStatusAsync(tenant2KeyId, CancellationToken.None);

		// Assert
		_ = tenant1Status.ShouldNotBeNull();
		_ = tenant2Status.ShouldNotBeNull();
		tenant1Status.KeyId.ShouldNotBe(tenant2Status.KeyId);
	}

	[Fact]
	public async Task EnforceThresholdRecoveryRules()
	{
		// Arrange
		var keyId = $"threshold-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act - Generate tokens with specific threshold
		var tokens = await _escrowService.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Assert
		tokens.Length.ShouldBe(5);
		tokens.All(t => t.Threshold == 3).ShouldBeTrue();
		tokens.All(t => t.TotalShares == 5).ShouldBeTrue();
		tokens.Select(t => t.ShareIndex).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public async Task HandleRevocationProperly()
	{
		// Arrange
		var keyId = $"revocation-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		_ = await _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		// Act
		var revoked = await _escrowService.RevokeEscrowAsync(keyId, "Security audit required", CancellationToken.None);

		// Assert
		revoked.ShouldBeTrue();

		var status = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);
		_ = status.ShouldNotBeNull();
		status.State.ShouldBe(EscrowState.Revoked);
		status.IsRecoverable.ShouldBeFalse();
		status.ActiveTokenCount.ShouldBe(0);
	}

	[Fact]
	public async Task SupportMultipleKeyBackups()
	{
		// Arrange & Act - Create multiple keys
		var keyIds = new List<string>();
		for (var i = 0; i < 5; i++)
		{
			var keyId = $"multi-key-{i}-{Guid.NewGuid():N}";
			var keyMaterial = RandomNumberGenerator.GetBytes(32);
			_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
			keyIds.Add(keyId);
		}

		// Assert - All keys should be retrievable
		foreach (var keyId in keyIds)
		{
			var status = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);
			_ = status.ShouldNotBeNull();
			status.State.ShouldBe(EscrowState.Active);
		}
	}

	[Fact]
	public async Task TrackRecoveryAttempts()
	{
		// Arrange
		var keyId = $"recovery-attempts-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Get initial status
		var initialStatus = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert
		_ = initialStatus.ShouldNotBeNull();
		initialStatus.RecoveryAttempts.ShouldBe(0);
	}

	[Fact]
	public async Task HandleConcurrentKeyEscrowOperations()
	{
		// Arrange
		var baseKeyId = $"concurrent-{Guid.NewGuid():N}";

		// Act - Create multiple keys concurrently
		var tasks = Enumerable.Range(0, 10)
			.Select(i => _escrowService.BackupKeyAsync(
				$"{baseKeyId}-{i}",
				RandomNumberGenerator.GetBytes(32),
				null,
				CancellationToken.None))
			.ToList();

		var results = await Task.WhenAll(tasks);

		// Assert - All should succeed
		results.All(r => r != null).ShouldBeTrue();
		results.Select(r => r.KeyId).Distinct().Count().ShouldBe(10);
	}

	[Fact]
	public async Task ValidateKeyHashIntegrity()
	{
		// Arrange
		var keyId = $"hash-integrity-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act
		var receipt = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		var status = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert
		receipt.KeyHash.ShouldNotBeNullOrEmpty();
		_ = status.ShouldNotBeNull();
		status.KeyId.ShouldBe(keyId);
	}

	[Fact]
	public async Task HandleExpirationSettings()
	{
		// Arrange
		var keyId = $"expiration-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		var options = new EscrowOptions
		{
			ExpiresIn = TimeSpan.FromDays(30)
		};

		// Act
		var receipt = await _escrowService.BackupKeyAsync(keyId, keyMaterial, options, CancellationToken.None);

		// Assert
		_ = receipt.ExpiresAt.ShouldNotBeNull();
		receipt.ExpiresAt.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(29));
		receipt.ExpiresAt.Value.ShouldBeLessThan(DateTimeOffset.UtcNow.AddDays(31));
	}

	[Fact]
	public async Task PreventDuplicateTokenGeneration()
	{
		// Arrange
		var keyId = $"dup-tokens-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act - Generate tokens twice
		var tokens1 = await _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);
		var tokens2 = await _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		// Assert - Both should succeed but with different token IDs
		tokens1.Select(t => t.TokenId).ShouldNotBe(tokens2.Select(t => t.TokenId));
	}

	[Fact]
	public async Task MaintainAuditTrailForOperations()
	{
		// Arrange
		var keyId = $"audit-trail-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act - Perform operations that should be auditable
		var receipt = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		_ = await _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		var status = await _escrowService.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert - Timestamps should be tracked
		receipt.EscrowedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
		_ = status.ShouldNotBeNull();
		status.EscrowedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task ValidatePurposeBasedKeyOrganization()
	{
		// Arrange
		var piiKeyId = $"pii-key-{Guid.NewGuid():N}";
		var paymentKeyId = $"payment-key-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		var piiOptions = new EscrowOptions { Purpose = "pii-encryption" };
		var paymentOptions = new EscrowOptions { Purpose = "payment-processing" };

		// Act
		_ = await _escrowService.BackupKeyAsync(piiKeyId, keyMaterial, piiOptions, CancellationToken.None);
		_ = await _escrowService.BackupKeyAsync(paymentKeyId, keyMaterial, paymentOptions, CancellationToken.None);

		var piiStatus = await _escrowService.GetEscrowStatusAsync(piiKeyId, CancellationToken.None);
		var paymentStatus = await _escrowService.GetEscrowStatusAsync(paymentKeyId, CancellationToken.None);

		// Assert
		_ = piiStatus.ShouldNotBeNull();
		_ = paymentStatus.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleVariableKeySizes()
	{
		// Arrange & Act - Test different key sizes
		var key128 = $"key-128-{Guid.NewGuid():N}";
		var key256 = $"key-256-{Guid.NewGuid():N}";
		var key512 = $"key-512-{Guid.NewGuid():N}";

		_ = await _escrowService.BackupKeyAsync(key128, RandomNumberGenerator.GetBytes(16), null, CancellationToken.None);
		_ = await _escrowService.BackupKeyAsync(key256, RandomNumberGenerator.GetBytes(32), null, CancellationToken.None);
		_ = await _escrowService.BackupKeyAsync(key512, RandomNumberGenerator.GetBytes(64), null, CancellationToken.None);

		// Assert - All should be retrievable
		var status128 = await _escrowService.GetEscrowStatusAsync(key128, CancellationToken.None);
		var status256 = await _escrowService.GetEscrowStatusAsync(key256, CancellationToken.None);
		var status512 = await _escrowService.GetEscrowStatusAsync(key512, CancellationToken.None);

		_ = status128.ShouldNotBeNull();
		_ = status256.ShouldNotBeNull();
		_ = status512.ShouldNotBeNull();
	}

	[Fact]
	public async Task EnforceMinimumCustodianRequirements()
	{
		// Arrange
		var keyId = $"min-custodian-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert - Should enforce minimum custodian count
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 1, threshold: 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task EnforceMinimumThresholdRequirements()
	{
		// Arrange
		var keyId = $"min-threshold-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert - Should enforce minimum threshold
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task PreventThresholdExceedingCustodianCount()
	{
		// Arrange
		var keyId = $"exceed-threshold-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _escrowService.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _escrowService.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 5, null, CancellationToken.None));
	}

	[Fact]
	public async Task HandleNonExistentKeyOperations()
	{
		// Arrange
		var nonExistentKeyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var status = await _escrowService.GetEscrowStatusAsync(nonExistentKeyId, CancellationToken.None);
		var revokeResult = await _escrowService.RevokeEscrowAsync(nonExistentKeyId, null, CancellationToken.None);

		// Assert
		status.ShouldBeNull();
		revokeResult.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateEmptyKeyMaterialRejection()
	{
		// Arrange
		var keyId = $"empty-key-{Guid.NewGuid():N}";

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _escrowService.BackupKeyAsync(keyId, ReadOnlyMemory<byte>.Empty, null, CancellationToken.None));
	}

	[Fact]
	public async Task ValidateNullKeyIdRejection()
	{
		// Arrange
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _escrowService!.BackupKeyAsync(null!, keyMaterial, null, CancellationToken.None));
	}

	private IEncryptionProvider CreateMockEncryptionProvider()
	{
		var provider = A.Fake<IEncryptionProvider>();

		_ = A.CallTo(() => provider.EncryptAsync(
				A<byte[]>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken _) =>
			{
				// Simple XOR for testing
				var encrypted = new byte[data.Length];
				for (var i = 0; i < data.Length; i++)
				{
					encrypted[i] = (byte)(data[i] ^ 0xAA);
				}

				return Task.FromResult(new EncryptedData
				{
					Ciphertext = encrypted,
					Iv = RandomNumberGenerator.GetBytes(12),
					AuthTag = RandomNumberGenerator.GetBytes(16),
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					KeyId = "e2e-test-key",
					KeyVersion = 1
				});
			});

		_ = A.CallTo(() => provider.DecryptAsync(
				A<EncryptedData>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily(call =>
			{
				var data = call.GetArgument<EncryptedData>(0);
				// Reverse XOR
				var decrypted = new byte[data.Ciphertext.Length];
				for (var i = 0; i < data.Ciphertext.Length; i++)
				{
					decrypted[i] = (byte)(data.Ciphertext[i] ^ 0xAA);
				}

				return Task.FromResult(decrypted);
			});

		return provider;
	}

	private async Task CreateKeyEscrowTablesAsync()
	{
		var createTablesSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrow_E2E')
            BEGIN
                CREATE TABLE dbo.KeyEscrow_E2E (
                    EscrowId NVARCHAR(64) NOT NULL PRIMARY KEY,
                    KeyId NVARCHAR(256) NOT NULL,
                    EncryptedKey VARBINARY(MAX) NOT NULL,
                    KeyHash NVARCHAR(128) NOT NULL,
                    Algorithm INT NOT NULL,
                    Iv VARBINARY(64) NOT NULL,
                    AuthTag VARBINARY(64) NOT NULL,
                    MasterKeyId NVARCHAR(256) NOT NULL,
                    MasterKeyVersion INT NOT NULL,
                    State INT NOT NULL,
                    EscrowedAt DATETIMEOFFSET NOT NULL,
                    ExpiresAt DATETIMEOFFSET NULL,
                    RevokedAt DATETIMEOFFSET NULL,
                    RevocationReason NVARCHAR(1000) NULL,
                    RecoveryAttempts INT NOT NULL DEFAULT 0,
                    LastRecoveryAttempt DATETIMEOFFSET NULL,
                    TenantId NVARCHAR(256) NULL,
                    Purpose NVARCHAR(256) NULL,
                    Metadata NVARCHAR(MAX) NULL
                )
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrowTokens_E2E')
            BEGIN
                CREATE TABLE dbo.KeyEscrowTokens_E2E (
                    TokenId NVARCHAR(64) NOT NULL PRIMARY KEY,
                    KeyId NVARCHAR(256) NOT NULL,
                    EscrowId NVARCHAR(64) NOT NULL,
                    ShareIndex INT NOT NULL,
                    TotalShares INT NOT NULL,
                    Threshold INT NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL,
                    ExpiresAt DATETIMEOFFSET NOT NULL,
                    IsUsed BIT NOT NULL DEFAULT 0
                )
            END";

		await _fixture.SqlServer.ExecuteScriptAsync(createTablesSql);
	}
}
