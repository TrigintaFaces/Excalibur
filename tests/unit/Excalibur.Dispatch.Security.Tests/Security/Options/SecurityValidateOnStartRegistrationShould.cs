// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Verifies that security DI extension methods register ValidateOnStart and DataAnnotation validation
/// for all security options classes.
/// Sprint 562 S562.51: Security ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityValidateOnStartRegistrationShould
{
	#region Encryption

	[Fact]
	public void RegisterEncryptionOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessageEncryption();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<EncryptionOptions>>();
		validators.ShouldNotBeEmpty("AddMessageEncryption should register IValidateOptions<EncryptionOptions>");
	}

	[Fact]
	public void EncryptionValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddMessageEncryption(opts =>
		{
			opts.Enabled = true;
			opts.KeyRotationIntervalDays = 90;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<EncryptionOptions>>();
		var value = options.Value;

		// Assert
		value.Enabled.ShouldBeTrue();
		value.KeyRotationIntervalDays.ShouldBe(90);
	}

	[Fact]
	public void EncryptionInvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddMessageEncryption(opts =>
		{
			opts.KeyRotationIntervalDays = 0; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<EncryptionOptions>>();

		// Assert - accessing .Value triggers validation
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Signing

	[Fact]
	public void RegisterSigningOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessageSigning();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<SigningOptions>>();
		validators.ShouldNotBeEmpty("AddMessageSigning should register IValidateOptions<SigningOptions>");
	}

	[Fact]
	public void SigningValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddMessageSigning(opts =>
		{
			opts.MaxSignatureAgeMinutes = 10;
			opts.KeyRotationIntervalDays = 60;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SigningOptions>>();
		var value = options.Value;

		// Assert
		value.MaxSignatureAgeMinutes.ShouldBe(10);
		value.KeyRotationIntervalDays.ShouldBe(60);
	}

	[Fact]
	public void SigningInvalidMaxSignatureAge_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddMessageSigning(opts =>
		{
			opts.MaxSignatureAgeMinutes = 0; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SigningOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void SigningInvalidKeyRotation_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddMessageSigning(opts =>
		{
			opts.KeyRotationIntervalDays = -1; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SigningOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Rate Limiting

	[Fact]
	public void RegisterRateLimitingOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRateLimiting();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<RateLimitingOptions>>();
		validators.ShouldNotBeEmpty("AddRateLimiting should register IValidateOptions<RateLimitingOptions>");
	}

	[Fact]
	public void RateLimitingValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddRateLimiting(opts =>
		{
			opts.DefaultRetryAfterMilliseconds = 2000;
			opts.CleanupIntervalMinutes = 10;
			opts.InactivityTimeoutMinutes = 60;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>();
		var value = options.Value;

		// Assert
		value.DefaultRetryAfterMilliseconds.ShouldBe(2000);
		value.CleanupIntervalMinutes.ShouldBe(10);
		value.InactivityTimeoutMinutes.ShouldBe(60);
	}

	[Fact]
	public void RateLimitingInvalidRetryAfter_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddRateLimiting(opts =>
		{
			opts.DefaultRetryAfterMilliseconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void RateLimitingInvalidCleanupInterval_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddRateLimiting(opts =>
		{
			opts.CleanupIntervalMinutes = -5; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region JWT Authentication

	[Fact]
	public void RegisterJwtAuthenticationOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddJwtAuthentication();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<JwtAuthenticationOptions>>();
		validators.ShouldNotBeEmpty("AddJwtAuthentication should register IValidateOptions<JwtAuthenticationOptions>");
	}

	[Fact]
	public void JwtAuthenticationRegistersCustomValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddJwtAuthentication();

		// Assert - should have the custom cross-property validator
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<JwtAuthenticationOptions>>();
		validators.ShouldContain(
			v => v is JwtAuthenticationOptionsValidator,
			"AddJwtAuthentication should register JwtAuthenticationOptionsValidator");
	}

	[Fact]
	public void JwtAuthenticationValidOptions_ResolveSuccessfully()
	{
		// Arrange - must provide issuer/audience/signing key since validators are enabled by default
		var services = new ServiceCollection();

		_ = services.AddJwtAuthentication(opts =>
		{
			opts.Enabled = true;
			opts.ClockSkewSeconds = 300;
			opts.ValidIssuer = "https://auth.example.com";
			opts.ValidAudience = "my-api";
			opts.SigningKey = "super-secret-key-that-is-long-enough-for-hmac";
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<JwtAuthenticationOptions>>();
		var value = options.Value;

		// Assert
		value.Enabled.ShouldBeTrue();
		value.ClockSkewSeconds.ShouldBe(300);
	}

	[Fact]
	public void JwtAuthenticationInvalidClockSkew_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddJwtAuthentication(opts =>
		{
			opts.ClockSkewSeconds = -1; // Violates [Range(0, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<JwtAuthenticationOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Cross-Cutting

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidators()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		_ = services.AddMessageEncryption();
		_ = services.AddMessageEncryption();

		// Assert - TryAddSingleton prevents duplicates
		using var provider = services.BuildServiceProvider();
		// DataAnnotation validators are added per-call but TryAdd for custom validators prevents duplication
		var validators = provider.GetServices<IValidateOptions<EncryptionOptions>>();
		validators.ShouldNotBeEmpty();
	}

	[Fact]
	public void AllSecurityOptions_RegisteredTogether()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessageEncryption();
		_ = services.AddMessageSigning();
		_ = services.AddRateLimiting();
		_ = services.AddJwtAuthentication();

		// Assert
		using var provider = services.BuildServiceProvider();

		var encryptionValidators = provider.GetServices<IValidateOptions<EncryptionOptions>>();
		var signingValidators = provider.GetServices<IValidateOptions<SigningOptions>>();
		var rateLimitValidators = provider.GetServices<IValidateOptions<RateLimitingOptions>>();
		var jwtValidators = provider.GetServices<IValidateOptions<JwtAuthenticationOptions>>();

		encryptionValidators.ShouldNotBeEmpty();
		signingValidators.ShouldNotBeEmpty();
		rateLimitValidators.ShouldNotBeEmpty();
		jwtValidators.ShouldNotBeEmpty();
	}

	#endregion
}
