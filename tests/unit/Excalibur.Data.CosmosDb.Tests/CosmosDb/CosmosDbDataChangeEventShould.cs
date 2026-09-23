// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Cdc.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbDataChangeEvent"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify event creation and properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CosmosDbDataChangeEventShould
{
	#region Default Value Tests

	[Fact]
	public void Position_DefaultsToBeginning()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.Position.ShouldNotBeNull();
	}

	[Fact]
	public void ChangeType_DefaultsToInsert()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.ChangeType.ShouldBe(CosmosDbDataChangeType.Insert);
	}

	[Fact]
	public void DocumentId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.DocumentId.ShouldBe(string.Empty);
	}

	[Fact]
	public void PartitionKey_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.PartitionKey.ShouldBeNull();
		evt.PartitionKeyKind.ShouldBe(
			CosmosDbPartitionKeyKind.None,
			"an event with no partition key must say so, not claim an explicit JSON null.");
	}

	[Fact]
	public void PartitionKeyKind_RoundTripsThroughTheFactories()
	{
		// The kind is what separates the number 42 from the string "42" — two different Cosmos partitions
		// that render to the same text. A factory that dropped it would merge them.
		var position = CosmosDbCdcPosition.Beginning();
		using var document = JsonDocument.Parse("{}");
		var timestamp = DateTimeOffset.UtcNow;

		CosmosDbDataChangeEvent
			.CreateInsert(position, "d1", "42", document, timestamp, 1, null, CosmosDbPartitionKeyKind.Number)
			.PartitionKeyKind.ShouldBe(CosmosDbPartitionKeyKind.Number);

		CosmosDbDataChangeEvent
			.CreateUpdate(position, "d1", "42", document, null, timestamp, 1, null, CosmosDbPartitionKeyKind.String)
			.PartitionKeyKind.ShouldBe(CosmosDbPartitionKeyKind.String);

		CosmosDbDataChangeEvent
			.CreateDelete(position, "d1", "true", null, timestamp, 1, CosmosDbPartitionKeyKind.Boolean)
			.PartitionKeyKind.ShouldBe(CosmosDbPartitionKeyKind.Boolean);
	}

	[Fact]
	public void Document_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.Document.ShouldBeNull();
	}

	[Fact]
	public void PreviousDocument_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.PreviousDocument.ShouldBeNull();
	}

	[Fact]
	public void ETag_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent();

		// Assert
		evt.ETag.ShouldBeNull();
	}

	#endregion

	#region CreateInsert Factory Tests

	[Fact]
	public void CreateInsert_SetsCorrectChangeType()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var document = JsonDocument.Parse("{}");

		// Act
		var evt = CosmosDbDataChangeEvent.CreateInsert(
			position,
			"doc-1",
			"partition-1",
			document,
			DateTimeOffset.UtcNow,
			123,
			"etag-1");

		// Assert
		evt.ChangeType.ShouldBe(CosmosDbDataChangeType.Insert);
	}

	[Fact]
	public void CreateInsert_SetsAllProperties()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var document = JsonDocument.Parse("{}");
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = CosmosDbDataChangeEvent.CreateInsert(
			position,
			"doc-1",
			"partition-1",
			document,
			timestamp,
			123,
			"etag-1");

		// Assert
		evt.DocumentId.ShouldBe("doc-1");
		evt.PartitionKey.ShouldBe("partition-1");
		evt.Document.ShouldNotBeNull();
		evt.Timestamp.ShouldBe(timestamp);
		evt.Lsn.ShouldBe(123);
		evt.ETag.ShouldBe("etag-1");
	}

	#endregion

	#region CreateUpdate Factory Tests

	[Fact]
	public void CreateUpdate_SetsCorrectChangeType()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var document = JsonDocument.Parse("{}");
		using var previousDocument = JsonDocument.Parse("{}");

		// Act
		var evt = CosmosDbDataChangeEvent.CreateUpdate(
			position,
			"doc-1",
			"partition-1",
			document,
			previousDocument,
			DateTimeOffset.UtcNow,
			124,
			"etag-2");

		// Assert
		evt.ChangeType.ShouldBe(CosmosDbDataChangeType.Update);
	}

	[Fact]
	public void CreateUpdate_IncludesPreviousDocument()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var document = JsonDocument.Parse("{}");
		using var previousDocument = JsonDocument.Parse("{\"old\":true}");

		// Act
		var evt = CosmosDbDataChangeEvent.CreateUpdate(
			position,
			"doc-1",
			"partition-1",
			document,
			previousDocument,
			DateTimeOffset.UtcNow,
			124,
			"etag-2");

		// Assert
		evt.PreviousDocument.ShouldNotBeNull();
	}

	#endregion

	#region CreateDelete Factory Tests

	[Fact]
	public void CreateDelete_SetsCorrectChangeType()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var previousDocument = JsonDocument.Parse("{}");

		// Act
		var evt = CosmosDbDataChangeEvent.CreateDelete(
			position,
			"doc-1",
			"partition-1",
			previousDocument,
			DateTimeOffset.UtcNow,
			125);

		// Assert
		evt.ChangeType.ShouldBe(CosmosDbDataChangeType.Delete);
	}

	[Fact]
	public void CreateDelete_HasNoCurrentDocument()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		using var previousDocument = JsonDocument.Parse("{}");

		// Act
		var evt = CosmosDbDataChangeEvent.CreateDelete(
			position,
			"doc-1",
			"partition-1",
			previousDocument,
			DateTimeOffset.UtcNow,
			125);

		// Assert
		evt.Document.ShouldBeNull();
		evt.ETag.ShouldBeNull();
	}

	[Fact]
	public void CreateDelete_CanHaveNullPreviousDocument()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();

		// Act
		var evt = CosmosDbDataChangeEvent.CreateDelete(
			position,
			"doc-1",
			"partition-1",
			null,
			DateTimeOffset.UtcNow,
			125);

		// Assert
		evt.PreviousDocument.ShouldBeNull();
	}

	#endregion

	#region Lsn Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void Lsn_AcceptsVariousValues(long lsn)
	{
		// Arrange & Act
		var evt = new CosmosDbDataChangeEvent { Lsn = lsn };

		// Assert
		evt.Lsn.ShouldBe(lsn);
	}

	#endregion
}
