// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Unit tests for <see cref="ObservabilityServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "DependencyInjection")]
public sealed class ObservabilityServiceCollectionExtensionsShould
{
	[Fact]
	public void AddDispatchObservability_RegistersCoreServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — core context observability services
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowTracker));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowMetrics));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextTraceEnricher));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowDiagnostics));
	}

	[Fact]
	public void AddDispatchObservability_RegistersTelemetrySanitizer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — ITelemetrySanitizer registered
		services.ShouldContain(sd => sd.ServiceType == typeof(ITelemetrySanitizer));
	}

	[Fact]
	public void AddDispatchObservability_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability(opts => opts.Enabled = true);

		// Assert — ContextObservabilityOptions are configured
		var optionsDescriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IConfigureOptions<ContextObservabilityOptions>));
		optionsDescriptor.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchObservability_AcceptsNullConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — should not throw with null configure
		services.AddDispatchObservability(configureOptions: null);

		// Assert — services still registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowTracker));
	}

	[Fact]
	public void AddContextObservability_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddContextObservability());
	}

	[Fact]
	public void AddContextObservability_RegistersMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddContextObservability();

		// Assert — ContextObservabilityMiddleware registered
		services.ShouldContain(sd => sd.ServiceType == typeof(ContextObservabilityMiddleware));
	}

	[Fact]
	public void AddComplianceTelemetrySanitizer_RegistersComplianceSanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		// First add base observability
		services.AddDispatchObservability();

		// Act
		services.AddComplianceTelemetrySanitizer();

		// Assert — ComplianceTelemetrySanitizer replaces the default ITelemetrySanitizer
		var sanitizerDescriptor = services
			.Where(sd => sd.ServiceType == typeof(ITelemetrySanitizer))
			.Last(); // Last registration wins
		sanitizerDescriptor.ImplementationType.ShouldBe(typeof(ComplianceTelemetrySanitizer));
	}

	[Fact]
	public void AddComplianceTelemetrySanitizer_RegistersValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddComplianceTelemetrySanitizer();

		// Assert — ComplianceTelemetrySanitizerOptionsValidator registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<ComplianceTelemetrySanitizerOptions>));
	}

	[Fact]
	public void AddComplianceTelemetrySanitizer_AcceptsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddComplianceTelemetrySanitizer(opts =>
		{
			opts.DetectEmails = true;
			opts.DetectPhoneNumbers = false;
		});

		// Assert
		var configureDescriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IConfigureOptions<ComplianceTelemetrySanitizerOptions>));
		configureDescriptor.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchObservability_RegistersValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — ValidateOnStart triggers options infrastructure registration
		// Check that options infrastructure was registered
		var hasOptionsInfrastructure = services.Any(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IOptions<>).GetGenericTypeDefinition());
		hasOptionsInfrastructure.ShouldBeTrue();
	}
}
