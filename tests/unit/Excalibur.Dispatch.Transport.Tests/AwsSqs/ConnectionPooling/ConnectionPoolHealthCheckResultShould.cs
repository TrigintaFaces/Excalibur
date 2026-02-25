// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.ConnectionPooling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConnectionPoolHealthCheckResultShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new ConnectionPoolHealthCheckResult();

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.HealthyConnections.ShouldBe(0);
		result.UnhealthyConnections.ShouldBe(0);
		result.TotalConnections.ShouldBe(0);
		result.ActiveConnections.ShouldBe(0);
		result.Message.ShouldBeNull();
		result.CheckedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var checkedAt = DateTimeOffset.UtcNow;

		// Act
		var result = new ConnectionPoolHealthCheckResult
		{
			IsHealthy = true,
			HealthyConnections = 8,
			UnhealthyConnections = 2,
			TotalConnections = 10,
			ActiveConnections = 6,
			Message = "Pool is operational with 2 unhealthy connections",
			CheckedAt = checkedAt,
		};

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.HealthyConnections.ShouldBe(8);
		result.UnhealthyConnections.ShouldBe(2);
		result.TotalConnections.ShouldBe(10);
		result.ActiveConnections.ShouldBe(6);
		result.Message.ShouldBe("Pool is operational with 2 unhealthy connections");
		result.CheckedAt.ShouldBe(checkedAt);
	}
}
