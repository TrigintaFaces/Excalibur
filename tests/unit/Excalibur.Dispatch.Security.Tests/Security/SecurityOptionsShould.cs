using Excalibur.Compliance;


namespace Excalibur.Dispatch.Security.Tests;

/// <summary>
/// Unit tests for SecurityOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class SecurityOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new SecurityOptions();

		// Assert
		// Every component is off until the host names it. These flags decide what the configuration
		// delegate composes, so a default of true made naming any one component compose the others too:
		// asking for encryption alone produced JWT authentication with no credentials, and the dispatch
		// pipeline could not then be resolved at all.
		options.Encryption.EnableEncryption.ShouldBeFalse();
		options.Encryption.EncryptionAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		options.Signing.EnableSigning.ShouldBeFalse();
		options.Signing.SigningAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
		options.RateLimiting.EnableRateLimiting.ShouldBeFalse();
		options.RateLimiting.RateLimitAlgorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
		options.Authentication.EnableAuthentication.ShouldBeFalse();

		// Not an Enable flag: this decides how authentication behaves once composed, not whether it is
		// composed, and authentication that is not enforced buys the host nothing.
		options.Authentication.RequireAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void EnableEncryption_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Encryption.EnableEncryption = false;

		// Assert
		options.Encryption.EnableEncryption.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionAlgorithm_CanBeChanged()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Encryption.EncryptionAlgorithm = EncryptionAlgorithm.Aes256CbcHmac;

		// Assert
		options.Encryption.EncryptionAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void AzureKeyVaultUrl_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();
		var vaultUrl = new Uri("https://my-vault.vault.azure.net/");

		// Act
		options.Encryption.AzureKeyVaultUrl = vaultUrl;

		// Assert
		options.Encryption.AzureKeyVaultUrl.ShouldBe(vaultUrl);
	}

	[Fact]
	public void AwsKmsKeyArn_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();
		var keyArn = "arn:aws:kms:us-east-1:123456789:key/12345-abcd";

		// Act
		options.Encryption.AwsKmsKeyArn = keyArn;

		// Assert
		options.Encryption.AwsKmsKeyArn.ShouldBe(keyArn);
	}

	[Fact]
	public void EnableSigning_CanBeEnabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act -- signing is off by default, so opting in is the direction worth asserting.
		options.Signing.EnableSigning = true;

		// Assert
		options.Signing.EnableSigning.ShouldBeTrue();
	}

	[Fact]
	public void SigningAlgorithm_CanBeChanged()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Signing.SigningAlgorithm = SigningAlgorithm.HMACSHA512;

		// Assert
		options.Signing.SigningAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
	}

	[Fact]
	public void EnableRateLimiting_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.RateLimiting.EnableRateLimiting = false;

		// Assert
		options.RateLimiting.EnableRateLimiting.ShouldBeFalse();
	}

	[Fact]
	public void RateLimitAlgorithm_CanBeChangedToSlidingWindow()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.RateLimiting.RateLimitAlgorithm = RateLimitAlgorithm.SlidingWindow;

		// Assert
		options.RateLimiting.RateLimitAlgorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
	}

	[Fact]
	public void DefaultRateLimits_IsInitialized()
	{
		// Arrange & Act
		var options = new SecurityOptions();

		// Assert
		_ = options.RateLimiting.DefaultRateLimits.ShouldNotBeNull();
	}

	[Fact]
	public void JwtIssuer_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Authentication.JwtIssuer = "https://auth.example.com";

		// Assert
		options.Authentication.JwtIssuer.ShouldBe("https://auth.example.com");
	}

	[Fact]
	public void JwtAudience_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Authentication.JwtAudience = "my-api";

		// Assert
		options.Authentication.JwtAudience.ShouldBe("my-api");
	}

	[Fact]
	public void JwtSigningKey_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Authentication.JwtSigningKey = "super-secret-key-12345";

		// Assert
		options.Authentication.JwtSigningKey.ShouldBe("super-secret-key-12345");
	}

	[Fact]
	public void RequireAuthentication_CanBeDisabled()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.Authentication.RequireAuthentication = false;

		// Assert
		options.Authentication.RequireAuthentication.ShouldBeFalse();
	}
}
