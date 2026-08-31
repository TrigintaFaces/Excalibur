// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.ConnectionPooling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class ConnectionModelsShould
{
	[Fact]
	public void ConnectionHealthEnumHaveCorrectValues()
	{
		// Assert
		((int)ConnectionHealth.Healthy).ShouldBe(0);
		((int)ConnectionHealth.Unhealthy).ShouldBe(1);
		((int)ConnectionHealth.Unknown).ShouldBe(2);
	}

	[Fact]
	public void ConnectionPoolHealthCheckResultHaveCorrectDefaults()
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
		result.CheckedAt.ShouldNotBe(default);
	}

	[Fact]
	public void ConnectionPoolHealthCheckResultAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var result = new ConnectionPoolHealthCheckResult
		{
			IsHealthy = true,
			HealthyConnections = 8,
			UnhealthyConnections = 2,
			TotalConnections = 10,
			ActiveConnections = 5,
			Message = "Pool healthy",
			CheckedAt = now,
		};

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.HealthyConnections.ShouldBe(8);
		result.UnhealthyConnections.ShouldBe(2);
		result.TotalConnections.ShouldBe(10);
		result.ActiveConnections.ShouldBe(5);
		result.Message.ShouldBe("Pool healthy");
		result.CheckedAt.ShouldBe(now);
	}

	[Fact]
	public void PooledConnectionHaveCorrectDefaults()
	{
		// Arrange & Act
		var connection = new PooledConnection<object> { Client = new object() };

		// Assert
		connection.Client.ShouldNotBeNull();
		connection.CreatedAt.ShouldNotBe(default);
		connection.LastUsedAt.ShouldNotBe(default);
		connection.UseCount.ShouldBe(0);
		connection.InUse.ShouldBeFalse();
		connection.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void PooledConnectionAllowSettingAllProperties()
	{
		// Arrange
		var client = "test-client";
		var now = DateTimeOffset.UtcNow;

		// Act
		var connection = new PooledConnection<string>
		{
			Client = client,
			CreatedAt = now,
			LastUsedAt = now,
			UseCount = 42,
			InUse = true,
		};
		connection.Metadata["region"] = "us-east-1";

		// Assert
		connection.Client.ShouldBe(client);
		connection.CreatedAt.ShouldBe(now);
		connection.LastUsedAt.ShouldBe(now);
		connection.UseCount.ShouldBe(42);
		connection.InUse.ShouldBeTrue();
		connection.Metadata.Count.ShouldBe(1);
	}
}
