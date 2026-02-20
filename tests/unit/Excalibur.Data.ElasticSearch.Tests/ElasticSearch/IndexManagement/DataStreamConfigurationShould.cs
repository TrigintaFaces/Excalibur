// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="DataStreamConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify data stream configuration properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class DataStreamConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void TimestampField_DefaultsToNull()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration();

		// Assert
		config.TimestampField.ShouldBeNull();
	}

	[Fact]
	public void Hidden_DefaultsToNull()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration();

		// Assert
		config.Hidden.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomRouting_DefaultsToNull()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration();

		// Assert
		config.AllowCustomRouting.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration
		{
			TimestampField = new DataStreamTimestampField { Name = "@timestamp" },
			Hidden = false,
			AllowCustomRouting = true
		};

		// Assert
		config.TimestampField.ShouldNotBeNull();
		config.TimestampField.Name.ShouldBe("@timestamp");
		config.Hidden.ShouldBe(false);
		config.AllowCustomRouting.ShouldBe(true);
	}

	#endregion

	#region Hidden Data Stream Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Hidden_CanBeSetExplicitly(bool isHidden)
	{
		// Arrange & Act
		var config = new DataStreamConfiguration
		{
			Hidden = isHidden
		};

		// Assert
		config.Hidden.ShouldBe(isHidden);
	}

	#endregion

	#region Custom Routing Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowCustomRouting_CanBeSetExplicitly(bool allowRouting)
	{
		// Arrange & Act
		var config = new DataStreamConfiguration
		{
			AllowCustomRouting = allowRouting
		};

		// Assert
		config.AllowCustomRouting.ShouldBe(allowRouting);
	}

	#endregion

	#region Timestamp Field Configuration Tests

	[Fact]
	public void TimestampField_CanHaveCustomName()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration
		{
			TimestampField = new DataStreamTimestampField
			{
				Name = "event_timestamp"
			}
		};

		// Assert
		config.TimestampField.Name.ShouldBe("event_timestamp");
	}

	[Fact]
	public void TimestampField_UsesDefaultNameWhenNotSpecified()
	{
		// Arrange & Act
		var config = new DataStreamConfiguration
		{
			TimestampField = new DataStreamTimestampField()
		};

		// Assert
		config.TimestampField.Name.ShouldBe("@timestamp");
	}

	#endregion
}
