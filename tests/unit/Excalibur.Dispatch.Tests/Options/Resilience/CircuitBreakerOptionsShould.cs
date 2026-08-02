using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CircuitBreakerOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new CircuitBreakerOptions();

        options.FailureThreshold.ShouldBe(5);
        options.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.CircuitKeySelector.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromMinutes(1),
            OperationTimeout = TimeSpan.FromSeconds(10),
            CircuitKeySelector = _ => "test-key",
        };

        options.FailureThreshold.ShouldBe(10);
        options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.CircuitKeySelector.ShouldNotBeNull();
    }

    [Fact]
    public void InvokeCircuitKeySelector()
    {
        var message = A.Fake<Excalibur.Dispatch.IDispatchMessage>();
        var options = new CircuitBreakerOptions
        {
            CircuitKeySelector = _ => "custom-circuit",
        };

        var key = options.CircuitKeySelector!(message);

        key.ShouldBe("custom-circuit");
    }
}
