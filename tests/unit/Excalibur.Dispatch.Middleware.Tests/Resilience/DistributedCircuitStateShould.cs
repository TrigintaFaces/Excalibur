// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DistributedCircuitStateShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var sut = new DistributedCircuitState();

		// Assert
		sut.State.ShouldBe(CircuitState.Closed);
		sut.InstanceId.ShouldBe(string.Empty);
	}

	[Fact]
	public void SetAndGetState()
	{
		var sut = new DistributedCircuitState { State = CircuitState.Open };
		sut.State.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void SetAndGetHalfOpenState()
	{
		var sut = new DistributedCircuitState { State = CircuitState.HalfOpen };
		sut.State.ShouldBe(CircuitState.HalfOpen);
	}

	[Fact]
	public void SetAndGetOpenedAt()
	{
		var now = DateTimeOffset.UtcNow;
		var sut = new DistributedCircuitState { OpenedAt = now };
		sut.OpenedAt.ShouldBe(now);
	}

	[Fact]
	public void SetAndGetOpenUntil()
	{
		var until = DateTimeOffset.UtcNow.AddMinutes(5);
		var sut = new DistributedCircuitState { OpenUntil = until };
		sut.OpenUntil.ShouldBe(until);
	}

	[Fact]
	public void SetAndGetTransitionedAt()
	{
		var now = DateTimeOffset.UtcNow;
		var sut = new DistributedCircuitState { TransitionedAt = now };
		sut.TransitionedAt.ShouldBe(now);
	}

	[Fact]
	public void SetAndGetInstanceId()
	{
		var sut = new DistributedCircuitState { InstanceId = "instance-42" };
		sut.InstanceId.ShouldBe("instance-42");
	}
}
