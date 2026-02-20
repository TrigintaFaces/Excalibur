// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="TracingOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of distributed tracing settings.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class TracingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToTrue()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultRecordOperationDetails_ToTrue()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert
		settings.RecordOperationDetails.ShouldBeTrue();
	}

	[Fact]
	public void DefaultRecordRequestResponse_ToFalse()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert
		settings.RecordRequestResponse.ShouldBeFalse();
	}

	[Fact]
	public void DefaultRecordResilienceDetails_ToTrue()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert
		settings.RecordResilienceDetails.ShouldBeTrue();
	}

	[Fact]
	public void DefaultActivitySourceName_ToExcaliburDataElasticSearch()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert
		settings.ActivitySourceName.ShouldBe("Excalibur.Data.ElasticSearch");
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new TracingOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowRecordOperationDetails_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new TracingOptions { RecordOperationDetails = false };

		// Assert
		settings.RecordOperationDetails.ShouldBeFalse();
	}

	[Fact]
	public void AllowRecordRequestResponse_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new TracingOptions { RecordRequestResponse = true };

		// Assert
		settings.RecordRequestResponse.ShouldBeTrue();
	}

	[Fact]
	public void AllowRecordResilienceDetails_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new TracingOptions { RecordResilienceDetails = false };

		// Assert
		settings.RecordResilienceDetails.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomActivitySourceName()
	{
		// Arrange
		const string customName = "MyApp.Elasticsearch";

		// Act
		var settings = new TracingOptions { ActivitySourceName = customName };

		// Assert
		settings.ActivitySourceName.ShouldBe(customName);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new TracingOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new TracingOptions
		{
			Enabled = false,
			RecordOperationDetails = false,
			RecordRequestResponse = true,
			RecordResilienceDetails = false,
			ActivitySourceName = "CustomSource"
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.RecordOperationDetails.ShouldBeFalse();
		settings.RecordRequestResponse.ShouldBeTrue();
		settings.RecordResilienceDetails.ShouldBeFalse();
		settings.ActivitySourceName.ShouldBe("CustomSource");
	}

	#endregion

	#region String Property Edge Cases

	[Fact]
	public void AllowEmptyActivitySourceName()
	{
		// Arrange & Act
		var settings = new TracingOptions { ActivitySourceName = string.Empty };

		// Assert
		settings.ActivitySourceName.ShouldBe(string.Empty);
	}

	[Fact]
	public void ActivitySourceName_MatchesExpectedFormat()
	{
		// Arrange & Act
		var settings = new TracingOptions();

		// Assert - Should follow .NET namespace convention
		settings.ActivitySourceName.ShouldStartWith("Excalibur.");
		settings.ActivitySourceName.ShouldContain("ElasticSearch");
	}

	#endregion
}
