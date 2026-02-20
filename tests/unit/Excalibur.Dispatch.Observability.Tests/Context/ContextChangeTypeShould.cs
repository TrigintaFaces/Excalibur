// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextChangeType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextChangeTypeShould
{
	[Theory]
	[InlineData(ContextChangeType.Added, 0)]
	[InlineData(ContextChangeType.Removed, 1)]
	[InlineData(ContextChangeType.Modified, 2)]
	public void HaveCorrectIntegerValue(ContextChangeType type, int expectedValue)
	{
		// Assert
		((int)type).ShouldBe(expectedValue);
	}

	[Fact]
	public void HaveThreeValues()
	{
		// Assert
		Enum.GetValues<ContextChangeType>().ShouldBe([
			ContextChangeType.Added,
			ContextChangeType.Removed,
			ContextChangeType.Modified,
		]);
	}

	[Theory]
	[InlineData("Added", ContextChangeType.Added)]
	[InlineData("Removed", ContextChangeType.Removed)]
	[InlineData("Modified", ContextChangeType.Modified)]
	public void ParseFromString(string value, ContextChangeType expected)
	{
		// Act & Assert
		Enum.Parse<ContextChangeType>(value).ShouldBe(expected);
	}

	[Theory]
	[InlineData(ContextChangeType.Added, "Added")]
	[InlineData(ContextChangeType.Removed, "Removed")]
	[InlineData(ContextChangeType.Modified, "Modified")]
	public void ConvertToString(ContextChangeType type, string expected)
	{
		// Act & Assert
		type.ToString().ShouldBe(expected);
	}

	[Fact]
	public void DefaultToAdded()
	{
		// Arrange
		ContextChangeType defaultValue = default;

		// Assert
		defaultValue.ShouldBe(ContextChangeType.Added);
	}
}
