// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="SagaStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "Models")]
public sealed class SagaStatusShould : UnitTestBase
{
	[Fact]
	public void HaveCreatedAsZero()
	{
		// Assert
		((int)SagaStatus.Created).ShouldBe(0);
	}

	[Fact]
	public void HaveRunningAsOne()
	{
		// Assert
		((int)SagaStatus.Running).ShouldBe(1);
	}

	[Fact]
	public void HaveCompletedAsTwo()
	{
		// Assert
		((int)SagaStatus.Completed).ShouldBe(2);
	}

	[Fact]
	public void HaveFailedAsThree()
	{
		// Assert
		((int)SagaStatus.Failed).ShouldBe(3);
	}

	[Fact]
	public void HaveCompensatingAsFour()
	{
		// Assert
		((int)SagaStatus.Compensating).ShouldBe(4);
	}

	[Fact]
	public void HaveCompensatedAsFive()
	{
		// Assert
		((int)SagaStatus.Compensated).ShouldBe(5);
	}

	[Fact]
	public void HaveCancelledAsSix()
	{
		// Assert
		((int)SagaStatus.Cancelled).ShouldBe(6);
	}

	[Fact]
	public void HaveSuspendedAsSeven()
	{
		// Assert
		((int)SagaStatus.Suspended).ShouldBe(7);
	}

	[Fact]
	public void HaveExpiredAsEight()
	{
		// Assert
		((int)SagaStatus.Expired).ShouldBe(8);
	}

	[Fact]
	public void HaveNineDefinedValues()
	{
		// Assert
		Enum.GetValues<SagaStatus>().Length.ShouldBe(9);
	}

	[Fact]
	public void DefaultToCreated()
	{
		// Act
		var defaultValue = default(SagaStatus);

		// Assert
		defaultValue.ShouldBe(SagaStatus.Created);
	}

	[Theory]
	[InlineData(SagaStatus.Created, "Created")]
	[InlineData(SagaStatus.Running, "Running")]
	[InlineData(SagaStatus.Completed, "Completed")]
	[InlineData(SagaStatus.Failed, "Failed")]
	[InlineData(SagaStatus.Compensating, "Compensating")]
	[InlineData(SagaStatus.Compensated, "Compensated")]
	[InlineData(SagaStatus.Cancelled, "Cancelled")]
	[InlineData(SagaStatus.Suspended, "Suspended")]
	[InlineData(SagaStatus.Expired, "Expired")]
	public void HaveCorrectStringRepresentation(SagaStatus status, string expected)
	{
		// Assert
		status.ToString().ShouldBe(expected);
	}
}
