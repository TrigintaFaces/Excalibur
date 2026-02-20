namespace Excalibur.Dispatch.Compliance.Tests.Masking;

public class MaskingRulesShould
{
    [Fact]
    public void Have_default_mask_character_as_asterisk()
    {
        var rules = new MaskingRules();

        rules.MaskCharacter.ShouldBe('*');
    }

    [Fact]
    public void Have_email_and_phone_and_ssn_and_card_enabled_by_default()
    {
        var rules = new MaskingRules();

        rules.MaskEmail.ShouldBeTrue();
        rules.MaskPhone.ShouldBeTrue();
        rules.MaskSsn.ShouldBeTrue();
        rules.MaskCardNumber.ShouldBeTrue();
    }

    [Fact]
    public void Have_ip_and_dob_disabled_by_default()
    {
        var rules = new MaskingRules();

        rules.MaskIpAddress.ShouldBeFalse();
        rules.MaskDateOfBirth.ShouldBeFalse();
    }

    [Fact]
    public void Provide_strict_rules_with_all_patterns_enabled()
    {
        var rules = MaskingRules.Strict;

        rules.MaskEmail.ShouldBeTrue();
        rules.MaskPhone.ShouldBeTrue();
        rules.MaskSsn.ShouldBeTrue();
        rules.MaskCardNumber.ShouldBeTrue();
        rules.MaskIpAddress.ShouldBeTrue();
        rules.MaskDateOfBirth.ShouldBeTrue();
    }

    [Fact]
    public void Provide_pci_dss_rules_with_only_card_enabled()
    {
        var rules = MaskingRules.PciDss;

        rules.MaskEmail.ShouldBeFalse();
        rules.MaskPhone.ShouldBeFalse();
        rules.MaskSsn.ShouldBeFalse();
        rules.MaskCardNumber.ShouldBeTrue();
        rules.MaskIpAddress.ShouldBeFalse();
        rules.MaskDateOfBirth.ShouldBeFalse();
    }

    [Fact]
    public void Provide_hipaa_rules_with_phi_patterns_enabled()
    {
        var rules = MaskingRules.Hipaa;

        rules.MaskEmail.ShouldBeTrue();
        rules.MaskPhone.ShouldBeTrue();
        rules.MaskSsn.ShouldBeTrue();
        rules.MaskCardNumber.ShouldBeFalse();
        rules.MaskIpAddress.ShouldBeTrue();
        rules.MaskDateOfBirth.ShouldBeTrue();
    }
}
