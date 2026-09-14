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

        options.ConsecutiveFailureThreshold.ShouldBe(5);
        options.MinimumThroughput.ShouldBe(5);
        options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.CircuitKeySelector.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailureThreshold = 10,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromMinutes(1),
            OperationTimeout = TimeSpan.FromSeconds(10),
            CircuitKeySelector = _ => "test-key",
        };

        options.ConsecutiveFailureThreshold.ShouldBe(10);
        options.MinimumThroughput.ShouldBe(10);
        options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(1));
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
