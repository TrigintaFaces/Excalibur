// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "Abstractions")]
public sealed class SagaStateShould : UnitTestBase
{
	[Fact]
	public void HaveCreatedAsZero()
	{
		// Assert
		((int)SagaState.Created).ShouldBe(0);
	}

	[Fact]
	public void HaveRunningAsOne()
	{
		// Assert
		((int)SagaState.Running).ShouldBe(1);
	}

	[Fact]
	public void HaveCompensatingAsTwo()
	{
		// Assert
		((int)SagaState.Compensating).ShouldBe(2);
	}

	[Fact]
	public void HaveCompletedAsThree()
	{
		// Assert
		((int)SagaState.Completed).ShouldBe(3);
	}

	[Fact]
	public void HaveCompensatedSuccessfullyAsFour()
	{
		// Assert
		((int)SagaState.CompensatedSuccessfully).ShouldBe(4);
	}

	[Fact]
	public void HaveCompensationFailedAsFive()
	{
		// Assert
		((int)SagaState.CompensationFailed).ShouldBe(5);
	}

	[Fact]
	public void HaveCancelledAsSix()
	{
		// Assert
		((int)SagaState.Cancelled).ShouldBe(6);
	}

	[Fact]
	public void HaveSevenDefinedValues()
	{
		// Assert
		Enum.GetValues<SagaState>().Length.ShouldBe(7);
	}

	[Fact]
	public void DefaultToCreated()
	{
		// Act
		var defaultValue = default(SagaState);

		// Assert
		defaultValue.ShouldBe(SagaState.Created);
	}

	[Theory]
	[InlineData(SagaState.Created, "Created")]
	[InlineData(SagaState.Running, "Running")]
	[InlineData(SagaState.Compensating, "Compensating")]
	[InlineData(SagaState.Completed, "Completed")]
	[InlineData(SagaState.CompensatedSuccessfully, "CompensatedSuccessfully")]
	[InlineData(SagaState.CompensationFailed, "CompensationFailed")]
	[InlineData(SagaState.Cancelled, "Cancelled")]
	public void HaveCorrectStringRepresentation(SagaState state, string expected)
	{
		// Assert
		state.ToString().ShouldBe(expected);
	}
}
