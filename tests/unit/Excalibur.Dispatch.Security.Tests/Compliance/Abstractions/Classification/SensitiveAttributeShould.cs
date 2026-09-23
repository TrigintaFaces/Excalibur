// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Classification;

/// <summary>
/// Unit tests for <see cref="SensitiveAttribute"/> and <see cref="SensitiveDataCategory"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Feature", "Classification")]
public sealed class SensitiveAttributeShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var attribute = new SensitiveAttribute();

		// Assert
		attribute.Classification.ShouldBe(DataClassification.Confidential);
		attribute.Category.ShouldBe(SensitiveDataCategory.General);
		attribute.MaskInLogs.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Act
		var attribute = new SensitiveAttribute
		{
			Classification = DataClassification.Restricted,
			Category = SensitiveDataCategory.Credentials,
			MaskInLogs = true
		};

		// Assert
		attribute.Classification.ShouldBe(DataClassification.Restricted);
		attribute.Category.ShouldBe(SensitiveDataCategory.Credentials);
		attribute.MaskInLogs.ShouldBeTrue();
	}

	[Theory]
	[InlineData(SensitiveDataCategory.General)]
	[InlineData(SensitiveDataCategory.Credentials)]
	[InlineData(SensitiveDataCategory.CryptographicMaterial)]
	[InlineData(SensitiveDataCategory.Configuration)]
	[InlineData(SensitiveDataCategory.TradeSecret)]
	[InlineData(SensitiveDataCategory.BusinessMetrics)]
	public void SupportAllSensitiveDataCategories(SensitiveDataCategory category)
	{
		// Act
		var attribute = new SensitiveAttribute { Category = category };

		// Assert
		attribute.Category.ShouldBe(category);
	}

	[Theory]
	[InlineData(SensitiveDataCategory.General, 0)]
	[InlineData(SensitiveDataCategory.Credentials, 1)]
	[InlineData(SensitiveDataCategory.CryptographicMaterial, 2)]
	[InlineData(SensitiveDataCategory.Configuration, 3)]
	[InlineData(SensitiveDataCategory.TradeSecret, 4)]
	[InlineData(SensitiveDataCategory.BusinessMetrics, 5)]
	public void HaveCorrectCategoryValues(SensitiveDataCategory category, int expectedValue)
	{
		// Assert
		((int)category).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData(DataClassification.Public)]
	[InlineData(DataClassification.Internal)]
	[InlineData(DataClassification.Confidential)]
	[InlineData(DataClassification.Restricted)]
	public void SupportAllClassificationLevels(DataClassification classification)
	{
		// Act
		var attribute = new SensitiveAttribute { Classification = classification };

		// Assert
		attribute.Classification.ShouldBe(classification);
	}

	[Fact]
	public void BeApplicableToPropertiesAndFields()
	{
		// Act
		var usage = typeof(SensitiveAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
		usage.AllowMultiple.ShouldBeFalse();
		usage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void NotExposeAnyEncryptionOrErrorSuppressionSurface()
	{
		// The attribute classifies and drives masking. It does not encrypt and it does not filter exception
		// details, so it must not carry a property implying either - a consumer setting one would be relying
		// on protection that does not exist.
		var propertyNames = typeof(SensitiveAttribute)
			.GetProperties()
			.Where(p => p.DeclaringType == typeof(SensitiveAttribute))
			.Select(p => p.Name)
			.ToArray();

		propertyNames.ShouldBe(["Classification", "Category", "MaskInLogs"], ignoreOrder: true);
	}

	[Fact]
	public void Have6SensitiveDataCategories()
	{
		// Act
		var categories = Enum.GetValues<SensitiveDataCategory>();

		// Assert
		categories.Length.ShouldBe(6);
	}

	[Fact]
	public void DefaultToConfidentialClassification()
	{
		// Act
		var attribute = new SensitiveAttribute();

		// Assert - per ADR, sensitive data defaults to Confidential
		attribute.Classification.ShouldBe(DataClassification.Confidential);
	}
}
