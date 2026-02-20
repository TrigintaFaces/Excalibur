namespace Excalibur.Dispatch.Compliance.Tests.Masking;

public class RegexDataMaskerShould
{
    private readonly RegexDataMasker _sut = new();

    [Fact]
    public void Return_empty_string_when_input_is_empty()
    {
        var result = _sut.Mask(string.Empty, MaskingRules.Default);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Return_null_when_input_is_null()
    {
        var result = _sut.Mask(null!, MaskingRules.Default);

        result.ShouldBeNull();
    }

    [Fact]
    public void Throw_when_rules_are_null()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Mask("test", null!));
    }

    [Fact]
    public void Mask_email_addresses()
    {
        var input = "Contact us at john.doe@example.com for support.";
        var rules = new MaskingRules { MaskEmail = true, MaskPhone = false, MaskSsn = false, MaskCardNumber = false };

        var result = _sut.Mask(input, rules);

        result.ShouldNotContain("john.doe@example.com");
        result.ShouldContain("j***@e***.com");
    }

    [Fact]
    public void Mask_phone_numbers_keeping_last_four_digits()
    {
        var input = "Call 555-123-4567 for details.";
        var rules = new MaskingRules { MaskEmail = false, MaskPhone = true, MaskSsn = false, MaskCardNumber = false };

        var result = _sut.Mask(input, rules);

        result.ShouldNotContain("555-123-4567");
        result.ShouldContain("4567");
    }

    [Fact]
    public void Mask_ssn_keeping_last_four_digits()
    {
        var input = "SSN: 123-45-6789";
        var rules = new MaskingRules { MaskEmail = false, MaskPhone = false, MaskSsn = true, MaskCardNumber = false };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("***-**-6789");
        result.ShouldNotContain("123-45");
    }

    [Fact]
    public void Mask_credit_card_numbers_with_dashes()
    {
        var input = "Card: 4111-1111-1111-1111";
        var rules = new MaskingRules { MaskEmail = false, MaskPhone = false, MaskSsn = false, MaskCardNumber = true };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("1111");
        result.ShouldNotContain("4111-1111-1111-1111");
    }

    [Fact]
    public void Mask_credit_card_numbers_with_spaces()
    {
        var input = "Card: 4111 1111 1111 1111";
        var rules = new MaskingRules { MaskEmail = false, MaskPhone = false, MaskSsn = false, MaskCardNumber = true };

        var result = _sut.Mask(input, rules);

        result.ShouldNotContain("4111 1111 1111 1111");
    }

    [Fact]
    public void Mask_ip_addresses_keeping_last_octet()
    {
        var input = "Request from 192.168.1.100";
        var rules = new MaskingRules
        {
            MaskEmail = false, MaskPhone = false, MaskSsn = false,
            MaskCardNumber = false, MaskIpAddress = true
        };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("***.***.***.100");
        result.ShouldNotContain("192.168.1.100");
    }

    [Fact]
    public void Mask_dates_of_birth()
    {
        var input = "DOB: 12/25/1990";
        var rules = new MaskingRules
        {
            MaskEmail = false, MaskPhone = false, MaskSsn = false,
            MaskCardNumber = false, MaskDateOfBirth = true
        };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("**/**");
        result.ShouldNotContain("12/25/1990");
    }

    [Fact]
    public void Not_mask_patterns_when_disabled_in_rules()
    {
        var input = "Email: user@test.com SSN: 123-45-6789";
        var rules = new MaskingRules { MaskEmail = false, MaskSsn = false, MaskPhone = false, MaskCardNumber = false };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("user@test.com");
        result.ShouldContain("123-45-6789");
    }

    [Fact]
    public void Use_custom_mask_character()
    {
        var input = "SSN: 123-45-6789";
        var rules = new MaskingRules { MaskSsn = true, MaskCharacter = '#', MaskEmail = false, MaskPhone = false, MaskCardNumber = false };

        var result = _sut.Mask(input, rules);

        result.ShouldContain("###-##-6789");
    }

    [Fact]
    public void MaskAll_uses_default_rules()
    {
        var input = "Email: user@test.com, SSN: 123-45-6789";

        var result = _sut.MaskAll(input);

        result.ShouldNotContain("user@test.com");
        result.ShouldNotContain("123-45-6789");
    }

    [Fact]
    public void Mask_multiple_patterns_in_same_input()
    {
        var input = "Name: John, Email: john@example.com, SSN: 123-45-6789, Phone: 555-123-4567";
        var rules = MaskingRules.Strict;

        var result = _sut.Mask(input, rules);

        result.ShouldNotContain("john@example.com");
        result.ShouldNotContain("123-45-6789");
    }

    [Fact]
    public void Construct_with_default_rules()
    {
        var masker = new RegexDataMasker();

        var result = masker.MaskAll("test@example.com");

        result.ShouldNotContain("test@example.com");
    }

    [Fact]
    public void Construct_with_null_rules_falls_back_to_default()
    {
        var masker = new RegexDataMasker(null!);

        var result = masker.MaskAll("test@example.com");

        result.ShouldNotContain("test@example.com");
    }
}
