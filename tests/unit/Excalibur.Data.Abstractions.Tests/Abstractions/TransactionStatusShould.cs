// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="TransactionStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class TransactionStatusShould : UnitTestBase
{
	[Fact]
	public void HaveEightStatuses()
	{
		// Act
		var values = Enum.GetValues<TransactionStatus>();

		// Assert
		values.Length.ShouldBe(8);
	}

	[Fact]
	public void HaveActiveAsDefault()
	{
		// Assert
		TransactionStatus defaultValue = default;
		defaultValue.ShouldBe(TransactionStatus.Active);
	}

	[Theory]
	[InlineData(TransactionStatus.Active, 0)]
	[InlineData(TransactionStatus.Committing, 1)]
	[InlineData(TransactionStatus.Committed, 2)]
	[InlineData(TransactionStatus.RollingBack, 3)]
	[InlineData(TransactionStatus.RolledBack, 4)]
	[InlineData(TransactionStatus.Failed, 5)]
	[InlineData(TransactionStatus.TimedOut, 6)]
	[InlineData(TransactionStatus.Disposed, 7)]
	public void HaveCorrectUnderlyingValues(TransactionStatus status, int expectedValue)
	{
		// Assert
		((int)status).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Active", TransactionStatus.Active)]
	[InlineData("Committing", TransactionStatus.Committing)]
	[InlineData("Committed", TransactionStatus.Committed)]
	[InlineData("RollingBack", TransactionStatus.RollingBack)]
	[InlineData("RolledBack", TransactionStatus.RolledBack)]
	[InlineData("Failed", TransactionStatus.Failed)]
	[InlineData("TimedOut", TransactionStatus.TimedOut)]
	[InlineData("Disposed", TransactionStatus.Disposed)]
	public void ParseFromString(string input, TransactionStatus expected)
	{
		// Act
		var result = Enum.Parse<TransactionStatus>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Act & Assert
		foreach (var status in Enum.GetValues<TransactionStatus>())
		{
			Enum.IsDefined(status).ShouldBeTrue();
		}
	}
}
