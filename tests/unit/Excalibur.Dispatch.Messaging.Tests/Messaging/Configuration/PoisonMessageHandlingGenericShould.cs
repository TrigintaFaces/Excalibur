// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Tests for <c>AddPoisonMessageHandling&lt;TDetector&gt;()</c> convenience overload (Sprint 656 Q.5 / N.8).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PoisonMessageHandlingGenericShould
{
	[Fact]
	public void RegisterBaseServicesAndCustomDetector()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPoisonMessageHandling<CustomPoisonDetector>();

		// Assert -- base handler registered
		services.ShouldContain(d =>
			d.ServiceType == typeof(IPoisonMessageHandler));

		// Assert -- custom detector registered as enumerable service
		services.ShouldContain(d =>
			d.ServiceType == typeof(IPoisonMessageDetector) &&
			d.ImplementationType == typeof(CustomPoisonDetector));
	}

	[Fact]
	public void RegisterBaseServicesWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPoisonMessageHandling<CustomPoisonDetector>(
			opts => opts.MaxRetryAttempts = 7);

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();
		options.Value.MaxRetryAttempts.ShouldBe(7);
	}

	[Fact]
	public void RegisterBaseServicesWithNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- null configureOptions should use defaults
		_ = services.AddPoisonMessageHandling<CustomPoisonDetector>(configureOptions: null);

		// Assert -- defaults resolve
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();
		options.Value.MaxRetryAttempts.ShouldBe(3); // default
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddPoisonMessageHandling<CustomPoisonDetector>());
	}

	[Fact]
	public void ReturnServiceCollection_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddPoisonMessageHandling<CustomPoisonDetector>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterDefaultDetectors_InAdditionToCustom()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPoisonMessageHandling<CustomPoisonDetector>();

		// Assert -- default detectors from base AddPoisonMessageHandling
		services.ShouldContain(d =>
			d.ServiceType == typeof(IPoisonMessageDetector) &&
			d.ImplementationType == typeof(RetryCountPoisonDetector));

		services.ShouldContain(d =>
			d.ServiceType == typeof(IPoisonMessageDetector) &&
			d.ImplementationType == typeof(ExceptionTypePoisonDetector));

		services.ShouldContain(d =>
			d.ServiceType == typeof(IPoisonMessageDetector) &&
			d.ImplementationType == typeof(TimespanPoisonDetector));
	}

	/// <summary>
	/// Test custom poison message detector.
	/// </summary>
	private sealed class CustomPoisonDetector : IPoisonMessageDetector
	{
		public Task<PoisonDetectionResult> IsPoisonMessageAsync(
			IDispatchMessage message,
			IMessageContext context,
			MessageProcessingInfo processingInfo,
			Exception? exception = null)
			=> Task.FromResult(PoisonDetectionResult.NotPoison());
	}
}
