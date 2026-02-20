// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Masking;

/// <summary>
/// Unit tests for <see cref="RegexDataMasker"/>.
/// </summary>
[UnitTest]
[Trait("Category", "Unit")]
[Trait("Component", "Masking")]
public sealed class RegexDataMaskerShould
{
	private readonly RegexDataMasker _masker = new();

	#region Email Masking (MASK-001)

	[Theory]
	[InlineData("john@example.com", "j***@e***.com")]
	[InlineData("user@domain.org", "u***@d***.org")]
	public void Mask_Email_AddressesWith_PartialRedaction(string input, string expected)
	{
		// Arrange
		var rules = new MaskingRules { MaskEmail = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Mask_Email_InText()
	{
		// Arrange
		var input = "Contact john@example.com for more info";
		var rules = new MaskingRules { MaskEmail = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("j***@e***.com");
		result.ShouldContain("Contact");
	}

	[Fact]
	public void Not_Mask_Email_WhenDisabled()
	{
		// Arrange
		var input = "john@example.com";
		var rules = new MaskingRules { MaskEmail = false };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe(input);
	}

	#endregion Email Masking (MASK-001)

	#region Phone Masking (MASK-001)

	[Theory]
	[InlineData("555-123-4567")]
	[InlineData("555.123.4567")]
	[InlineData("5551234567")]
	public void Mask_PhoneNumbers_KeepingLast4(string input)
	{
		// Arrange
		var rules = new MaskingRules { MaskPhone = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldEndWith("4567");
		result.ShouldContain('*');
	}

	[Fact]
	public void Mask_Phone_InText()
	{
		// Arrange
		var input = "Call me at 555-123-4567 today";
		var rules = new MaskingRules { MaskPhone = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("4567");
		result.ShouldContain("Call me at");
		result.ShouldContain("***");
	}

	#endregion Phone Masking (MASK-001)

	#region SSN Masking (MASK-001)

	[Fact]
	public void Mask_Ssn_KeepingLast4()
	{
		// Arrange
		var input = "123-45-6789";
		var rules = new MaskingRules { MaskSsn = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe("***-**-6789");
	}

	[Fact]
	public void Mask_Ssn_InText()
	{
		// Arrange
		var input = "SSN: 123-45-6789 on file";
		var rules = new MaskingRules { MaskSsn = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("***-**-6789");
		result.ShouldContain("SSN:");
	}

	#endregion SSN Masking (MASK-001)

	#region Credit Card Masking (MASK-004 PCI-DSS)

	[Theory]
	[InlineData("4111-1111-1111-1111", "****-****-****-1111")]
	[InlineData("4111 1111 1111 1111", "**** **** **** 1111")]
	[InlineData("4111111111111111", "************1111")]
	public void Mask_CreditCardNumber_KeepingLast4(string input, string expected)
	{
		// Arrange
		var rules = new MaskingRules { MaskCardNumber = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Mask_CreditCard_InText()
	{
		// Arrange
		var input = "Charged to card 4111-1111-1111-1111 on 12/25";
		var rules = MaskingRules.PciDss;

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("****-****-****-1111");
		result.ShouldContain("Charged to card");
	}

	#endregion Credit Card Masking (MASK-004 PCI-DSS)

	#region IP Address Masking (PHI/MASK-002)

	[Fact]
	public void Mask_IpAddress_KeepingLastOctet()
	{
		// Arrange
		var input = "192.168.1.100";
		var rules = new MaskingRules { MaskIpAddress = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe("***.***.***.100");
	}

	[Fact]
	public void Not_Mask_IpAddress_ByDefault()
	{
		// Arrange
		var input = "192.168.1.100";
		var rules = MaskingRules.Default;

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe(input);
	}

	#endregion IP Address Masking (PHI/MASK-002)

	#region Date of Birth Masking (MASK-002)

	[Theory]
	[InlineData("12/25/1990", "**/**/****")]
	[InlineData("01-15-2000", "**-**-****")]
	public void Mask_DateOfBirth_Completely(string input, string expected)
	{
		// Arrange
		var rules = new MaskingRules { MaskDateOfBirth = true };

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion Date of Birth Masking (MASK-002)

	#region Preset Rules

	[Fact]
	public void PciDss_Rules_OnlyMaskCardNumbers()
	{
		// Arrange
		var rules = MaskingRules.PciDss;
		var input = "Card: 4111-1111-1111-1111, Email: john@example.com";

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("****-****-****-1111");
		result.ShouldContain("john@example.com"); // Email not masked
	}

	[Fact]
	public void Hipaa_Rules_MaskPhiPatterns()
	{
		// Arrange
		var rules = MaskingRules.Hipaa;
		var input = "Patient: john@example.com, SSN: 123-45-6789, IP: 192.168.1.100";

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("j***@e***.com");
		result.ShouldContain("***-**-6789");
		result.ShouldContain("***.***.***.100");
	}

	[Fact]
	public void Strict_Rules_MaskAllPatterns()
	{
		// Arrange
		var rules = MaskingRules.Strict;
		var input = "Email: john@example.com, Card: 4111-1111-1111-1111, DOB: 12/25/1990";

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("j***@e***.com");
		result.ShouldContain("****-****-****-1111");
		result.ShouldContain("**/**/****");
	}

	#endregion Preset Rules

	#region Edge Cases

	[Fact]
	public void Handle_NullInput()
	{
		// Arrange
		string? input = null;

		// Act
		var result = _masker.Mask(input, MaskingRules.Default);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Handle_EmptyInput()
	{
		// Arrange
		var input = string.Empty;

		// Act
		var result = _masker.Mask(input, MaskingRules.Default);

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void Handle_InputWithNoPatterns()
	{
		// Arrange
		var input = "Just some regular text without sensitive data";

		// Act
		var result = _masker.MaskAll(input);

		// Assert
		result.ShouldBe(input);
	}

	[Fact]
	public void Handle_MultiplePatterns_InSameString()
	{
		// Arrange
		var input = "Email: john@example.com, Phone: 555-123-4567, Card: 4111-1111-1111-1111";
		var rules = MaskingRules.Default;

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldContain("j***@e***.com");
		result.ShouldContain("4567");
		result.ShouldContain("****-****-****-1111");
	}

	#endregion Edge Cases

	#region MaskAll

	[Fact]
	public void MaskAll_UsesDefaultRules()
	{
		// Arrange
		var input = "Contact john@example.com at 555-123-4567";

		// Act
		var result = _masker.MaskAll(input);

		// Assert
		result.ShouldContain("j***@e***.com");
		result.ShouldContain("4567");
	}

	#endregion MaskAll

	#region Object Masking

	[Fact]
	public void MaskObject_ReturnsNewInstance()
	{
		// Arrange
		var obj = new TestPerson
		{
			Name = "John Doe",
			Email = "john@example.com",
			Phone = "555-123-4567"
		};

		// Act
		var result = _masker.MaskObject(obj);

		// Assert
		result.ShouldNotBeSameAs(obj);
	}

	[Fact]
	public void MaskObject_MasksStringProperties()
	{
		// Arrange
		var obj = new TestPerson
		{
			Name = "John Doe",
			Email = "john@example.com",
			Phone = "555-123-4567"
		};

		// Act
		var result = _masker.MaskObject(obj);

		// Assert
		result.Email.ShouldContain("***");
		result.Phone.ShouldEndWith("4567");
	}

	private sealed class TestPerson
	{
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Phone { get; set; } = string.Empty;
	}

	#endregion Object Masking

	#region Custom Mask Character

	[Fact]
	public void Use_CustomMaskCharacter()
	{
		// Arrange
		var rules = new MaskingRules { MaskSsn = true, MaskCharacter = 'X' };
		var input = "123-45-6789";

		// Act
		var result = _masker.Mask(input, rules);

		// Assert
		result.ShouldBe("XXX-XX-6789");
	}

	#endregion Custom Mask Character
}
