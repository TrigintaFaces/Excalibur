// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DistributedCircuitJsonContext"/> serialization context.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DistributedCircuitJsonContextShould : UnitTestBase
{
	[Fact]
	public void RoundTrip_DistributedCircuitState_Succeeds()
	{
		// Arrange
		var state = new DistributedCircuitState
		{
			State = CircuitState.Open,
			OpenedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
			OpenUntil = DateTimeOffset.UtcNow.AddMinutes(4),
			TransitionedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
			InstanceId = "node-1",
		};

		// Act
		var json = JsonSerializer.Serialize(state, DistributedCircuitJsonContext.Default.DistributedCircuitState);
		var deserialized = JsonSerializer.Deserialize(json, DistributedCircuitJsonContext.Default.DistributedCircuitState);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.State.ShouldBe(CircuitState.Open);
		deserialized.InstanceId.ShouldBe("node-1");
	}

	[Fact]
	public void RoundTrip_DistributedCircuitMetrics_Succeeds()
	{
		// Arrange
		var metrics = new DistributedCircuitMetrics
		{
			SuccessCount = 100,
			FailureCount = 5,
			ConsecutiveFailures = 2,
			ConsecutiveSuccesses = 0,
			LastSuccess = DateTimeOffset.UtcNow.AddSeconds(-30),
			LastFailure = DateTimeOffset.UtcNow.AddSeconds(-5),
			LastFailureReason = "Connection timeout",
		};

		// Act
		var json = JsonSerializer.Serialize(metrics, DistributedCircuitJsonContext.Default.DistributedCircuitMetrics);
		var deserialized = JsonSerializer.Deserialize(json, DistributedCircuitJsonContext.Default.DistributedCircuitMetrics);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.SuccessCount.ShouldBe(100);
		deserialized.FailureCount.ShouldBe(5);
		deserialized.ConsecutiveFailures.ShouldBe(2);
		deserialized.LastFailureReason.ShouldBe("Connection timeout");
	}

	[Fact]
	public void Serialization_IsNotIndented()
	{
		// Arrange
		var state = new DistributedCircuitState
		{
			State = CircuitState.Closed,
			InstanceId = "test",
		};

		// Act
		var json = JsonSerializer.Serialize(state, DistributedCircuitJsonContext.Default.DistributedCircuitState);

		// Assert — WriteIndented = false means no newlines
		json.ShouldNotContain("\n");
	}
}
