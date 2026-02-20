// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;

namespace Excalibur.Saga.Tests.EventSourced;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaStepFailedShould
{
	[Fact]
	public void DefaultSagaIdToEmpty()
	{
		var sut = new SagaStepFailed();
		sut.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExposeEventTypeAsSagaStepFailed()
	{
		var sut = new SagaStepFailed();
		sut.EventType.ShouldBe("SagaStepFailed");
	}

	[Fact]
	public void DefaultStepNameToEmpty()
	{
		var sut = new SagaStepFailed();
		sut.StepName.ShouldBe(string.Empty);
	}

	[Fact]
	public void DefaultErrorMessageToEmpty()
	{
		var sut = new SagaStepFailed();
		sut.ErrorMessage.ShouldBe(string.Empty);
	}

	[Fact]
	public void DefaultRetryCountToZero()
	{
		var sut = new SagaStepFailed();
		sut.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void ExposeAllProperties()
	{
		var sut = new SagaStepFailed
		{
			SagaId = "saga-5",
			StepName = "ChargeCustomer",
			StepIndex = 3,
			ErrorMessage = "Insufficient funds",
			RetryCount = 2,
		};

		sut.SagaId.ShouldBe("saga-5");
		sut.StepName.ShouldBe("ChargeCustomer");
		sut.StepIndex.ShouldBe(3);
		sut.ErrorMessage.ShouldBe("Insufficient funds");
		sut.RetryCount.ShouldBe(2);
	}

	[Fact]
	public void ImplementISagaEvent()
	{
		var sut = new SagaStepFailed();
		sut.ShouldBeAssignableTo<ISagaEvent>();
	}
}
