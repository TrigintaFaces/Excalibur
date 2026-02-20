namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class PageableResultDepthShould
{
    [Fact]
    public void Constructor_WithItemsOnly_SetDefaults()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };

        // Act
        var result = new PageableResult<string>(items);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(3);
        result.TotalItems.ShouldBe(3);
        result.TotalPages.ShouldBe(1);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeFalse();
        result.IsFirstPage.ShouldBeTrue();
        result.IsLastPage.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetCorrectly()
    {
        // Arrange
        var items = new[] { "a", "b" };

        // Act
        var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 2, totalItems: 10);

        // Assert
        result.PageNumber.ShouldBe(2);
        result.PageSize.ShouldBe(2);
        result.TotalItems.ShouldBe(10);
        result.TotalPages.ShouldBe(5);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeTrue();
        result.IsFirstPage.ShouldBeFalse();
        result.IsLastPage.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_FirstPage_ReportsCorrectly()
    {
        // Arrange & Act
        var result = new PageableResult<string>(["a"], pageNumber: 1, pageSize: 1, totalItems: 5);

        // Assert
        result.IsFirstPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_LastPage_ReportsCorrectly()
    {
        // Arrange & Act
        var result = new PageableResult<string>(["a"], pageNumber: 5, pageSize: 1, totalItems: 5);

        // Assert
        result.IsLastPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_ThrowOnNullItems()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PageableResult<string>(null!));
    }

    [Fact]
    public void Constructor_ThrowWhenPageNumberZero()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageNumber: 0, pageSize: 10));
    }

    [Fact]
    public void Constructor_ThrowWhenPageSizeZero()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageNumber: 1, pageSize: 0));
    }

    [Fact]
    public void Constructor_ThrowWhenPageNumberNegative()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageNumber: -1, pageSize: 10));
    }

    [Fact]
    public void Constructor_ThrowWhenPageSizeNegative()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageNumber: 1, pageSize: -1));
    }

    [Fact]
    public void Constructor_ThrowWhenPageNumberWithoutPageSize()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageNumber: 1));
    }

    [Fact]
    public void Constructor_ThrowWhenPageSizeWithoutPageNumber()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a"], pageSize: 10));
    }

    [Fact]
    public void Constructor_ThrowWhenTotalItemsLessThanItemCount()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PageableResult<string>(["a", "b", "c"], totalItems: 1));
    }

    [Fact]
    public void Indexer_ReturnCorrectItem()
    {
        // Arrange
        var result = new PageableResult<string>(["first", "second", "third"]);

        // Act & Assert
        result[0].ShouldBe("first");
        result[1].ShouldBe("second");
        result[2].ShouldBe("third");
    }

    [Fact]
    public void GetEnumerator_IterateItems()
    {
        // Arrange
        var result = new PageableResult<string>(["a", "b", "c"]);
        var items = new List<string>();

        // Act
        using var enumerator = result.GetEnumerator();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        // Assert
        items.ShouldBe(new[] { "a", "b", "c" });
    }

    [Fact]
    public void TotalPages_CalculateCorrectly_WithRemainder()
    {
        // Arrange: 11 items, 3 per page = 4 pages
        var result = new PageableResult<string>(["a", "b", "c"], pageNumber: 1, pageSize: 3, totalItems: 11);

        // Act & Assert
        result.TotalPages.ShouldBe(4);
    }

    [Fact]
    public void TotalPages_CalculateCorrectly_ExactDivision()
    {
        // Arrange: 9 items, 3 per page = 3 pages
        var result = new PageableResult<string>(["a", "b", "c"], pageNumber: 1, pageSize: 3, totalItems: 9);

        // Act & Assert
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    public void EmptyItems_HandleCorrectly()
    {
        // Arrange & Act
        var result = new PageableResult<string>(Array.Empty<string>());

        // Assert
        result.Items.ShouldBeEmpty();
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(0);
        result.TotalItems.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_AcceptIEnumerable()
    {
        // Arrange
        IEnumerable<string> items = new[] { "a", "b" }.AsEnumerable();

        // Act
        var result = new PageableResult<string>(items);

        // Assert
        result.Items.Count.ShouldBe(2);
    }
}
