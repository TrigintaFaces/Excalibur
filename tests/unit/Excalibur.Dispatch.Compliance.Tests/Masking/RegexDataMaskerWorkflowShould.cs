namespace Excalibur.Dispatch.Compliance.Tests.Masking;

/// <summary>
/// Tests the regex data masker with complex multi-pattern inputs,
/// MaskObject functionality, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RegexDataMaskerWorkflowShould
{
	private readonly RegexDataMasker _sut = new();

	[Fact]
	public void Mask_email_preserving_first_char_and_domain_first_char()
	{
		var input = "Email: jane.doe@company.org";
		var rules = new MaskingRules
		{
			MaskEmail = true,
			MaskPhone = false,
			MaskSsn = false,
			MaskCardNumber = false,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldNotContain("jane.doe@company.org");
		result.ShouldContain("j***@c***.org");
	}

	[Fact]
	public void Mask_multiple_emails_in_same_input()
	{
		var input = "From: alice@example.com To: bob@test.net";
		var rules = new MaskingRules
		{
			MaskEmail = true,
			MaskPhone = false,
			MaskSsn = false,
			MaskCardNumber = false,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldNotContain("alice@example.com");
		result.ShouldNotContain("bob@test.net");
	}

	[Fact]
	public void Mask_all_pii_types_simultaneously()
	{
		var input = "User john@example.com, SSN 123-45-6789, Phone 555-123-4567, Card 4111-1111-1111-1111, IP 192.168.1.1";

		var result = _sut.MaskAll(input);

		result.ShouldNotContain("john@example.com");
		result.ShouldNotContain("123-45-6789");
		result.ShouldNotContain("555-123-4567");
		result.ShouldNotContain("4111-1111-1111-1111");
	}

	[Fact]
	public void Preserve_non_pii_text()
	{
		var input = "Name: John, Age: 30";
		var rules = MaskingRules.Default;

		var result = _sut.Mask(input, rules);

		result.ShouldContain("Name: John, Age: 30");
	}

	[Fact]
	public void Handle_input_with_no_matching_patterns()
	{
		var input = "This is a plain text message with no PII.";
		var rules = MaskingRules.Strict;

		var result = _sut.Mask(input, rules);

		result.ShouldBe(input);
	}

	[Fact]
	public void Strict_rules_mask_all_patterns()
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
	public void Mask_credit_card_with_continuous_digits()
	{
		var input = "Card: 4111111111111111";
		var rules = new MaskingRules
		{
			MaskEmail = false,
			MaskPhone = false,
			MaskSsn = false,
			MaskCardNumber = true,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldNotContain("4111111111111111");
	}

	[Fact]
	public void Mask_ip_address_preserving_last_octet()
	{
		var input = "Server 10.0.0.42 responded";
		var rules = new MaskingRules
		{
			MaskEmail = false,
			MaskPhone = false,
			MaskSsn = false,
			MaskCardNumber = false,
			MaskIpAddress = true,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldContain("***.***.***.42");
		result.ShouldNotContain("10.0.0.42");
	}

	[Fact]
	public void Mask_date_of_birth_in_various_formats()
	{
		var input = "DOB: 01/15/1985";
		var rules = new MaskingRules
		{
			MaskEmail = false,
			MaskPhone = false,
			MaskSsn = false,
			MaskCardNumber = false,
			MaskDateOfBirth = true,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldNotContain("01/15/1985");
	}

	[Fact]
	public void Use_custom_mask_character_for_all_patterns()
	{
		var input = "SSN: 123-45-6789";
		var rules = new MaskingRules
		{
			MaskSsn = true,
			MaskCharacter = 'X',
			MaskEmail = false,
			MaskPhone = false,
			MaskCardNumber = false,
		};

		var result = _sut.Mask(input, rules);

		result.ShouldContain("XXX-XX-6789");
	}

	[Fact]
	public void Mask_phone_number_with_parentheses()
	{
		var input = "Call (555) 123-4567";
		var rules = new MaskingRules
		{
			MaskEmail = false,
			MaskPhone = true,
			MaskSsn = false,
			MaskCardNumber = false,
		};

		var result = _sut.Mask(input, rules);

		// Should mask the phone, keeping last 4
		result.ShouldContain("4567");
	}

	[Fact]
	public void Return_null_for_null_input()
	{
		var result = _sut.MaskAll(null!);
		result.ShouldBeNull();
	}

	[Fact]
	public void Return_empty_for_empty_input()
	{
		var result = _sut.MaskAll(string.Empty);
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_rules_mask_common_patterns()
	{
		var rules = MaskingRules.Default;

		rules.MaskEmail.ShouldBeTrue();
		rules.MaskPhone.ShouldBeTrue();
		rules.MaskSsn.ShouldBeTrue();
		rules.MaskCardNumber.ShouldBeTrue();
	}
}
