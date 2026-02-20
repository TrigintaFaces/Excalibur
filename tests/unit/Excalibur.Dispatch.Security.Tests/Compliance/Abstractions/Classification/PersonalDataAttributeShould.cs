// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Classification;

/// <summary>
/// Unit tests for <see cref="PersonalDataAttribute"/>, <see cref="PersonalDataCategory"/>, and <see cref="LegalBasis"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Classification")]
public sealed class PersonalDataAttributeShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var attribute = new PersonalDataAttribute();

		// Assert
		attribute.Category.ShouldBe(PersonalDataCategory.General);
		attribute.IsSensitive.ShouldBeFalse();
		attribute.Purpose.ShouldBeNull();
		attribute.LegalBasis.ShouldBe(LegalBasis.Consent);
		attribute.RetentionDays.ShouldBe(0);
		attribute.MaskInLogs.ShouldBeTrue();
		attribute.ExcludeFromErrors.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Act
		var attribute = new PersonalDataAttribute
		{
			Category = PersonalDataCategory.Health,
			IsSensitive = true,
			Purpose = "Medical service delivery",
			LegalBasis = LegalBasis.VitalInterests,
			RetentionDays = 365 * 10,
			MaskInLogs = true,
			ExcludeFromErrors = true
		};

		// Assert
		attribute.Category.ShouldBe(PersonalDataCategory.Health);
		attribute.IsSensitive.ShouldBeTrue();
		attribute.Purpose.ShouldBe("Medical service delivery");
		attribute.LegalBasis.ShouldBe(LegalBasis.VitalInterests);
		attribute.RetentionDays.ShouldBe(3650);
	}

	[Theory]
	[InlineData(PersonalDataCategory.General)]
	[InlineData(PersonalDataCategory.Identity)]
	[InlineData(PersonalDataCategory.ContactInfo)]
	[InlineData(PersonalDataCategory.Financial)]
	[InlineData(PersonalDataCategory.Health)]
	[InlineData(PersonalDataCategory.Biometric)]
	[InlineData(PersonalDataCategory.Location)]
	[InlineData(PersonalDataCategory.Behavioral)]
	public void SupportAllPersonalDataCategories(PersonalDataCategory category)
	{
		// Act
		var attribute = new PersonalDataAttribute { Category = category };

		// Assert
		attribute.Category.ShouldBe(category);
	}

	[Theory]
	[InlineData(PersonalDataCategory.General, 0)]
	[InlineData(PersonalDataCategory.Identity, 1)]
	[InlineData(PersonalDataCategory.ContactInfo, 2)]
	[InlineData(PersonalDataCategory.Financial, 3)]
	[InlineData(PersonalDataCategory.Health, 4)]
	[InlineData(PersonalDataCategory.Biometric, 5)]
	[InlineData(PersonalDataCategory.Location, 6)]
	[InlineData(PersonalDataCategory.Behavioral, 7)]
	public void HaveCorrectCategoryValues(PersonalDataCategory category, int expectedValue)
	{
		// Assert
		((int)category).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData(LegalBasis.Consent)]
	[InlineData(LegalBasis.Contract)]
	[InlineData(LegalBasis.LegalObligation)]
	[InlineData(LegalBasis.VitalInterests)]
	[InlineData(LegalBasis.PublicInterest)]
	[InlineData(LegalBasis.LegitimateInterests)]
	public void SupportAllLegalBases(LegalBasis basis)
	{
		// Act
		var attribute = new PersonalDataAttribute { LegalBasis = basis };

		// Assert
		attribute.LegalBasis.ShouldBe(basis);
	}

	[Theory]
	[InlineData(LegalBasis.Consent, 0)]
	[InlineData(LegalBasis.Contract, 1)]
	[InlineData(LegalBasis.LegalObligation, 2)]
	[InlineData(LegalBasis.VitalInterests, 3)]
	[InlineData(LegalBasis.PublicInterest, 4)]
	[InlineData(LegalBasis.LegitimateInterests, 5)]
	public void HaveCorrectLegalBasisValues(LegalBasis basis, int expectedValue)
	{
		// Assert
		((int)basis).ShouldBe(expectedValue);
	}

	[Fact]
	public void BeApplicableToPropertiesAndFields()
	{
		// Act
		var usage = typeof(PersonalDataAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
		usage.AllowMultiple.ShouldBeFalse();
		usage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingLoggingMasking()
	{
		// Act - in some cases logs may be secured and masking unnecessary
		var attribute = new PersonalDataAttribute
		{
			MaskInLogs = false,
			ExcludeFromErrors = false
		};

		// Assert
		attribute.MaskInLogs.ShouldBeFalse();
		attribute.ExcludeFromErrors.ShouldBeFalse();
	}

	[Fact]
	public void Have8PersonalDataCategories()
	{
		// Act
		var categories = Enum.GetValues<PersonalDataCategory>();

		// Assert
		categories.Length.ShouldBe(8);
	}

	[Fact]
	public void Have6LegalBases()
	{
		// Act
		var bases = Enum.GetValues<LegalBasis>();

		// Assert
		bases.Length.ShouldBe(6);
	}
}
