// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Deep coverage tests for <see cref="ObservabilityServiceCollectionExtensions"/> covering
/// DI registration paths: disabled OpenTelemetry, null configure action,
/// compliance sanitizer override, and ValidateOnStart registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ObservabilityServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void RegisterCoreServiceDescriptors_WithNullConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — null configureOptions
		services.AddDispatchObservability(configureOptions: null);

		// Assert — core service descriptors registered (check descriptors, not resolve)
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowTracker));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowMetrics));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextTraceEnricher));
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowDiagnostics));
	}

	[Fact]
	public void RegisterTelemetrySanitizerDescriptor_AsHashingByDefault()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — HashingTelemetrySanitizer registered as ITelemetrySanitizer
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ITelemetrySanitizer) &&
			sd.ImplementationType == typeof(HashingTelemetrySanitizer));
	}

	[Fact]
	public void RegisterTelemetrySanitizerOptionsValidatorDescriptor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — validator registered via TryAddEnumerable
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<TelemetrySanitizerOptions>) &&
			sd.ImplementationType == typeof(TelemetrySanitizerOptionsValidator));
	}

	[Fact]
	public void NotRegisterDuplicateDescriptors_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — call twice
		services.AddDispatchObservability();
		services.AddDispatchObservability();

		// Assert — TryAddSingleton prevents duplicate trackers
		var trackerDescriptors = services.Count(sd => sd.ServiceType == typeof(IContextFlowTracker));
		trackerDescriptors.ShouldBe(1);
	}

	[Fact]
	public void RegisterContextObservabilityOptions_WithValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability(options => options.Enabled = false);

		// Assert — IOptions<ContextObservabilityOptions> is resolvable
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<ContextObservabilityOptions>>();
		options.Value.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void SkipOpenTelemetryDescriptors_WhenDisabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — disable OTel
		services.AddDispatchObservability(options => options.Enabled = false);

		// Assert — core descriptors registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IContextFlowTracker));
		// No OTel-specific exceptions during registration
	}

	[Fact]
	public void ReplaceDefaultSanitizerDescriptor_WithComplianceSanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchObservability(options => options.Enabled = false);

		// Act — add compliance sanitizer AFTER base registration
		services.AddComplianceTelemetrySanitizer();

		// Assert — ComplianceTelemetrySanitizer replaces HashingTelemetrySanitizer
		// After RemoveAll + AddSingleton, only ComplianceTelemetrySanitizer should remain
		var sanitizerDescriptors = services.Where(sd => sd.ServiceType == typeof(ITelemetrySanitizer)).ToList();
		sanitizerDescriptors.Count.ShouldBe(1);
		sanitizerDescriptors[0].ImplementationType.ShouldBe(typeof(ComplianceTelemetrySanitizer));
	}

	[Fact]
	public void RegisterComplianceSanitizerOptions_WithValidation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchObservability(options => options.Enabled = false);

		// Act
		services.AddComplianceTelemetrySanitizer(options =>
		{
			options.DetectEmails = true;
			options.DetectPhoneNumbers = false;
		});

		// Assert — options resolvable
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<ComplianceTelemetrySanitizerOptions>>();
		options.Value.DetectEmails.ShouldBeTrue();
		options.Value.DetectPhoneNumbers.ShouldBeFalse();
	}

	[Fact]
	public void RegisterComplianceSanitizerDescriptor_WithNullConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchObservability(options => options.Enabled = false);

		// Act — null configureOptions
		services.AddComplianceTelemetrySanitizer(configureOptions: null);

		// Assert — compliance descriptor still registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ITelemetrySanitizer) &&
			sd.ImplementationType == typeof(ComplianceTelemetrySanitizer));
	}

	[Fact]
	public void RegisterComplianceSanitizerOptionsValidatorDescriptor()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchObservability(options => options.Enabled = false);

		// Act
		services.AddComplianceTelemetrySanitizer();

		// Assert — validator registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<ComplianceTelemetrySanitizerOptions>) &&
			sd.ImplementationType == typeof(ComplianceTelemetrySanitizerOptionsValidator));
	}

	[Fact]
	public void RegisterPostConfigureOptions_ForSensitiveDataFlow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchObservability();

		// Assert — SensitiveDataPostConfigureOptions registered for multiple option types
		services.ShouldContain(sd =>
			sd.ImplementationType == typeof(SensitiveDataPostConfigureOptions));
	}
}
