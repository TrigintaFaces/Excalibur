using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareRegistrationOptionsShould
{
    [Fact]
    public void HaveEmptyRegistrationsByDefault()
    {
        var options = new MiddlewareRegistrationOptions();

        options.Registrations.ShouldNotBeNull();
        options.Registrations.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowAddingRegistrations()
    {
        var options = new MiddlewareRegistrationOptions();
        options.Registrations.Add(new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.Validation));
        options.Registrations.Add(new MiddlewareRegistration(typeof(string), DispatchMiddlewareStage.Logging));

        options.Registrations.Count.ShouldBe(2);
        options.Registrations[0].MiddlewareType.ShouldBe(typeof(object));
        options.Registrations[1].Stage.ShouldBe(DispatchMiddlewareStage.Logging);
    }
}
