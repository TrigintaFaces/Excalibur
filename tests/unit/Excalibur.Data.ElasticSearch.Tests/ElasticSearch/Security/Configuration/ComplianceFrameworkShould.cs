// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

/// <summary>
/// Unit tests for the <see cref="ComplianceFramework"/> enum.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.2): Elasticsearch compliance unit tests.
/// Tests verify enum values for GDPR/HIPAA/PCI-DSS/SOX/ISO27001/NIST-CSF/FISMA compliance frameworks.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class ComplianceFrameworkShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineGdprAsZero()
	{
		// Assert
		((int)ComplianceFramework.Gdpr).ShouldBe(0);
	}

	[Fact]
	public void DefineHipaaAsOne()
	{
		// Assert
		((int)ComplianceFramework.Hipaa).ShouldBe(1);
	}

	[Fact]
	public void DefineSoxAsTwo()
	{
		// Assert
		((int)ComplianceFramework.Sox).ShouldBe(2);
	}

	[Fact]
	public void DefinePciDssAsThree()
	{
		// Assert
		((int)ComplianceFramework.PciDss).ShouldBe(3);
	}

	[Fact]
	public void DefineIso27001AsFour()
	{
		// Assert
		((int)ComplianceFramework.Iso27001).ShouldBe(4);
	}

	[Fact]
	public void DefineNistCsfAsFive()
	{
		// Assert
		((int)ComplianceFramework.NistCsf).ShouldBe(5);
	}

	[Fact]
	public void DefineFismaAsSix()
	{
		// Assert
		((int)ComplianceFramework.Fisma).ShouldBe(6);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveSevenDefinedValues()
	{
		// Act
		var values = Enum.GetValues<ComplianceFramework>();

		// Assert
		values.Length.ShouldBe(7);
	}

	[Fact]
	public void ContainAllExpectedFrameworks()
	{
		// Act
		var values = Enum.GetValues<ComplianceFramework>();

		// Assert
		values.ShouldContain(ComplianceFramework.Gdpr);
		values.ShouldContain(ComplianceFramework.Hipaa);
		values.ShouldContain(ComplianceFramework.Sox);
		values.ShouldContain(ComplianceFramework.PciDss);
		values.ShouldContain(ComplianceFramework.Iso27001);
		values.ShouldContain(ComplianceFramework.NistCsf);
		values.ShouldContain(ComplianceFramework.Fisma);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForGdpr()
	{
		// Assert
		ComplianceFramework.Gdpr.ToString().ShouldBe("Gdpr");
	}

	[Fact]
	public void HaveCorrectNameForHipaa()
	{
		// Assert
		ComplianceFramework.Hipaa.ToString().ShouldBe("Hipaa");
	}

	[Fact]
	public void HaveCorrectNameForSox()
	{
		// Assert
		ComplianceFramework.Sox.ToString().ShouldBe("Sox");
	}

	[Fact]
	public void HaveCorrectNameForPciDss()
	{
		// Assert
		ComplianceFramework.PciDss.ToString().ShouldBe("PciDss");
	}

	[Fact]
	public void HaveCorrectNameForIso27001()
	{
		// Assert
		ComplianceFramework.Iso27001.ToString().ShouldBe("Iso27001");
	}

	[Fact]
	public void HaveCorrectNameForNistCsf()
	{
		// Assert
		ComplianceFramework.NistCsf.ToString().ShouldBe("NistCsf");
	}

	[Fact]
	public void HaveCorrectNameForFisma()
	{
		// Assert
		ComplianceFramework.Fisma.ToString().ShouldBe("Fisma");
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Gdpr", ComplianceFramework.Gdpr)]
	[InlineData("Hipaa", ComplianceFramework.Hipaa)]
	[InlineData("Sox", ComplianceFramework.Sox)]
	[InlineData("PciDss", ComplianceFramework.PciDss)]
	[InlineData("Iso27001", ComplianceFramework.Iso27001)]
	[InlineData("NistCsf", ComplianceFramework.NistCsf)]
	[InlineData("Fisma", ComplianceFramework.Fisma)]
	public void ParseFromString_WithValidName(string name, ComplianceFramework expected)
	{
		// Act
		var result = Enum.Parse<ComplianceFramework>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("gdpr", ComplianceFramework.Gdpr)]
	[InlineData("GDPR", ComplianceFramework.Gdpr)]
	[InlineData("hipaa", ComplianceFramework.Hipaa)]
	[InlineData("HIPAA", ComplianceFramework.Hipaa)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, ComplianceFramework expected)
	{
		// Act
		var result = Enum.Parse<ComplianceFramework>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowArgumentException_WhenParsingInvalidName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			Enum.Parse<ComplianceFramework>("InvalidFramework"));
	}

	#endregion

	#region Enum Conversion Tests

	[Theory]
	[InlineData(0, ComplianceFramework.Gdpr)]
	[InlineData(1, ComplianceFramework.Hipaa)]
	[InlineData(2, ComplianceFramework.Sox)]
	[InlineData(3, ComplianceFramework.PciDss)]
	[InlineData(4, ComplianceFramework.Iso27001)]
	[InlineData(5, ComplianceFramework.NistCsf)]
	[InlineData(6, ComplianceFramework.Fisma)]
	public void ConvertFromInt_ToComplianceFramework(int value, ComplianceFramework expected)
	{
		// Act
		var result = (ComplianceFramework)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void AllowInvalidIntConversion_WithoutThrowingException()
	{
		// Note: C# enums allow invalid int values without throwing
		// This test documents this behavior
		var invalidValue = (ComplianceFramework)999;

		// Assert - Should not throw, value is 999
		((int)invalidValue).ShouldBe(999);
	}

	#endregion

	#region Enum IsDefined Tests

	[Theory]
	[InlineData(ComplianceFramework.Gdpr, true)]
	[InlineData(ComplianceFramework.Hipaa, true)]
	[InlineData(ComplianceFramework.Sox, true)]
	[InlineData(ComplianceFramework.PciDss, true)]
	[InlineData(ComplianceFramework.Iso27001, true)]
	[InlineData(ComplianceFramework.NistCsf, true)]
	[InlineData(ComplianceFramework.Fisma, true)]
	public void ReturnTrue_ForDefinedValues(ComplianceFramework framework, bool expected)
	{
		// Act
		var isDefined = Enum.IsDefined(framework);

		// Assert
		isDefined.ShouldBe(expected);
	}

	[Fact]
	public void ReturnFalse_ForUndefinedValue()
	{
		// Act
		var isDefined = Enum.IsDefined((ComplianceFramework)999);

		// Assert
		isDefined.ShouldBeFalse();
	}

	#endregion
}
