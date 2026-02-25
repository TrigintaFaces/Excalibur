// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests that the DI registration for <see cref="ITelemetrySanitizer"/> and
/// <see cref="TelemetrySanitizerOptions"/> behaves correctly, including the IncludeRawPii bypass.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class SanitizerDiRegistrationShould
{
	/// <summary>
	/// Registers the sanitizer services in isolation (same registrations as AddDispatchObservability
	/// but without the IPostConfigureOptions wiring that is being developed in parallel).
	/// </summary>
	private static IServiceCollection AddSanitizerServices(IServiceCollection services)
	{
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ITelemetrySanitizer, HashingTelemetrySanitizer>();
		return services;
	}

	[Fact]
	public void RegisterHashingTelemetrySanitizerAsDefault()
	{
		// Arrange
		var services = new ServiceCollection();
		AddSanitizerServices(services);

		using var provider = services.BuildServiceProvider();

		// Act
		var sanitizer = provider.GetService<ITelemetrySanitizer>();

		// Assert
		sanitizer.ShouldNotBeNull();
		sanitizer.ShouldBeOfType<HashingTelemetrySanitizer>();
	}

	[Fact]
	public void RegisterTelemetrySanitizerOptionsWithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		AddSanitizerServices(services);

		using var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetService<IOptions<TelemetrySanitizerOptions>>();

		// Assert
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void RespectIncludeRawPiiOptionViaDi()
	{
		// Arrange
		var services = new ServiceCollection();
		AddSanitizerServices(services);
		services.Configure<TelemetrySanitizerOptions>(o => o.IncludeRawPii = true);

		using var provider = services.BuildServiceProvider();
		var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

		// Act — with IncludeRawPii=true, sensitive tags should pass through
		var result = sanitizer.SanitizeTag("user.id", "alice@example.com");

		// Assert
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void HashSensitiveTagsByDefaultViaDi()
	{
		// Arrange
		var services = new ServiceCollection();
		AddSanitizerServices(services);

		using var provider = services.BuildServiceProvider();
		var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

		// Act — default IncludeRawPii=false, so sensitive tags should be hashed
		var result = sanitizer.SanitizeTag("user.id", "alice@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldNotBe("alice@example.com");
	}

	[Fact]
	public void RegisterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		AddSanitizerServices(services);

		using var provider = services.BuildServiceProvider();

		// Act
		var first = provider.GetRequiredService<ITelemetrySanitizer>();
		var second = provider.GetRequiredService<ITelemetrySanitizer>();

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void NotReplaceCustomSanitizerRegistration()
	{
		// Arrange — register custom sanitizer before the default
		var services = new ServiceCollection();
		services.AddSingleton<ITelemetrySanitizer>(NullTelemetrySanitizer.Instance);
		AddSanitizerServices(services);

		using var provider = services.BuildServiceProvider();

		// Act
		var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

		// Assert — TryAddSingleton should not replace existing registration
		sanitizer.ShouldBeSameAs(NullTelemetrySanitizer.Instance);
	}
}
