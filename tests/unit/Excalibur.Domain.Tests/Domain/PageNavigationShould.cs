// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="PageNavigation"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class PageNavigationShould
{
	[Fact]
	public void First_HasValueZero()
	{
		// Assert
		((int)PageNavigation.First).ShouldBe(0);
	}

	[Fact]
	public void Previous_HasValueOne()
	{
		// Assert
		((int)PageNavigation.Previous).ShouldBe(1);
	}

	[Fact]
	public void Next_HasValueTwo()
	{
		// Assert
		((int)PageNavigation.Next).ShouldBe(2);
	}

	[Fact]
	public void Last_HasValueThree()
	{
		// Assert
		((int)PageNavigation.Last).ShouldBe(3);
	}

	[Fact]
	public void HasFourValues()
	{
		// Arrange
		var values = Enum.GetValues<PageNavigation>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void CanBeParsedFromString()
	{
		// Act & Assert
		Enum.Parse<PageNavigation>("First").ShouldBe(PageNavigation.First);
		Enum.Parse<PageNavigation>("Previous").ShouldBe(PageNavigation.Previous);
		Enum.Parse<PageNavigation>("Next").ShouldBe(PageNavigation.Next);
		Enum.Parse<PageNavigation>("Last").ShouldBe(PageNavigation.Last);
	}

	[Fact]
	public void CanBeParsedFromInt()
	{
		// Act & Assert
		((PageNavigation)0).ShouldBe(PageNavigation.First);
		((PageNavigation)1).ShouldBe(PageNavigation.Previous);
		((PageNavigation)2).ShouldBe(PageNavigation.Next);
		((PageNavigation)3).ShouldBe(PageNavigation.Last);
	}

	[Fact]
	public void ToString_ReturnsEnumName()
	{
		// Act & Assert
		PageNavigation.First.ToString().ShouldBe("First");
		PageNavigation.Previous.ToString().ShouldBe("Previous");
		PageNavigation.Next.ToString().ShouldBe("Next");
		PageNavigation.Last.ToString().ShouldBe("Last");
	}
}
