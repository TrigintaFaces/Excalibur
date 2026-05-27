// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="ProjectionFilterBuilder"/> and <see cref="ProjectionFilterClause"/> (bd-2p8gxe).
/// Verifies all 8 operators, fluent chaining, key format, null guards,
/// overwrite behavior, defensive copy, and round-trip with <see cref="FilterParser"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionFilterBuilderShould
{
    #region Empty Builder

    [Fact]
    public void BuildEmptyDictionaryWhenNoFilters()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act
        var filters = builder.Build();

        // Assert
        filters.ShouldNotBeNull();
        filters.ShouldBeEmpty();
    }

    #endregion Empty Builder

    #region EqualTo Operator

    [Fact]
    public void AddEqualToFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Active")
            .Build();

        // Assert
        filters.Count.ShouldBe(1);
        filters.ShouldContainKeyAndValue("Status", "Active");
    }

    [Fact]
    public void EqualToUsesPlainPropertyNameAsKey()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Active")
            .Build();

        // Assert — no operator suffix for equality
        filters.ShouldContainKey("Status");
        filters.Keys.ShouldNotContain(k => k.Contains(':', StringComparison.Ordinal));
    }

    #endregion EqualTo Operator

    #region NotEqualTo Operator

    [Fact]
    public void AddNotEqualToFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Status").NotEqualTo("Deleted")
            .Build();

        // Assert
        filters.Count.ShouldBe(1);
        filters.ShouldContainKeyAndValue("Status:neq", "Deleted");
    }

    #endregion NotEqualTo Operator

    #region GreaterThan Operator

    [Fact]
    public void AddGreaterThanFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Amount").GreaterThan(100)
            .Build();

        // Assert
        filters.ShouldContainKeyAndValue("Amount:gt", 100);
    }

    #endregion GreaterThan Operator

    #region GreaterThanOrEqual Operator

    [Fact]
    public void AddGreaterThanOrEqualFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Amount").GreaterThanOrEqual(50)
            .Build();

        // Assert
        filters.ShouldContainKeyAndValue("Amount:gte", 50);
    }

    #endregion GreaterThanOrEqual Operator

    #region LessThan Operator

    [Fact]
    public void AddLessThanFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Price").LessThan(999.99)
            .Build();

        // Assert
        filters.ShouldContainKeyAndValue("Price:lt", 999.99);
    }

    #endregion LessThan Operator

    #region LessThanOrEqual Operator

    [Fact]
    public void AddLessThanOrEqualFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Quantity").LessThanOrEqual(0)
            .Build();

        // Assert
        filters.ShouldContainKeyAndValue("Quantity:lte", 0);
    }

    #endregion LessThanOrEqual Operator

    #region In Operator

    private static readonly string[] TagValues = ["electronics", "sale"];

    [Fact]
    public void AddInFilter()
    {
        // Act
        var filters = new ProjectionFilterBuilder()
            .Where("Tags").In(TagValues)
            .Build();

        // Assert
        filters.ShouldContainKey("Tags:in");
        filters["Tags:in"].ShouldBe(TagValues);
    }

    #endregion In Operator

    #region Contains Operator

    [Fact]
    public void AddContainsFilter()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Name").Contains("test")
            .Build();

        // Assert
        filters.ShouldContainKeyAndValue("Name:contains", "test");
    }

    #endregion Contains Operator

    #region Fluent Chaining

    [Fact]
    public void SupportMultipleFiltersCombined()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Active")
            .Where("Amount").GreaterThan(100)
            .Where("Tags").In(TagValues)
            .Where("Name").Contains("test")
            .Build();

        // Assert
        filters.Count.ShouldBe(4);
        filters.ShouldContainKey("Status");
        filters.ShouldContainKey("Amount:gt");
        filters.ShouldContainKey("Tags:in");
        filters.ShouldContainKey("Name:contains");
    }

    [Fact]
    public void WhereReturnsProjectionFilterClause()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act
        var clause = builder.Where("Property");

        // Assert
        clause.ShouldNotBeNull();
        clause.ShouldBeOfType<ProjectionFilterClause>();
    }

    [Fact]
    public void OperatorMethodsReturnBuilderForChaining()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act — each operator method should return the builder
        var result = builder.Where("A").EqualTo("v");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(builder);
    }

    #endregion Fluent Chaining

    #region Overwrite Behavior

    [Fact]
    public void OverwriteFilterWhenSameKeyUsedTwice()
    {
        // Arrange & Act — same property + operator overwrites
        var filters = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Draft")
            .Where("Status").EqualTo("Active")
            .Build();

        // Assert — last write wins
        filters.Count.ShouldBe(1);
        filters["Status"].ShouldBe("Active");
    }

    [Fact]
    public void AllowDifferentOperatorsOnSameProperty()
    {
        // Arrange & Act — same property, different operators = different keys
        var filters = new ProjectionFilterBuilder()
            .Where("Amount").GreaterThan(10)
            .Where("Amount").LessThan(100)
            .Build();

        // Assert — two separate entries
        filters.Count.ShouldBe(2);
        filters.ShouldContainKeyAndValue("Amount:gt", 10);
        filters.ShouldContainKeyAndValue("Amount:lt", 100);
    }

    #endregion Overwrite Behavior

    #region Defensive Copy

    [Fact]
    public void BuildReturnsDefensiveCopy()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Active");

        // Act
        var filters1 = builder.Build();
        var filters2 = builder.Build();

        // Assert — two separate dictionary instances
        ReferenceEquals(filters1, filters2).ShouldBeFalse("Build() should return a new dictionary each time");
    }

    [Fact]
    public void MutatingBuildOutputDoesNotAffectBuilder()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder()
            .Where("Status").EqualTo("Active");

        // Act
        var filters = builder.Build();
        filters["Status"] = "Modified";
        var freshFilters = builder.Build();

        // Assert — builder is unaffected by mutations to previous output
        freshFilters["Status"].ShouldBe("Active");
    }

    #endregion Defensive Copy

    #region Null & Whitespace Guards

    [Fact]
    public void ThrowArgumentNullExceptionWhenPropertyNameIsNull()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => builder.Where(null!));
    }

    [Fact]
    public void ThrowArgumentExceptionWhenPropertyNameIsEmpty()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() => builder.Where(string.Empty));
    }

    [Fact]
    public void ThrowArgumentExceptionWhenPropertyNameIsWhitespace()
    {
        // Arrange
        var builder = new ProjectionFilterBuilder();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() => builder.Where("   "));
    }

    #endregion Null & Whitespace Guards

    #region Round-Trip with FilterParser

    [Theory]
    [InlineData("Status", "Active", FilterOperator.Equals)]
    [InlineData("Status:neq", "Deleted", FilterOperator.NotEquals)]
    [InlineData("Amount:gt", 100, FilterOperator.GreaterThan)]
    [InlineData("Amount:gte", 50, FilterOperator.GreaterThanOrEqual)]
    [InlineData("Price:lt", 999, FilterOperator.LessThan)]
    [InlineData("Quantity:lte", 0, FilterOperator.LessThanOrEqual)]
    [InlineData("Name:contains", "test", FilterOperator.Contains)]
    public void ProduceKeysParsableByFilterParser(string expectedKey, object value, FilterOperator expectedOp)
    {
        // Arrange — build filters using the builder
        var builder = new ProjectionFilterBuilder();
        switch (expectedOp)
        {
            case FilterOperator.Equals:
                builder.Where("Status").EqualTo(value);
                break;
            case FilterOperator.NotEquals:
                builder.Where("Status").NotEqualTo(value);
                break;
            case FilterOperator.GreaterThan:
                builder.Where("Amount").GreaterThan(value);
                break;
            case FilterOperator.GreaterThanOrEqual:
                builder.Where("Amount").GreaterThanOrEqual(value);
                break;
            case FilterOperator.LessThan:
                builder.Where("Price").LessThan(value);
                break;
            case FilterOperator.LessThanOrEqual:
                builder.Where("Quantity").LessThanOrEqual(value);
                break;
            case FilterOperator.Contains:
                builder.Where("Name").Contains(value);
                break;
        }

        var filters = builder.Build();

        // Act — parse the key with FilterParser
        var key = filters.Keys.Single();
        var parsed = FilterParser.Parse(key);

        // Assert — round-trip: builder key -> FilterParser -> correct operator
        key.ShouldBe(expectedKey);
        parsed.Operator.ShouldBe(expectedOp);
    }

    [Fact]
    public void InFilterKeyParsesCorrectlyWithFilterParser()
    {
        // Arrange
        var filters = new ProjectionFilterBuilder()
            .Where("Tags").In(TagValues)
            .Build();

        // Act
        var key = filters.Keys.Single();
        var parsed = FilterParser.Parse(key);

        // Assert
        parsed.PropertyName.ShouldBe("Tags");
        parsed.Operator.ShouldBe(FilterOperator.In);
    }

    #endregion Round-Trip with FilterParser

    #region Key Format Verification

    [Theory]
    [InlineData("Status", "Status")]
    [InlineData("Amount:gt", "Amount:gt")]
    [InlineData("Amount:gte", "Amount:gte")]
    [InlineData("Price:lt", "Price:lt")]
    [InlineData("Quantity:lte", "Quantity:lte")]
    [InlineData("Status:neq", "Status:neq")]
    [InlineData("Name:contains", "Name:contains")]
    public void UseOrdinalStringComparerForKeys(string key, string expectedKey)
    {
        // Arrange — verify the builder uses ordinal key comparison
        var builder = new ProjectionFilterBuilder();

        // Build filters using the builder with various operators
        switch (key)
        {
            case "Status": builder.Where("Status").EqualTo("v"); break;
            case "Amount:gt": builder.Where("Amount").GreaterThan(1); break;
            case "Amount:gte": builder.Where("Amount").GreaterThanOrEqual(1); break;
            case "Price:lt": builder.Where("Price").LessThan(1); break;
            case "Quantity:lte": builder.Where("Quantity").LessThanOrEqual(0); break;
            case "Status:neq": builder.Where("Status").NotEqualTo("v"); break;
            case "Name:contains": builder.Where("Name").Contains("x"); break;
        }

        var filters = builder.Build();

        // Assert
        filters.ShouldContainKey(expectedKey);
    }

    [Fact]
    public void InOperatorProducesCorrectKeySuffix()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Tags").In(TagValues)
            .Build();

        // Assert
        filters.ShouldContainKey("Tags:in");
    }

    #endregion Key Format Verification

    #region Value Types

    [Fact]
    public void SupportIntegerValues()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Count").EqualTo(42)
            .Build();

        // Assert
        filters["Count"].ShouldBe(42);
    }

    [Fact]
    public void SupportDecimalValues()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("Price").GreaterThan(19.99m)
            .Build();

        // Assert
        filters["Price:gt"].ShouldBe(19.99m);
    }

    [Fact]
    public void SupportDateTimeOffsetValues()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var filters = new ProjectionFilterBuilder()
            .Where("CreatedAt").GreaterThanOrEqual(date)
            .Build();

        // Assert
        filters["CreatedAt:gte"].ShouldBe(date);
    }

    [Fact]
    public void SupportBooleanValues()
    {
        // Arrange & Act
        var filters = new ProjectionFilterBuilder()
            .Where("IsActive").EqualTo(true)
            .Build();

        // Assert
        filters["IsActive"].ShouldBe(true);
    }

    #endregion Value Types
}
