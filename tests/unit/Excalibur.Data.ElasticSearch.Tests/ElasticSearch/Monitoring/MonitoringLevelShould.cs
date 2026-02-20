// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="MonitoringLevel"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for monitoring verbosity levels.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class MonitoringLevelShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineMinimalAsZero()
	{
		// Assert
		((int)MonitoringLevel.Minimal).ShouldBe(0);
	}

	[Fact]
	public void DefineStandardAsOne()
	{
		// Assert
		((int)MonitoringLevel.Standard).ShouldBe(1);
	}

	[Fact]
	public void DefineVerboseAsTwo()
	{
		// Assert
		((int)MonitoringLevel.Verbose).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<MonitoringLevel>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedLevels()
	{
		// Act
		var values = Enum.GetValues<MonitoringLevel>();

		// Assert
		values.ShouldContain(MonitoringLevel.Minimal);
		values.ShouldContain(MonitoringLevel.Standard);
		values.ShouldContain(MonitoringLevel.Verbose);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForMinimal()
	{
		// Assert
		MonitoringLevel.Minimal.ToString().ShouldBe("Minimal");
	}

	[Fact]
	public void HaveCorrectNameForStandard()
	{
		// Assert
		MonitoringLevel.Standard.ToString().ShouldBe("Standard");
	}

	[Fact]
	public void HaveCorrectNameForVerbose()
	{
		// Assert
		MonitoringLevel.Verbose.ToString().ShouldBe("Verbose");
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Minimal", MonitoringLevel.Minimal)]
	[InlineData("Standard", MonitoringLevel.Standard)]
	[InlineData("Verbose", MonitoringLevel.Verbose)]
	public void ParseFromString_WithValidName(string name, MonitoringLevel expected)
	{
		// Act
		var result = Enum.Parse<MonitoringLevel>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("minimal", MonitoringLevel.Minimal)]
	[InlineData("STANDARD", MonitoringLevel.Standard)]
	[InlineData("verbose", MonitoringLevel.Verbose)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, MonitoringLevel expected)
	{
		// Act
		var result = Enum.Parse<MonitoringLevel>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowArgumentException_WhenParsingInvalidName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			Enum.Parse<MonitoringLevel>("InvalidLevel"));
	}

	#endregion

	#region Enum Conversion Tests

	[Theory]
	[InlineData(0, MonitoringLevel.Minimal)]
	[InlineData(1, MonitoringLevel.Standard)]
	[InlineData(2, MonitoringLevel.Verbose)]
	public void ConvertFromInt_ToMonitoringLevel(int value, MonitoringLevel expected)
	{
		// Act
		var result = (MonitoringLevel)value;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum IsDefined Tests

	[Theory]
	[InlineData(MonitoringLevel.Minimal, true)]
	[InlineData(MonitoringLevel.Standard, true)]
	[InlineData(MonitoringLevel.Verbose, true)]
	public void ReturnTrue_ForDefinedValues(MonitoringLevel level, bool expected)
	{
		// Act
		var isDefined = Enum.IsDefined(level);

		// Assert
		isDefined.ShouldBe(expected);
	}

	[Fact]
	public void ReturnFalse_ForUndefinedValue()
	{
		// Act
		var isDefined = Enum.IsDefined((MonitoringLevel)999);

		// Assert
		isDefined.ShouldBeFalse();
	}

	#endregion
}
