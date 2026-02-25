using Company.ExcaliburDdd.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace Company.ExcaliburDdd.Tests.Domain;

public class OrderShould
{
    [Fact]
    public void CreateWithCorrectStatusAndItems()
    {
        // Arrange & Act
        var order = Order.Create(Guid.NewGuid(), "PROD-001", 5);

        // Assert
        order.Status.ShouldBe(OrderStatus.Created);
        order.Items.Count.ShouldBe(1);
        order.Items[0].ProductId.ShouldBe("PROD-001");
        order.Items[0].Quantity.ShouldBe(5);
    }

    [Fact]
    public void ThrowWhenProductIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() => Order.Create(Guid.NewGuid(), "", 1));
    }

    [Fact]
    public void ThrowWhenQuantityIsZero()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Order.Create(Guid.NewGuid(), "PROD-001", 0));
    }
}
