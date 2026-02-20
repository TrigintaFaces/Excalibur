// Copyright (c) TrigintaFaces. All rights reserved.

using Excalibur.Security.Abstractions;

using Excalibur.Security;

namespace Excalibur.Dispatch.Security.Tests.Excalibur;

/// <summary>
/// Unit tests for <see cref="Argon2idPasswordHasher"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class Argon2idPasswordHasherShould
{
	private readonly Argon2Options _defaultOptions = new();
	private readonly Argon2idPasswordHasher _sut;

	public Argon2idPasswordHasherShould()
	{
		_sut = new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(_defaultOptions));
	}

	[Fact]
	public async Task HashPasswordAsync_WithValidPassword_ReturnsHashResult()
	{
		// Arrange
		var password = "SecurePassword123!";

		// Act
		var result = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Hash.ShouldNotBeNullOrWhiteSpace();
		result.Salt.ShouldNotBeNullOrWhiteSpace();
		result.Algorithm.ShouldBe("argon2id");
		result.Version.ShouldBe(_defaultOptions.Version);
		result.Parameters.ShouldContainKey("memorySize");
		result.Parameters.ShouldContainKey("iterations");
		result.Parameters.ShouldContainKey("parallelism");
		result.Parameters.ShouldContainKey("hashLength");
	}

	[Fact]
	public async Task HashPasswordAsync_WithSamePassword_ProducesDifferentHashes()
	{
		// Arrange
		var password = "SecurePassword123!";

		// Act
		var result1 = await _sut.HashPasswordAsync(password, CancellationToken.None);
		var result2 = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Assert
		result1.Hash.ShouldNotBe(result2.Hash);
		result1.Salt.ShouldNotBe(result2.Salt);
	}

	[Fact]
	public async Task HashPasswordAsync_WithNullPassword_ThrowsArgumentNullException()
	{
		// Arrange
		string? password = null;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.HashPasswordAsync(password, CancellationToken.None));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public async Task HashPasswordAsync_WithEmptyOrWhitespacePassword_ThrowsArgumentException(string password)
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.HashPasswordAsync(password, CancellationToken.None));
	}

	[Fact]
	public async Task HashPasswordAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		var password = "SecurePassword123!";
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.HashPasswordAsync(password, cts.Token));
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithCorrectPassword_ReturnsSuccess()
	{
		// Arrange
		var password = "SecurePassword123!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Act
		var result = await _sut.VerifyPasswordAsync(password, hash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.Success);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithIncorrectPassword_ReturnsFailed()
	{
		// Arrange
		var password = "SecurePassword123!";
		var wrongPassword = "WrongPassword456!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Act
		var result = await _sut.VerifyPasswordAsync(wrongPassword, hash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.Failed);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithNullPassword_ThrowsArgumentNullException()
	{
		// Arrange
		string? password = null;
		var hash = await _sut.HashPasswordAsync("AnyPassword", CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyPasswordAsync(password, hash, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithNullHash_ThrowsArgumentNullException()
	{
		// Arrange
		var password = "SecurePassword123!";
		PasswordHashResult? hash = null;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyPasswordAsync(password, hash, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithDifferentAlgorithm_ReturnsFailed()
	{
		// Arrange
		var password = "SecurePassword123!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);
		var modifiedHash = hash with { Algorithm = "bcrypt" };

		// Act
		var result = await _sut.VerifyPasswordAsync(password, modifiedHash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.Failed);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WhenParametersChanged_ReturnsSuccessRehashNeeded()
	{
		// Arrange
		var password = "SecurePassword123!";
		var oldOptions = new Argon2Options { MemorySize = 32768, Version = 1 }; // Lower memory
		var newOptions = new Argon2Options { MemorySize = 65536, Version = 2 }; // Higher memory

		var oldHasher = new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(oldOptions));
		var newHasher = new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(newOptions));

		var hash = await oldHasher.HashPasswordAsync(password, CancellationToken.None);

		// Act
		var result = await newHasher.VerifyPasswordAsync(password, hash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.SuccessRehashNeeded);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		var password = "SecurePassword123!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.VerifyPasswordAsync(password, hash, cts.Token));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Argon2idPasswordHasher(null!));
	}

	[Theory]
	[InlineData(4096)] // Below minimum of 8192
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithInvalidMemorySize_ThrowsArgumentException(int memorySize)
	{
		// Arrange
		var options = new Argon2Options { MemorySize = memorySize };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(options)));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithInvalidIterations_ThrowsArgumentException(int iterations)
	{
		// Arrange
		var options = new Argon2Options { Iterations = iterations };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(options)));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithInvalidParallelism_ThrowsArgumentException(int parallelism)
	{
		// Arrange
		var options = new Argon2Options { Parallelism = parallelism };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(options)));
	}

	[Theory]
	[InlineData(8)] // Below minimum of 16
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithInvalidHashLength_ThrowsArgumentException(int hashLength)
	{
		// Arrange
		var options = new Argon2Options { HashLength = hashLength };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(options)));
	}

	[Theory]
	[InlineData(8)] // Below minimum of 16
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithInvalidSaltLength_ThrowsArgumentException(int saltLength)
	{
		// Arrange
		var options = new Argon2Options { SaltLength = saltLength };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(options)));
	}

	[Fact]
	public async Task HashPasswordAsync_ReturnsBase64EncodedValues()
	{
		// Arrange
		var password = "SecurePassword123!";

		// Act
		var result = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Assert - Verify values are valid Base64
		var hashBytes = Convert.FromBase64String(result.Hash);
		var saltBytes = Convert.FromBase64String(result.Salt);

		hashBytes.Length.ShouldBe(_defaultOptions.HashLength);
		saltBytes.Length.ShouldBe(_defaultOptions.SaltLength);
	}

	[Fact]
	public async Task HashPasswordAsync_StoresCorrectParameters()
	{
		// Arrange
		var customOptions = new Argon2Options
		{
			MemorySize = 32768,
			Iterations = 3,
			Parallelism = 2,
			HashLength = 64,
			SaltLength = 32,
			Version = 5,
		};
		var hasher = new Argon2idPasswordHasher(Microsoft.Extensions.Options.Options.Create(customOptions));
		var password = "SecurePassword123!";

		// Act
		var result = await hasher.HashPasswordAsync(password, CancellationToken.None);

		// Assert
		result.Version.ShouldBe(5);
		result.Parameters["memorySize"].ShouldBe(32768);
		result.Parameters["iterations"].ShouldBe(3);
		result.Parameters["parallelism"].ShouldBe(2);
		result.Parameters["hashLength"].ShouldBe(64);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithEmptyPassword_ReturnsFailed()
	{
		// Arrange - The underlying Argon2 library requires non-empty passwords
		// Our implementation should return Failed for empty passwords rather than throw
		var password = "SecurePassword123!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Act - Empty password should fail verification
		var result = await _sut.VerifyPasswordAsync(string.Empty, hash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.Failed);
	}

	[Fact]
	public async Task VerifyPasswordAsync_WithWhitespacePassword_ReturnsFailed()
	{
		// Arrange
		var password = "SecurePassword123!";
		var hash = await _sut.HashPasswordAsync(password, CancellationToken.None);

		// Act - Whitespace-only password should fail verification
		var result = await _sut.VerifyPasswordAsync("   ", hash, CancellationToken.None);

		// Assert
		result.ShouldBe(PasswordVerificationResult.Failed);
	}
}
