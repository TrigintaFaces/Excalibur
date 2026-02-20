using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KeyDerivationOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new KeyDerivationOptions();

        options.Password.ShouldBeNull();
        options.Salt.ShouldBeNull();
        options.Iterations.ShouldBe(100_000);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var salt = new byte[] { 1, 2, 3, 4 };

        var options = new KeyDerivationOptions
        {
            Password = "secret",
            Salt = salt,
            Iterations = 200_000,
        };

        options.Password.ShouldBe("secret");
        options.Salt.ShouldBeSameAs(salt);
        options.Iterations.ShouldBe(200_000);
    }

    [Fact]
    public void AllowNullPassword()
    {
        var options = new KeyDerivationOptions
        {
            Password = null,
        };

        options.Password.ShouldBeNull();
    }
}
