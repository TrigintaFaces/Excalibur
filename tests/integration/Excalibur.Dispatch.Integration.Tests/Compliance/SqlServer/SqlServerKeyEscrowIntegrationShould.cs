// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Security.Cryptography;
using Excalibur.Compliance.SqlServer;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;
using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Integration tests for SQL Server Key Escrow service using TestContainers.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
public sealed class SqlServerKeyEscrowIntegrationShould : IAsyncLifetime, IDisposable
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly ILogger<SqlServerKeyEscrowService> _logger;
	private readonly IEncryptionProvider _encryptionProvider;
	private SqlServerKeyEscrowService? _service;

	public SqlServerKeyEscrowIntegrationShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
		_logger = A.Fake<ILogger<SqlServerKeyEscrowService>>();
		_encryptionProvider = A.Fake<IEncryptionProvider>();
	}

	public async Task InitializeAsync()
	{
		// Create the required tables
		await CreateTablesAsync();

		// Setup encryption provider mock
		SetupEncryptionProviderMock();

		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = "dbo",
			TableName = "KeyEscrow",
			TokensTableName = "KeyEscrowTokens",
			DefaultTokenExpiration = TimeSpan.FromHours(24),
			CommandTimeoutSeconds = 30
		});

		_service = new SqlServerKeyEscrowService(options, _encryptionProvider, _logger);
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_service?.Dispose();
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringNotConfigured()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = null!
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new SqlServerKeyEscrowService(options, _encryptionProvider, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(null!, _encryptionProvider, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenEncryptionProviderIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = "test"
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = "test"
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(options, _encryptionProvider, null!));
	}

	[Fact]
	public async Task BackupKey_StoresEncryptedKey()
	{
		// Arrange
		var keyId = $"backup-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act
		var receipt = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Assert
		_ = receipt.ShouldNotBeNull();
		receipt.KeyId.ShouldBe(keyId);
		receipt.EscrowId.ShouldNotBeNullOrEmpty();
		receipt.EscrowedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
		receipt.KeyHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task BackupKey_WithOptions()
	{
		// Arrange
		var keyId = $"backup-options-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		var options = new EscrowOptions
		{
			TenantId = "tenant-123",
			Purpose = "pii-encryption",
			ExpiresIn = TimeSpan.FromDays(365)
		};

		// Act
		var receipt = await _service.BackupKeyAsync(keyId, keyMaterial, options, CancellationToken.None);

		// Assert
		_ = receipt.ShouldNotBeNull();
		receipt.KeyId.ShouldBe(keyId);
		_ = receipt.ExpiresAt.ShouldNotBeNull();
		receipt.ExpiresAt.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(364));
	}

	[Fact]
	public async Task GetEscrowStatus_ReturnsStatus()
	{
		// Arrange
		var keyId = $"status-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act
		var status = await _service.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert
		_ = status.ShouldNotBeNull();
		status.KeyId.ShouldBe(keyId);
		status.State.ShouldBe(EscrowState.Active);
		status.IsRecoverable.ShouldBeTrue();
	}

	[Fact]
	public async Task GetEscrowStatus_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var status = await _service.GetEscrowStatusAsync(keyId, CancellationToken.None);

		// Assert
		status.ShouldBeNull();
	}

	[Fact]
	public async Task GenerateRecoveryTokens_CreatesTokens()
	{
		// Arrange
		var keyId = $"tokens-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act
		var tokens = await _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Assert
		_ = tokens.ShouldNotBeNull();
		tokens.Length.ShouldBe(5);
		tokens.All(t => t.KeyId == keyId).ShouldBeTrue();
		tokens.All(t => t.Threshold == 3).ShouldBeTrue();
		tokens.All(t => t.TotalShares == 5).ShouldBeTrue();
		tokens.Select(t => t.ShareIndex).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public async Task GenerateRecoveryTokens_ThrowsForInvalidCustodianCount()
	{
		// Arrange
		var keyId = $"invalid-custodian-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 1, threshold: 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokens_ThrowsForInvalidThreshold()
	{
		// Arrange
		var keyId = $"invalid-threshold-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokens_ThrowsWhenThresholdExceedsCustodianCount()
	{
		// Arrange
		var keyId = $"exceed-threshold-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 5, null, CancellationToken.None));
	}

	[Fact]
	public async Task RevokeEscrow_MarksAsRevoked()
	{
		// Arrange
		var keyId = $"revoke-test-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		// Act
		var result = await _service.RevokeEscrowAsync(keyId, "Security incident", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		var status = await _service.GetEscrowStatusAsync(keyId, CancellationToken.None);
		_ = status.ShouldNotBeNull();
		status.State.ShouldBe(EscrowState.Revoked);
		status.IsRecoverable.ShouldBeFalse();
	}

	[Fact]
	public async Task RevokeEscrow_ReturnsFalse_WhenNotFound()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var result = await _service.RevokeEscrowAsync(keyId, null, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RevokeEscrow_InvalidatesAllTokens()
	{
		// Arrange
		var keyId = $"revoke-tokens-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		_ = await _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		// Act
		_ = await _service.RevokeEscrowAsync(keyId, "Key compromised", CancellationToken.None);

		// Assert
		var status = await _service.GetEscrowStatusAsync(keyId, CancellationToken.None);
		_ = status.ShouldNotBeNull();
		status.ActiveTokenCount.ShouldBe(0);
	}

	[Fact]
	public async Task BackupKey_ThrowsForEmptyKeyMaterial()
	{
		// Arrange
		var keyId = $"empty-key-{Guid.NewGuid():N}";

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _service.BackupKeyAsync(keyId, ReadOnlyMemory<byte>.Empty, null, CancellationToken.None));
	}

	[Fact]
	public async Task BackupKey_ThrowsForNullKeyId()
	{
		// Arrange
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _service!.BackupKeyAsync(null!, keyMaterial, null, CancellationToken.None));
	}

	private void SetupEncryptionProviderMock()
	{
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken _) =>
			{
				// Simple test encryption - just XOR with a constant
				var encrypted = new byte[data.Length];
				for (var i = 0; i < data.Length; i++)
				{
					encrypted[i] = (byte)(data[i] ^ 0x55);
				}

				return Task.FromResult(new EncryptedData
				{
					Ciphertext = encrypted,
					Iv = RandomNumberGenerator.GetBytes(12),
					AuthTag = RandomNumberGenerator.GetBytes(16),
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					KeyId = "test-key",
					KeyVersion = 1
				});
			});

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily(call =>
			{
				var data = call.GetArgument<EncryptedData>(0);
				// Simple test decryption - XOR with same constant
				var decrypted = new byte[data.Ciphertext.Length];
				for (var i = 0; i < data.Ciphertext.Length; i++)
				{
					decrypted[i] = (byte)(data.Ciphertext[i] ^ 0x55);
				}

				return Task.FromResult(decrypted);
			});
	}

	private async Task CreateTablesAsync()
	{
		var createTablesSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrow')
            BEGIN
                CREATE TABLE dbo.KeyEscrow (
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

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrowTokens')
            BEGIN
                CREATE TABLE dbo.KeyEscrowTokens (
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

		await _fixture.ExecuteScriptAsync(createTablesSql);
	}
}
