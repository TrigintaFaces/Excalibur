// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DistributedCircuitJsonContext"/> serialization context.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
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
