// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class HealthCheckResultShould
{
	[Fact]
	public void Healthy_CreateHealthyResult()
	{
		var result = HealthCheckResult.Healthy();

		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("Healthy");
	}

	[Fact]
	public void Healthy_CreateWithCustomDescription()
	{
		var result = HealthCheckResult.Healthy("All good");

		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("All good");
	}

	[Fact]
	public void Healthy_CreateWithData()
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "value" };
		var result = HealthCheckResult.Healthy("All good", data);

		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("All good");
		result.Data.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void Unhealthy_CreateUnhealthyResult()
	{
		var result = HealthCheckResult.Unhealthy("Connection failed");

		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("Connection failed");
	}

	[Fact]
	public void Degraded_CreateDegradedResult()
	{
		var result = HealthCheckResult.Degraded("Running slow");

		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldBe("Running slow");
	}

	[Fact]
	public void Constructor_CreateWithIsHealthyTrue()
	{
		var result = new HealthCheckResult(isHealthy: true, description: "manual");

		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("manual");
	}

	[Fact]
	public void Constructor_CreateWithIsHealthyFalse()
	{
		var result = new HealthCheckResult(isHealthy: false, description: "broken");

		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("broken");
	}

	[Fact]
	public void Constructor_DefaultDataIsEmpty()
	{
		var result = new HealthCheckResult(isHealthy: true);

		result.Data.ShouldNotBeNull();
		result.Data.ShouldBeEmpty();
	}
}
