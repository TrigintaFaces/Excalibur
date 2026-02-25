// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="DataStreamTimestampField"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify default timestamp field and customization.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class DataStreamTimestampFieldShould
{
	#region Default Value Tests

	[Fact]
	public void Name_DefaultsToAtTimestamp()
	{
		// Arrange & Act
		var field = new DataStreamTimestampField();

		// Assert
		field.Name.ShouldBe("@timestamp");
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void Name_CanBeCustomized()
	{
		// Arrange & Act
		var field = new DataStreamTimestampField
		{
			Name = "event_timestamp"
		};

		// Assert
		field.Name.ShouldBe("event_timestamp");
	}

	#endregion

	#region Common Timestamp Field Names Tests

	[Theory]
	[InlineData("@timestamp")]
	[InlineData("timestamp")]
	[InlineData("event_time")]
	[InlineData("created_at")]
	[InlineData("occurred_at")]
	public void Name_AcceptsCommonTimestampFieldNames(string fieldName)
	{
		// Arrange & Act
		var field = new DataStreamTimestampField
		{
			Name = fieldName
		};

		// Assert
		field.Name.ShouldBe(fieldName);
	}

	#endregion

	#region Empty String Tests

	[Fact]
	public void Name_CanBeEmptyString()
	{
		// Arrange & Act - Empty string is technically allowed (validation at usage)
		var field = new DataStreamTimestampField
		{
			Name = ""
		};

		// Assert
		field.Name.ShouldBeEmpty();
	}

	#endregion
}
