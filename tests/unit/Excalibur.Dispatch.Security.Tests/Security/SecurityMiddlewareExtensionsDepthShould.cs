// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// Depth tests for <see cref="SecurityMiddlewareExtensions"/>.
/// Covers configuration-based overloads (AddDispatchSecurity with IConfiguration),
/// options-action overloads (AddDispatchSecurity with Action&lt;SecurityOptions&gt;),
/// individual service registration with IConfiguration, null argument guards,
/// and ValidateOnStart registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "DI")]
public sealed class SecurityMiddlewareExtensionsDepthShould
{
	[Fact]
	public void AddDispatchSecurityWithConfiguration_RegistersAllComponents()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IKeyProvider>());
		services.AddSingleton(A.Fake<ITelemetrySanitizer>());
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Encryption:Enabled"] = "true",
				["Security:Signing:Enabled"] = "true",
				["Security:RateLimiting:Enabled"] = "true",
				["Security:Authentication:Enabled"] = "true",
			})
			.Build();

		// Act
		services.AddDispatchSecurity(config);

		// Assert — encryption, signing, rate limiting, JWT authentication should all be registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageEncryptionService));
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageSigningService));
		services.ShouldContain(sd => sd.ServiceType == typeof(RateLimitingMiddleware));
	}

	[Fact]
	public void AddDispatchSecurityWithConfiguration_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddDispatchSecurity(null!, config));
	}

	[Fact]
	public void AddDispatchSecurityWithConfiguration_ThrowsWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchSecurity((IConfiguration)null!));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddDispatchSecurity(null!, _ => { }));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_ThrowsWhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchSecurity((Action<SecurityOptions>)null!));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_RegistersEncryptionWhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDispatchSecurity(opts =>
		{
			opts.EnableEncryption = true;
			opts.EnableSigning = false;
			opts.EnableRateLimiting = false;
			opts.EnableAuthentication = false;
		});

		// Assert — only encryption registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageEncryptionService));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_RegistersSigningWhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IKeyProvider>());

		// Act
		services.AddDispatchSecurity(opts =>
		{
			opts.EnableEncryption = false;
			opts.EnableSigning = true;
			opts.EnableRateLimiting = false;
			opts.EnableAuthentication = false;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageSigningService));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_RegistersRateLimitingWhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDispatchSecurity(opts =>
		{
			opts.EnableEncryption = false;
			opts.EnableSigning = false;
			opts.EnableRateLimiting = true;
			opts.EnableAuthentication = false;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RateLimitingMiddleware));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_RegistersAuthenticationWhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ITelemetrySanitizer>());

		// Act
		services.AddDispatchSecurity(opts =>
		{
			opts.EnableEncryption = false;
			opts.EnableSigning = false;
			opts.EnableRateLimiting = false;
			opts.EnableAuthentication = true;
			opts.RequireAuthentication = true;
			opts.JwtIssuer = "test-issuer";
			opts.JwtAudience = "test-audience";
			opts.JwtSigningKey = "ThisIsAVeryLongSigningKeyForHmacSha256ThatExceedsMinimumLength!";
		});

		// Assert — JWT middleware registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchMiddleware) &&
			sd.ImplementationType == typeof(JwtAuthenticationMiddleware));
	}

	[Fact]
	public void AddDispatchSecurityWithAction_SkipsComponentsWhenDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act — all features disabled
		services.AddDispatchSecurity(opts =>
		{
			opts.EnableEncryption = false;
			opts.EnableSigning = false;
			opts.EnableRateLimiting = false;
			opts.EnableAuthentication = false;
		});

		// Assert — none of the middleware services registered
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IMessageEncryptionService));
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IMessageSigningService));
		services.ShouldNotContain(sd => sd.ServiceType == typeof(RateLimitingMiddleware));
		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IDispatchMiddleware) &&
			sd.ImplementationType == typeof(JwtAuthenticationMiddleware));
	}

	[Fact]
	public void AddMessageEncryptionWithConfiguration_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddMessageEncryption(null!, config));
	}

	[Fact]
	public void AddMessageEncryptionWithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessageEncryption((IConfiguration)null!));
	}

	[Fact]
	public void AddMessageEncryptionWithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
			})
			.Build();

		// Act
		services.AddMessageEncryption(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageEncryptionService));
	}

	[Fact]
	public void AddMessageSigningWithConfiguration_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddMessageSigning(null!, config));
	}

	[Fact]
	public void AddMessageSigningWithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessageSigning((IConfiguration)null!));
	}

	[Fact]
	public void AddMessageSigningWithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
			})
			.Build();

		// Act
		services.AddMessageSigning(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageSigningService));
	}

	[Fact]
	public void AddRateLimitingWithConfiguration_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddRateLimiting(null!, config));
	}

	[Fact]
	public void AddRateLimitingWithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddRateLimiting((IConfiguration)null!));
	}

	[Fact]
	public void AddRateLimitingWithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
			})
			.Build();

		// Act
		services.AddRateLimiting(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RateLimitingMiddleware));
	}

	[Fact]
	public void AddJwtAuthenticationWithConfiguration_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
		Should.Throw<ArgumentNullException>(() =>
			SecurityMiddlewareExtensions.AddJwtAuthentication(null!, config));
	}

	[Fact]
	public void AddJwtAuthenticationWithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddJwtAuthentication((IConfiguration)null!));
	}

	[Fact]
	public void AddJwtAuthenticationWithConfiguration_RegistersMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ITelemetrySanitizer>());
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
			})
			.Build();

		// Act
		services.AddJwtAuthentication(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchMiddleware) &&
			sd.ImplementationType == typeof(JwtAuthenticationMiddleware));
	}

	[Fact]
	public void AddMessageEncryptionWithNullDelegate_StillRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act — null configureOptions should be allowed (optional)
		services.AddMessageEncryption(configureOptions: null);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageEncryptionService));
	}

	[Fact]
	public void AddMessageSigningWithNullDelegate_StillRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddMessageSigning(configureOptions: null);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageSigningService));
	}

	[Fact]
	public void AddRateLimitingWithNullDelegate_StillRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddRateLimiting(configureOptions: null);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RateLimitingMiddleware));
	}

	[Fact]
	public void AddJwtAuthenticationWithNullDelegate_StillRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ITelemetrySanitizer>());

		// Act
		services.AddJwtAuthentication(configureOptions: null);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchMiddleware) &&
			sd.ImplementationType == typeof(JwtAuthenticationMiddleware));
	}

	[Fact]
	public void AddJwtAuthenticationRegistersJwtAuthenticationOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ITelemetrySanitizer>());

		// Act
		services.AddJwtAuthentication();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<JwtAuthenticationOptions>));
	}

	[Fact]
	public void AddRateLimitingResolvesSingletonMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddRateLimiting(opt => opt.Enabled = true);

		// Act
		var provider = services.BuildServiceProvider();
		var middleware1 = provider.GetRequiredService<RateLimitingMiddleware>();
		var middleware2 = provider.GetRequiredService<RateLimitingMiddleware>();

		// Assert — singleton registration means same instance
		middleware1.ShouldBeSameAs(middleware2);
	}
}

#pragma warning restore IL2026, IL3050
