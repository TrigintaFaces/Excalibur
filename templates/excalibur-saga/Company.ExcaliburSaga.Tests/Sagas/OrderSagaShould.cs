using Company.ExcaliburSaga.Sagas;
using Shouldly;
using Xunit;

namespace Company.ExcaliburSaga.Tests.Sagas;

public class OrderSagaShould
{
    [Fact]
    public void InitializeWithCorrectDefaults()
    {
        var sagaState = new OrderSagaState();

        sagaState.OrderId.ShouldBe(Guid.Empty);
        sagaState.Completed.ShouldBeFalse();
        sagaState.CompletedSteps.ShouldBeEmpty();
        sagaState.FailureReason.ShouldBeNull();
        sagaState.TrackingNumber.ShouldBeNull();
    }
}
