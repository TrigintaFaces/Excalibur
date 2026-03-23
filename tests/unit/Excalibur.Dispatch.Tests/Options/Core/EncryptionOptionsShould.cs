using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Options.Core;

using EncryptionOptions = Excalibur.Dispatch.Options.Core.EncryptionOptions;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EncryptionOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new EncryptionOptions();

        options.Enabled.ShouldBeFalse();
        options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
        options.Key.ShouldBeNull();
        options.KeyDerivation.ShouldBeNull();
        options.EnableKeyRotation.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var kdOptions = new KeyDerivationOptions();

        var options = new EncryptionOptions
        {
            Enabled = true,
            Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
            Key = key,
            KeyDerivation = kdOptions,
            EnableKeyRotation = true,
        };

        options.Enabled.ShouldBeTrue();
        options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
        options.Key.ShouldBeSameAs(key);
        options.KeyDerivation.ShouldBeSameAs(kdOptions);
        options.EnableKeyRotation.ShouldBeTrue();
    }

    [Fact]
    public void SupportAlternateAlgorithm()
    {
        var options = new EncryptionOptions
        {
            Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
        };

        options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
    }
}
