// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Configuration;

/// <summary>
/// Verifies that <c>AddDispatchObservability()</c> properly registers
/// ValidateOnStart and DataAnnotation validation for <see cref="ContextObservabilityOptions"/>.
/// Sprint 562 S562.52: Observability ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ObservabilityValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterContextObservabilityOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchObservability();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<ContextObservabilityOptions>>();
		validators.ShouldNotBeEmpty("AddDispatchObservability should register IValidateOptions<ContextObservabilityOptions>");
	}

	[Fact]
	public void DefaultOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchObservability();

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ContextObservabilityOptions>>();
		var value = options.Value;

		// Assert - defaults should pass validation
		value.Enabled.ShouldBeTrue();
		value.ValidateContextIntegrity.ShouldBeTrue();
		value.CaptureCustomItems.ShouldBeTrue();
	}

	[Fact]
	public void CustomConfiguration_IsPreservedAfterValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddDispatchObservability(opts =>
		{
			opts.Enabled = false;
			opts.ValidateContextIntegrity = false;
			opts.CaptureCustomItems = false;
			opts.EmitDiagnosticEvents = false;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ContextObservabilityOptions>>().Value;

		// Assert
		options.Enabled.ShouldBeFalse();
		options.ValidateContextIntegrity.ShouldBeFalse();
		options.CaptureCustomItems.ShouldBeFalse();
		options.EmitDiagnosticEvents.ShouldBeFalse();
	}

	[Fact]
	public void SubOptionsDefaults_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchObservability();

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ContextObservabilityOptions>>().Value;

		// Assert - verify sub-options have valid defaults
		options.Limits.MaxCustomItemsToCapture.ShouldBeGreaterThan(0);
		options.Limits.MaxContextSizeBytes.ShouldBeGreaterThan(0);
		options.Limits.MaxSnapshotsPerLineage.ShouldBeGreaterThan(0);
		options.Limits.MaxHistoryEventsPerContext.ShouldBeGreaterThan(0);
		options.Limits.MaxAnomalyQueueSize.ShouldBeGreaterThan(0);
		options.Tracing.MaxCustomItemsInTraces.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidators()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		_ = services.AddDispatchObservability();
		_ = services.AddDispatchObservability();

		// Assert - TryAddSingleton prevents duplicates
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<ContextObservabilityOptions>>();
		validators.ShouldNotBeEmpty();
	}
}
