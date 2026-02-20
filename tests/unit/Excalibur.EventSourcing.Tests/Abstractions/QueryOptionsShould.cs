// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="QueryOptions"/> to verify pagination and sorting options.
/// </summary>
[Trait("Category", "Unit")]
public sealed class QueryOptionsShould
{
	[Fact]
	public void Default_HasNullSkipAndTake()
	{
		// Arrange & Act
		var options = QueryOptions.Default;

		// Assert
		options.Skip.ShouldBeNull();
		options.Take.ShouldBeNull();
		options.OrderBy.ShouldBeNull();
		options.Descending.ShouldBeFalse();
	}

	[Fact]
	public void Create_WithPagination()
	{
		// Arrange & Act
		var options = new QueryOptions(Skip: 10, Take: 25);

		// Assert
		options.Skip.ShouldBe(10);
		options.Take.ShouldBe(25);
	}

	[Fact]
	public void Create_WithSorting()
	{
		// Arrange & Act
		var options = new QueryOptions(OrderBy: "CreatedDate", Descending: true);

		// Assert
		options.OrderBy.ShouldBe("CreatedDate");
		options.Descending.ShouldBeTrue();
	}

	[Fact]
	public void Create_WithAllOptions()
	{
		// Arrange & Act
		var options = new QueryOptions(
			Skip: 20,
			Take: 50,
			OrderBy: "Name",
			Descending: true);

		// Assert
		options.Skip.ShouldBe(20);
		options.Take.ShouldBe(50);
		options.OrderBy.ShouldBe("Name");
		options.Descending.ShouldBeTrue();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var options1 = new QueryOptions(Skip: 10, Take: 25, OrderBy: "Name", Descending: false);
		var options2 = new QueryOptions(Skip: 10, Take: 25, OrderBy: "Name", Descending: false);

		// Assert
		options1.ShouldBe(options2);
	}

	[Fact]
	public void SupportWithExpression_ForImmutableUpdates()
	{
		// Arrange
		var original = new QueryOptions(Skip: 0, Take: 10);

		// Act
		var updated = original with { Take = 20 };

		// Assert
		updated.Take.ShouldBe(20);
		original.Take.ShouldBe(10);
	}

	[Fact]
	public void Default_IsSingleton()
	{
		// Arrange & Act
		var default1 = QueryOptions.Default;
		var default2 = QueryOptions.Default;

		// Assert
		ReferenceEquals(default1, default2).ShouldBeTrue();
	}
}
