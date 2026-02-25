// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="ObservabilityServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ObservabilityServiceCollectionExtensionsShould : UnitTestBase
{
	private static void AddRequiredHosting(IServiceCollection services)
	{
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Development");
		services.AddSingleton(hostEnv);
	}

	#region AddDispatchObservability Tests

	[Fact]
	public async Task AddDispatchObservability_RegistersTelemetrySanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		AddRequiredHosting(services);

		// Act
		services.AddDispatchObservability(opts => opts.Enabled = false);

		// Assert
		await using var provider = services.BuildServiceProvider();
		var sanitizer = provider.GetService<ITelemetrySanitizer>();
		sanitizer.ShouldNotBeNull();
		sanitizer.ShouldBeOfType<HashingTelemetrySanitizer>();
	}

	[Fact]
	public async Task AddDispatchObservability_RegistersTelemetrySanitizerOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		AddRequiredHosting(services);

		// Act
		services.AddDispatchObservability(opts => opts.Enabled = false);

		// Assert
		await using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<TelemetrySanitizerOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchObservability_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddDispatchObservability(opts => opts.Enabled = false);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddComplianceTelemetrySanitizer Tests

	[Fact]
	public async Task AddComplianceTelemetrySanitizer_ReplacesDefaultSanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		AddRequiredHosting(services);
		services.AddDispatchObservability(opts => opts.Enabled = false);

		// Act
		services.AddComplianceTelemetrySanitizer();

		// Assert
		await using var provider = services.BuildServiceProvider();
		var sanitizer = provider.GetService<ITelemetrySanitizer>();
		sanitizer.ShouldNotBeNull();
		sanitizer.ShouldBeOfType<ComplianceTelemetrySanitizer>();
	}

	[Fact]
	public async Task AddComplianceTelemetrySanitizer_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		AddRequiredHosting(services);
		services.AddDispatchObservability(opts => opts.Enabled = false);

		// Act
		services.AddComplianceTelemetrySanitizer(opts =>
		{
			opts.RedactedPlaceholder = "***";
			opts.DetectEmails = false;
		});

		// Assert
		await using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ComplianceTelemetrySanitizerOptions>>();
		options.Value.RedactedPlaceholder.ShouldBe("***");
		options.Value.DetectEmails.ShouldBeFalse();
	}

	[Fact]
	public void AddComplianceTelemetrySanitizer_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddComplianceTelemetrySanitizer();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public async Task AddComplianceTelemetrySanitizer_RegistersOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddComplianceTelemetrySanitizer();

		// Assert
		await using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<ComplianceTelemetrySanitizerOptions>>();
		validators.ShouldNotBeEmpty();
	}

	#endregion

	#region AddContextObservability Tests

	[Fact]
	public void AddContextObservability_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		Excalibur.Dispatch.Abstractions.Configuration.IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.AddContextObservability());
	}

	#endregion
}
