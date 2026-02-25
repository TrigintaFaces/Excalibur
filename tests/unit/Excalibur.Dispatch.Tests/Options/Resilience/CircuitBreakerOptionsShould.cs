using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CircuitBreakerOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new CircuitBreakerOptions();

        options.FailureThreshold.ShouldBe(5);
        options.SuccessThreshold.ShouldBe(3);
        options.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.MaxHalfOpenTests.ShouldBe(3);
        options.CircuitKeySelector.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            SuccessThreshold = 5,
            OpenDuration = TimeSpan.FromMinutes(1),
            OperationTimeout = TimeSpan.FromSeconds(10),
            MaxHalfOpenTests = 1,
            CircuitKeySelector = _ => "test-key",
        };

        options.FailureThreshold.ShouldBe(10);
        options.SuccessThreshold.ShouldBe(5);
        options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.MaxHalfOpenTests.ShouldBe(1);
        options.CircuitKeySelector.ShouldNotBeNull();
    }

    [Fact]
    public void InvokeCircuitKeySelector()
    {
        var message = A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>();
        var options = new CircuitBreakerOptions
        {
            CircuitKeySelector = _ => "custom-circuit",
        };

        var key = options.CircuitKeySelector!(message);

        key.ShouldBe("custom-circuit");
    }
}
