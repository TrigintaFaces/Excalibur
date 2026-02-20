namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class PageNavigationDepthShould
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
    public void AllValues_AreDefined()
    {
        // Assert
        Enum.GetValues<PageNavigation>().Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(PageNavigation.First)]
    [InlineData(PageNavigation.Previous)]
    [InlineData(PageNavigation.Next)]
    [InlineData(PageNavigation.Last)]
    public void Enum_IsDefined(PageNavigation value)
    {
        // Assert
        Enum.IsDefined(value).ShouldBeTrue();
    }
}
