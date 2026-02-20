// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;

namespace Excalibur.Saga.Tests.EventSourced;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaStepCompletedShould
{
	[Fact]
	public void DefaultSagaIdToEmpty()
	{
		var sut = new SagaStepCompleted();
		sut.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExposeEventTypeAsSagaStepCompleted()
	{
		var sut = new SagaStepCompleted();
		sut.EventType.ShouldBe("SagaStepCompleted");
	}

	[Fact]
	public void DefaultStepNameToEmpty()
	{
		var sut = new SagaStepCompleted();
		sut.StepName.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExposeAllProperties()
	{
		var duration = TimeSpan.FromMilliseconds(250);
		var sut = new SagaStepCompleted
		{
			SagaId = "saga-1",
			StepName = "ProcessPayment",
			StepIndex = 2,
			Duration = duration,
		};

		sut.SagaId.ShouldBe("saga-1");
		sut.StepName.ShouldBe("ProcessPayment");
		sut.StepIndex.ShouldBe(2);
		sut.Duration.ShouldBe(duration);
	}

	[Fact]
	public void DefaultStepIndexToZero()
	{
		var sut = new SagaStepCompleted();
		sut.StepIndex.ShouldBe(0);
	}

	[Fact]
	public void ImplementISagaEvent()
	{
		var sut = new SagaStepCompleted();
		sut.ShouldBeAssignableTo<ISagaEvent>();
	}
}
