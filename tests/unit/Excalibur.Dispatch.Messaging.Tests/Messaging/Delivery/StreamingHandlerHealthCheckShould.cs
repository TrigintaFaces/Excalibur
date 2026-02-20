// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="StreamingHandlerHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HealthCheck")]
public sealed class StreamingHandlerHealthCheckShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new StreamingHandlerHealthCheck(null!));
	}

	[Fact]
	public async Task ReturnHealthyOrDegraded_WithValidServiceProvider()
	{
		// Arrange — Build a minimal service provider
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		var healthCheck = new StreamingHandlerHealthCheck(sp);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("streaming", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert — In test environment, no streaming handlers are registered
		// so result should be Healthy (if discovered) or Degraded (if none found)
		result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
		result.Data.ShouldContainKey("registered_handler_count");
	}

	[Fact]
	public async Task IncludeHandlerCountInData()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		var healthCheck = new StreamingHandlerHealthCheck(sp);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("streaming", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Data["registered_handler_count"].ShouldBeOfType<int>();
	}

	[Fact]
	public async Task ReturnCorrectStatus_BasedOnDiscoveredHandlerCount()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		var healthCheck = new StreamingHandlerHealthCheck(sp);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("streaming", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert — Status depends on whether assembly scanning finds handler types
		var handlerCount = (int)result.Data["registered_handler_count"];
		if (handlerCount > 0)
		{
			result.Status.ShouldBe(HealthStatus.Healthy);
			result.Description.ShouldContain("handler type(s) discovered");
		}
		else
		{
			result.Status.ShouldBe(HealthStatus.Degraded);
			result.Description.ShouldContain("No streaming document handler types discovered");
		}
	}
}
