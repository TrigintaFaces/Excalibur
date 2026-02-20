using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProviderCircuitStateShould
{
    [Fact]
    public void DefineClosedState()
    {
        // Assert
        DataProviderCircuitState.Closed.ShouldBe((DataProviderCircuitState)0);
    }

    [Fact]
    public void DefineOpenState()
    {
        // Assert
        DataProviderCircuitState.Open.ShouldBe((DataProviderCircuitState)1);
    }

    [Fact]
    public void DefineHalfOpenState()
    {
        // Assert
        DataProviderCircuitState.HalfOpen.ShouldBe((DataProviderCircuitState)2);
    }

    [Fact]
    public void HaveExactlyThreeValues()
    {
        // Assert
        Enum.GetValues<DataProviderCircuitState>().Length.ShouldBe(3);
    }

    [Theory]
    [InlineData(DataProviderCircuitState.Closed, "Closed")]
    [InlineData(DataProviderCircuitState.Open, "Open")]
    [InlineData(DataProviderCircuitState.HalfOpen, "HalfOpen")]
    public void ConvertToStringCorrectly(DataProviderCircuitState state, string expected)
    {
        // Assert
        state.ToString().ShouldBe(expected);
    }
}
