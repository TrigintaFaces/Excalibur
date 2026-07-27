// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Unit tests for <see cref="SagaQueryFilter"/>, verifying that paging bounds are structurally
/// clamped so a query can never request an unbounded result set across store providers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaQueryFilterShould : UnitTestBase
{
	[Fact]
	public void DefaultToTheDefaultPageSize()
	{
		var filter = new SagaQueryFilter();

		filter.MaxResults.ShouldBe(SagaQueryFilter.DefaultMaxResults);
		filter.Skip.ShouldBe(0);
	}

	[Fact]
	public void ClampMaxResultsDownToTheHardUpperBound()
	{
		var filter = new SagaQueryFilter { MaxResults = int.MaxValue };

		// Without the clamp this would translate to an unbounded LIMIT/Take across all providers.
		filter.MaxResults.ShouldBe(SagaQueryFilter.MaxAllowedResults);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	[InlineData(int.MinValue)]
	public void ClampNonPositiveMaxResultsUpToOne(int requested)
	{
		var filter = new SagaQueryFilter { MaxResults = requested };

		filter.MaxResults.ShouldBe(1);
	}

	[Fact]
	public void PreserveAnInRangeMaxResults()
	{
		var filter = new SagaQueryFilter { MaxResults = 250 };

		filter.MaxResults.ShouldBe(250);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void ClampNegativeSkipToZero(int requested)
	{
		var filter = new SagaQueryFilter { Skip = requested };

		filter.Skip.ShouldBe(0);
	}

	[Fact]
	public void PreserveANonNegativeSkip()
	{
		var filter = new SagaQueryFilter { Skip = 40 };

		filter.Skip.ShouldBe(40);
	}
}
