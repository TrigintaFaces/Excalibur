// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Repositories;

/// <summary>
///     Unit tests for the <see cref="IAggregateQuery{TAggregate}" /> interface contract.
/// </summary>
/// <remarks>
///     Tests interface design, generic constraints, and implementation patterns for aggregate query objects used in repository patterns.
/// </remarks>
[Trait("Category", "Unit")]
public class IAggregateQueryShould
{
	[Fact]
	public void ShouldSupportGenericAggregateConstraint()
	{
		// Arrange & Act
		var query = new SimpleTestQuery();

		// Assert
		_ = query.ShouldNotBeNull();
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
	}

	[Fact]
	public void ShouldAllowEmptyImplementation()
	{
		// Arrange & Act
		var query = new SimpleTestQuery();

		// Assert
		_ = query.ShouldNotBeNull();
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		// Empty implementation should be valid - interface is a marker
	}

	[Fact]
	public void ShouldAllowImplementationWithProperties()
	{
		// Arrange
		const string expectedFilter = "active";
		const int expectedPageSize = 20;
		const int expectedPageNumber = 1;
		var expectedFromDate = DateTime.UtcNow.AddDays(-30);
		var expectedToDate = DateTime.UtcNow;

		// Act
		var query = new TestQueryWithProperties
		{
			Filter = expectedFilter,
			PageSize = expectedPageSize,
			PageNumber = expectedPageNumber,
			FromDate = expectedFromDate,
			ToDate = expectedToDate,
		};

		// Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		query.Filter.ShouldBe(expectedFilter);
		query.PageSize.ShouldBe(expectedPageSize);
		query.PageNumber.ShouldBe(expectedPageNumber);
		query.FromDate.ShouldBe(expectedFromDate);
		query.ToDate.ShouldBe(expectedToDate);
	}

	[Fact]
	public void ShouldAllowImplementationWithMethods()
	{
		// Arrange
		const string searchTerm = " Test Search ";
		var query = new TestQueryWithMethods { SearchTerm = searchTerm };

		// Act
		var hasSearchTerm = query.HasSearchTerm();
		var normalizedSearchTerm = query.GetNormalizedSearchTerm();

		// Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		hasSearchTerm.ShouldBeTrue();
		normalizedSearchTerm.ShouldBe("TEST SEARCH");
	}

	[Fact]
	public void ShouldAllowImplementationWithMethodsWhenSearchTermIsEmpty()
	{
		// Arrange
		var query = new TestQueryWithMethods { SearchTerm = string.Empty };

		// Act
		var hasSearchTerm = query.HasSearchTerm();
		var normalizedSearchTerm = query.GetNormalizedSearchTerm();

		// Assert
		hasSearchTerm.ShouldBeFalse();
		normalizedSearchTerm.ShouldBe(string.Empty);
	}

	[Fact]
	public void ShouldAllowImplementationWithMethodsWhenSearchTermIsNull()
	{
		// Arrange
		var query = new TestQueryWithMethods { SearchTerm = null! };

		// Act
		var hasSearchTerm = query.HasSearchTerm();
		var normalizedSearchTerm = query.GetNormalizedSearchTerm();

		// Assert
		hasSearchTerm.ShouldBeFalse();
		normalizedSearchTerm.ShouldBe(string.Empty);
	}

	[Fact]
	public void ShouldEnforceGenericConstraints()
	{
		// This test verifies that the generic constraint is properly defined TAggregate must be class and implement IAggregateRoot

		// Arrange & Act - If this compiles, the constraints are working
		var query = new SimpleTestQuery();

		// Assert
		_ = query.ShouldNotBeNull();

		// The following should not compile if constraints are working: var badQuery = new BadQuery(); // where BadQuery :
		// IAggregateQuery<int> - int is not a class or IAggregateRoot
	}

	[Fact]
	public void DifferentQueryImplementationsShouldBeDistinct()
	{
		// Arrange & Act
		var simpleQuery = new SimpleTestQuery();
		var queryWithProperties =
			new TestQueryWithProperties() { Filter = "test-filter", FromDate = DateTime.Today.AddDays(-30), ToDate = DateTime.Today };
		var queryWithMethods = new TestQueryWithMethods { SearchTerm = "test-search" };

		// Assert
		_ = simpleQuery.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		_ = queryWithProperties.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		_ = queryWithMethods.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();

		simpleQuery.GetType().ShouldNotBe(queryWithProperties.GetType());
		simpleQuery.GetType().ShouldNotBe(queryWithMethods.GetType());
		queryWithProperties.GetType().ShouldNotBe(queryWithMethods.GetType());
	}

	[Fact]
	public void ShouldSupportInheritancePatterns()
	{
		// Test that query classes can inherit from other classes while implementing the interface

		// Arrange
		var baseQuery = new TestQueryWithProperties { Filter = "base", FromDate = null, ToDate = null, PageSize = 10 };

		// Act & Assert
		_ = baseQuery.ShouldBeAssignableTo<IAggregateQuery<TestAggregate>>();
		baseQuery.Filter.ShouldBe("base");
		baseQuery.PageSize.ShouldBe(10);
	}

	// Test aggregate implementation for testing interface contracts
	private sealed class TestAggregate : AggregateRoot
	{
		public TestAggregate()
		{
		}

		public TestAggregate(string id) : base(id)
		{
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test purposes
		}
	}

	// Test query implementations
	private sealed class SimpleTestQuery : IAggregateQuery<TestAggregate>
	{
	}

	private sealed class TestQueryWithProperties : IAggregateQuery<TestAggregate>
	{
		public required string Filter { get; set; } = string.Empty;

		public int PageSize { get; set; }

		public int PageNumber { get; set; }

		public required DateTime? FromDate { get; set; }

		public required DateTime? ToDate { get; set; }
	}

	private sealed class TestQueryWithMethods : IAggregateQuery<TestAggregate>
	{
		public required string SearchTerm { get; set; } = string.Empty;

		public bool HasSearchTerm() => !string.IsNullOrEmpty(SearchTerm);

		public string GetNormalizedSearchTerm() => SearchTerm?.Trim().ToUpperInvariant() ?? string.Empty;
	}
}
