using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareRegistrationDepthShould
{
    [Fact]
    public void CreateWithRequiredParameters()
    {
        var registration = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing);

        registration.MiddlewareType.ShouldBe(typeof(object));
        registration.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
        registration.Order.ShouldBe(100);
        registration.IsEnabled.ShouldBeTrue();
        registration.ConfigureOptions.ShouldBeNull();
    }

    [Fact]
    public void CreateWithCustomOrder()
    {
        var registration = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PostProcessing, order: 50);

        registration.Order.ShouldBe(50);
    }

    [Fact]
    public void CreateWithConfigureOptions()
    {
        Action<IServiceCollection> configure = _ => { };

        var registration = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing, configureOptions: configure);

        registration.ConfigureOptions.ShouldBeSameAs(configure);
    }

    [Fact]
    public void ThrowOnNullMiddlewareType()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MiddlewareRegistration(null!, DispatchMiddlewareStage.PreProcessing));
    }

    [Fact]
    public void AllowDisabling()
    {
        var registration = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing)
        {
            IsEnabled = false,
        };

        registration.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AllowChangingOrder()
    {
        var registration = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing)
        {
            Order = 200,
        };

        registration.Order.ShouldBe(200);
    }

    [Fact]
    public void CreateWithAllStageValues()
    {
        var stages = Enum.GetValues<DispatchMiddlewareStage>();

        foreach (var stage in stages)
        {
            var registration = new MiddlewareRegistration(typeof(object), stage);
            registration.Stage.ShouldBe(stage);
        }
    }
}
