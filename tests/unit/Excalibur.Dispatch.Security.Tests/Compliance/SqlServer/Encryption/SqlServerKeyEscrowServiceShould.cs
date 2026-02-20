// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer;
namespace Excalibur.Dispatch.Security.Tests.Compliance.SqlServer.Encryption;

/// <summary>
/// Unit tests for <see cref="SqlServerKeyEscrowService"/>.
/// </summary>
/// <remarks>
/// These tests validate constructor behavior, parameter validation, and disposal.
/// Integration tests with TestContainers are required for full database operations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class SqlServerKeyEscrowServiceShould : IDisposable
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly ILogger<SqlServerKeyEscrowService> _logger;
	private SqlServerKeyEscrowService? _sut;

	public SqlServerKeyEscrowServiceShould()
	{
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_logger = NullLogger<SqlServerKeyEscrowService>.Instance;
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(
				null!,
				_encryptionProvider,
				_logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenEncryptionProviderIsNull()
	{
		// Arrange
		var options = CreateOptions("Server=localhost;Database=test;");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(
				options,
				null!,
				_logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = CreateOptions("Server=localhost;Database=test;");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerKeyEscrowService(
				options,
				_encryptionProvider,
				null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var options = CreateOptions(string.Empty);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new SqlServerKeyEscrowService(
				options,
				_encryptionProvider,
				_logger));
	}

	[Fact]
	public void CreateInstance_WhenAllParametersValid()
	{
		// Arrange
		var options = CreateOptions("Server=localhost;Database=test;");

		// Act
		_sut = new SqlServerKeyEscrowService(
			options,
			_encryptionProvider,
			_logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region BackupKeyAsync Parameter Validation Tests

	[Fact]
	public async Task BackupKeyAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Arrange
		_sut = CreateSut();
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.BackupKeyAsync(null!, keyMaterial, null, CancellationToken.None));
	}

	[Fact]
	public async Task BackupKeyAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Arrange
		_sut = CreateSut();
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.BackupKeyAsync(string.Empty, keyMaterial, null, CancellationToken.None));
	}

	[Fact]
	public async Task BackupKeyAsync_ThrowArgumentException_WhenKeyIdIsWhitespace()
	{
		// Arrange
		_sut = CreateSut();
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.BackupKeyAsync("   ", keyMaterial, null, CancellationToken.None));
	}

	[Fact]
	public async Task BackupKeyAsync_ThrowArgumentException_WhenKeyMaterialIsEmpty()
	{
		// Arrange
		_sut = CreateSut();
		var keyMaterial = Array.Empty<byte>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.BackupKeyAsync("test-key", keyMaterial, null, CancellationToken.None));
	}

	#endregion BackupKeyAsync Parameter Validation Tests

	#region RecoverKeyAsync Parameter Validation Tests

	[Fact]
	public async Task RecoverKeyAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Arrange
		_sut = CreateSut();
		var token = CreateRecoveryToken();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RecoverKeyAsync(null!, token, CancellationToken.None));
	}

	[Fact]
	public async Task RecoverKeyAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Arrange
		_sut = CreateSut();
		var token = CreateRecoveryToken();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RecoverKeyAsync(string.Empty, token, CancellationToken.None));
	}

	[Fact]
	public async Task RecoverKeyAsync_ThrowArgumentNullException_WhenTokenIsNull()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RecoverKeyAsync("test-key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task RecoverKeyAsync_ThrowUnauthorizedAccessException_WhenTokenIsExpired()
	{
		// Arrange
		_sut = CreateSut();
		var expiredToken = new RecoveryToken
		{
			TokenId = "test-token",
			KeyId = "test-key",
			EscrowId = "escrow-1",
			ShareIndex = 1,
			ShareData = new byte[] { 0x01 },
			TotalShares = 5,
			Threshold = 3,
			CreatedAt = DateTimeOffset.UtcNow.AddHours(-25),
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // Expired
		};

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
			_sut.RecoverKeyAsync("test-key", expiredToken, CancellationToken.None));
	}

	#endregion RecoverKeyAsync Parameter Validation Tests

	#region GenerateRecoveryTokensAsync Parameter Validation Tests

	[Fact]
	public async Task GenerateRecoveryTokensAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GenerateRecoveryTokensAsync(null!, 5, 3, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokensAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GenerateRecoveryTokensAsync(string.Empty, 5, 3, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokensAsync_ThrowArgumentOutOfRangeException_WhenCustodianCountLessThan2()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoveryTokensAsync("test-key", 1, 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokensAsync_ThrowArgumentOutOfRangeException_WhenThresholdLessThan2()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoveryTokensAsync("test-key", 5, 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoveryTokensAsync_ThrowArgumentOutOfRangeException_WhenThresholdExceedsCustodianCount()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoveryTokensAsync("test-key", 3, 5, null, CancellationToken.None));
	}

	#endregion GenerateRecoveryTokensAsync Parameter Validation Tests

	#region RevokeEscrowAsync Parameter Validation Tests

	[Fact]
	public async Task RevokeEscrowAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RevokeEscrowAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task RevokeEscrowAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RevokeEscrowAsync(string.Empty, null, CancellationToken.None));
	}

	#endregion RevokeEscrowAsync Parameter Validation Tests

	#region GetEscrowStatusAsync Parameter Validation Tests

	[Fact]
	public async Task GetEscrowStatusAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GetEscrowStatusAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetEscrowStatusAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Arrange
		_sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GetEscrowStatusAsync(string.Empty, CancellationToken.None));
	}

	#endregion GetEscrowStatusAsync Parameter Validation Tests

	#region Dispose Tests

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange & Act & Assert
		typeof(SqlServerKeyEscrowService).GetInterfaces()
			.ShouldContain(typeof(IDisposable));
	}

	[Fact]
	public void AllowMultipleDisposes()
	{
		// Arrange
		_sut = CreateSut();

		// Act - should not throw
		_sut.Dispose();
		_sut.Dispose();
		_sut.Dispose();
	}

	#endregion Dispose Tests

	#region Helper Methods

	public void Dispose()
	{
		_sut?.Dispose();
	}

	private static IOptions<SqlServerKeyEscrowOptions> CreateOptions(string connectionString)
	{
		var options = new SqlServerKeyEscrowOptions
		{
			ConnectionString = connectionString
		};
		return Microsoft.Extensions.Options.Options.Create(options);
	}

	private static RecoveryToken CreateRecoveryToken()
	{
		return new RecoveryToken
		{
			TokenId = "test-token",
			KeyId = "test-key",
			EscrowId = "escrow-1",
			ShareIndex = 0,
			ShareData = new byte[] { 0x01, 0x02, 0x03 },
			TotalShares = 5,
			Threshold = 3,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
		};
	}

	private SqlServerKeyEscrowService CreateSut()
	{
		var options = CreateOptions("Server=localhost;Database=test;");
		return new SqlServerKeyEscrowService(options, _encryptionProvider, _logger);
	}

	#endregion Helper Methods
}
