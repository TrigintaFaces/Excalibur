using Company.ExcaliburSaga.Sagas;
using Shouldly;
using Xunit;

namespace Company.ExcaliburSaga.Tests.Sagas;

public class OrderSagaShould
{
    [Fact]
    public void InitializeWithCorrectDefaults()
    {
        var sagaData = new OrderSagaData();

        sagaData.OrderId.ShouldBe(Guid.Empty);
        sagaData.IsCompleted.ShouldBeFalse();
    }
}
