using Company.ExcaliburCqrs.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace Company.ExcaliburCqrs.Tests.Domain;

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
    public void TransitionToShippedStatus()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "PROD-001", 2);

        // Act
        order.Ship();

        // Assert
        order.Status.ShouldBe(OrderStatus.Shipped);
    }

    [Fact]
    public void ThrowWhenShippingAlreadyShippedOrder()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "PROD-001", 1);
        order.Ship();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => order.Ship());
    }
}
