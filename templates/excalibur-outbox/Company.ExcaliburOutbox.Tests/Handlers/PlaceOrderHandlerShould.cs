using Company.ExcaliburOutbox.Messages;
using Shouldly;
using Xunit;

namespace Company.ExcaliburOutbox.Tests.Handlers;

public class PlaceOrderHandlerShould
{
    [Fact]
    public void CreateCommandWithCorrectProperties()
    {
        var command = new PlaceOrderCommand
        {
            OrderId = Guid.NewGuid(),
            ProductId = "PROD-001",
            Quantity = 5
        };

        command.ProductId.ShouldBe("PROD-001");
        command.Quantity.ShouldBe(5);
    }
}
