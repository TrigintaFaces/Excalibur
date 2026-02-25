using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchProfileOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new DispatchProfileOptions();

        options.ProfileName.ShouldBe(string.Empty);
    }

    [Fact]
    public void AllowSettingProfileName()
    {
        var options = new DispatchProfileOptions
        {
            ProfileName = "production",
        };

        options.ProfileName.ShouldBe("production");
    }
}
