// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Timing;

/// <summary>
/// Depth tests for <see cref="TimePolicyServiceCollectionExtensions"/>.
/// Covers all overloads: default, with IConfiguration, with Action,
/// without monitoring, adaptive, and replacement methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimePolicyServiceCollectionExtensionsShould
{
	[Fact]
	public void AddTimePolicy_RegistersTimePolicyAndMonitor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddTimePolicy();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimePolicy));
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimeoutMonitor));
	}

	[Fact]
	public void AddTimePolicy_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddTimePolicy();
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddTimePolicy_WithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddTimePolicy((IConfiguration)null!));
	}

	[Fact]
	public void AddTimePolicy_WithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[$"{TimePolicyOptions.SectionName}:EnforceTimeouts"] = "false",
			})
			.Build();

		// Act
		services.AddTimePolicy(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimePolicy));
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimeoutMonitor));
	}

	[Fact]
	public void AddTimePolicy_WithAction_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddTimePolicy(opts =>
		{
			opts.DefaultTimeout = TimeSpan.FromSeconds(10);
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimePolicy));
	}

	[Fact]
	public void AddTimePolicyWithoutMonitoring_DoesNotRegisterMonitor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddTimePolicyWithoutMonitoring();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITimePolicy));
		services.ShouldNotContain(sd => sd.ServiceType == typeof(ITimeoutMonitor));
	}

	[Fact]
	public void AddTimePolicyWithoutMonitoring_AcceptsConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddTimePolicyWithoutMonitoring(opts =>
		{
			opts.EnforceTimeouts = false;
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAdaptiveTimeouts_EnablesAdaptiveAndMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAdaptiveTimeouts();
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimePolicyOptions>>().Value;

		// Assert
		options.UseAdaptiveTimeouts.ShouldBeTrue();
		options.IncludeTimeoutMetrics.ShouldBeTrue();
	}

	[Fact]
	public void AddAdaptiveTimeouts_AllowsCustomization()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAdaptiveTimeouts(opts =>
		{
			opts.AdaptiveTimeoutPercentile = 90;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimePolicyOptions>>().Value;

		// Assert
		options.UseAdaptiveTimeouts.ShouldBeTrue();
		options.AdaptiveTimeoutPercentile.ShouldBe(90);
	}

	[Fact]
	public void ReplaceTimePolicy_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTimePolicy();

		// Act
		services.ReplaceTimePolicy<TestTimePolicy>();

		// Assert
		var timePolicyDescriptors = services
			.Where(sd => sd.ServiceType == typeof(ITimePolicy))
			.ToList();
		timePolicyDescriptors.ShouldContain(sd =>
			sd.ImplementationType == typeof(TestTimePolicy));
	}

	[Fact]
	public void ReplaceTimeoutMonitor_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTimePolicy();

		// Act
		services.ReplaceTimeoutMonitor<TestTimeoutMonitor>();

		// Assert
		var monitorDescriptors = services
			.Where(sd => sd.ServiceType == typeof(ITimeoutMonitor))
			.ToList();
		monitorDescriptors.ShouldContain(sd =>
			sd.ImplementationType == typeof(TestTimeoutMonitor));
	}

	// Test doubles that implement full interfaces
	private sealed class TestTimePolicy : ITimePolicy
	{
		public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(1);
		public TimeSpan MaxTimeout => TimeSpan.FromMinutes(1);
		public TimeSpan HandlerTimeout => TimeSpan.FromSeconds(30);
		public TimeSpan SerializationTimeout => TimeSpan.FromSeconds(5);
		public TimeSpan TransportTimeout => TimeSpan.FromSeconds(30);
		public TimeSpan ValidationTimeout => TimeSpan.FromSeconds(5);

		public TimeSpan GetTimeoutFor(TimeoutOperationType operationType) =>
			TimeSpan.FromSeconds(1);

		public bool ShouldApplyTimeout(TimeoutOperationType operationType, TimeoutContext? context = null) =>
			true;

		public CancellationToken CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken) =>
			parentToken;
	}

	private sealed class TestTimeoutMonitor : ITimeoutMonitor
	{
		public ITimeoutOperationToken StartOperation(TimeoutOperationType operationType, TimeoutContext? context = null) =>
			A.Fake<ITimeoutOperationToken>();

		public void CompleteOperation(ITimeoutOperationToken token, bool success, bool timedOut) { }

		public TimeoutStatistics GetStatistics(TimeoutOperationType operationType) =>
			new();

		public TimeSpan GetRecommendedTimeout(TimeoutOperationType operationType, int percentile = 95, TimeoutContext? context = null) =>
			TimeSpan.FromSeconds(30);

		public void ClearStatistics(TimeoutOperationType? operationType = null) { }

		public int GetSampleCount(TimeoutOperationType operationType) => 0;

		public bool HasSufficientSamples(TimeoutOperationType operationType, int minimumSamples = 100) => false;
	}
}
