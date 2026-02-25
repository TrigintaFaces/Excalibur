using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Security.Tests;

/// <summary>
/// Unit tests for SecurityOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SecurityOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new SecurityOptions();

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.EncryptionAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		options.EnableSigning.ShouldBeTrue();
		options.SigningAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
		options.EnableRateLimiting.ShouldBeTrue();
		options.RateLimitAlgorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
		options.EnableAuthentication.ShouldBeTrue();
		options.RequireAuthentication.ShouldBeTrue();
		options.EnableSecurityHeaders.ShouldBeTrue();
	}

	[Fact]
	public void EnableEncryption_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableEncryption = false;

		// Assert
		options.EnableEncryption.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionAlgorithm_CanBeChanged()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EncryptionAlgorithm = EncryptionAlgorithm.Aes256CbcHmac;

		// Assert
		options.EncryptionAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void AzureKeyVaultUrl_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();
		var vaultUrl = new Uri("https://my-vault.vault.azure.net/");

		// Act
		options.AzureKeyVaultUrl = vaultUrl;

		// Assert
		options.AzureKeyVaultUrl.ShouldBe(vaultUrl);
	}

	[Fact]
	public void AwsKmsKeyArn_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();
		var keyArn = "arn:aws:kms:us-east-1:123456789:key/12345-abcd";

		// Act
		options.AwsKmsKeyArn = keyArn;

		// Assert
		options.AwsKmsKeyArn.ShouldBe(keyArn);
	}

	[Fact]
	public void EnableSigning_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableSigning = false;

		// Assert
		options.EnableSigning.ShouldBeFalse();
	}

	[Fact]
	public void SigningAlgorithm_CanBeChanged()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.SigningAlgorithm = SigningAlgorithm.HMACSHA512;

		// Assert
		options.SigningAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
	}

	[Fact]
	public void EnableRateLimiting_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableRateLimiting = false;

		// Assert
		options.EnableRateLimiting.ShouldBeFalse();
	}

	[Fact]
	public void RateLimitAlgorithm_CanBeChangedToSlidingWindow()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.RateLimitAlgorithm = RateLimitAlgorithm.SlidingWindow;

		// Assert
		options.RateLimitAlgorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
	}

	[Fact]
	public void DefaultRateLimits_IsInitialized()
	{
		// Arrange & Act
		var options = new SecurityOptions();

		// Assert
		_ = options.DefaultRateLimits.ShouldNotBeNull();
	}

	[Fact]
	public void JwtIssuer_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.JwtIssuer = "https://auth.example.com";

		// Assert
		options.JwtIssuer.ShouldBe("https://auth.example.com");
	}

	[Fact]
	public void JwtAudience_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.JwtAudience = "my-api";

		// Assert
		options.JwtAudience.ShouldBe("my-api");
	}

	[Fact]
	public void JwtSigningKey_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.JwtSigningKey = "super-secret-key-12345";

		// Assert
		options.JwtSigningKey.ShouldBe("super-secret-key-12345");
	}

	[Fact]
	public void CustomHeaders_CanAddAndRetrieveHeaders()
	{
		// Arrange
		var options = new SecurityOptions
		{
			CustomHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["X-Custom-Header"] = "CustomValue"
			}
		};

		// Assert
		options.CustomHeaders.ShouldContainKey("X-Custom-Header");
		options.CustomHeaders["X-Custom-Header"].ShouldBe("CustomValue");
	}

	[Fact]
	public void RequireAuthentication_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.RequireAuthentication = false;

		// Assert
		options.RequireAuthentication.ShouldBeFalse();
	}
}
