// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreDataChangeEvent"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify data change event creation and properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreDataChangeEventShould
{
	private const string TestCollectionPath = "test-collection";
	private const string TestDocumentId = "doc-123";

	#region CreateAdded Factory Method Tests

	[Fact]
	public void CreateAdded_SetsChangeTypeToAdded()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.ChangeType.ShouldBe(FirestoreDataChangeType.Added);
	}

	[Fact]
	public void CreateAdded_SetsPosition()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.Position.ShouldBe(position);
	}

	[Fact]
	public void CreateAdded_SetsCollectionPath()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.CollectionPath.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public void CreateAdded_SetsDocumentId()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.DocumentId.ShouldBe(TestDocumentId);
	}

	[Fact]
	public void CreateAdded_SetsDocumentData()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object> { ["name"] = "test" };

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, data, timestamp, null, null);

		// Assert
		evt.DocumentData.ShouldNotBeNull();
		evt.DocumentData["name"].ShouldBe("test");
	}

	[Fact]
	public void CreateAdded_SetsTimestamp()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void CreateAdded_SetsUpdateTime()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;
		var updateTime = DateTimeOffset.UtcNow.AddMinutes(-1);

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, updateTime, null);

		// Assert
		evt.UpdateTime.ShouldBe(updateTime);
	}

	[Fact]
	public void CreateAdded_SetsCreateTime()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;
		var createTime = DateTimeOffset.UtcNow.AddMinutes(-2);

		// Act
		var evt = FirestoreDataChangeEvent.CreateAdded(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, createTime);

		// Assert
		evt.CreateTime.ShouldBe(createTime);
	}

	#endregion

	#region CreateModified Factory Method Tests

	[Fact]
	public void CreateModified_SetsChangeTypeToModified()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateModified(
			position, TestCollectionPath, TestDocumentId, null, timestamp, null, null);

		// Assert
		evt.ChangeType.ShouldBe(FirestoreDataChangeType.Modified);
	}

	[Fact]
	public void CreateModified_SetsAllProperties()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;
		var updateTime = DateTimeOffset.UtcNow;
		var createTime = DateTimeOffset.UtcNow.AddMinutes(-5);
		var data = new Dictionary<string, object> { ["updated"] = true };

		// Act
		var evt = FirestoreDataChangeEvent.CreateModified(
			position, TestCollectionPath, TestDocumentId, data, timestamp, updateTime, createTime);

		// Assert
		evt.Position.ShouldBe(position);
		evt.CollectionPath.ShouldBe(TestCollectionPath);
		evt.DocumentId.ShouldBe(TestDocumentId);
		evt.DocumentData.ShouldNotBeNull();
		evt.Timestamp.ShouldBe(timestamp);
		evt.UpdateTime.ShouldBe(updateTime);
		evt.CreateTime.ShouldBe(createTime);
	}

	#endregion

	#region CreateRemoved Factory Method Tests

	[Fact]
	public void CreateRemoved_SetsChangeTypeToRemoved()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateRemoved(
			position, TestCollectionPath, TestDocumentId, timestamp);

		// Assert
		evt.ChangeType.ShouldBe(FirestoreDataChangeType.Removed);
	}

	[Fact]
	public void CreateRemoved_HasNullDocumentData()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateRemoved(
			position, TestCollectionPath, TestDocumentId, timestamp);

		// Assert
		evt.DocumentData.ShouldBeNull();
	}

	[Fact]
	public void CreateRemoved_HasNullUpdateTime()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateRemoved(
			position, TestCollectionPath, TestDocumentId, timestamp);

		// Assert
		evt.UpdateTime.ShouldBeNull();
	}

	[Fact]
	public void CreateRemoved_HasNullCreateTime()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateRemoved(
			position, TestCollectionPath, TestDocumentId, timestamp);

		// Assert
		evt.CreateTime.ShouldBeNull();
	}

	[Fact]
	public void CreateRemoved_SetsBasicProperties()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = FirestoreDataChangeEvent.CreateRemoved(
			position, TestCollectionPath, TestDocumentId, timestamp);

		// Assert
		evt.Position.ShouldBe(position);
		evt.CollectionPath.ShouldBe(TestCollectionPath);
		evt.DocumentId.ShouldBe(TestDocumentId);
		evt.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Default Property Tests

	[Fact]
	public void HaveEmptyCollectionPathByDefault()
	{
		// Arrange & Act
		var evt = new FirestoreDataChangeEvent();

		// Assert
		evt.CollectionPath.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyDocumentIdByDefault()
	{
		// Arrange & Act
		var evt = new FirestoreDataChangeEvent();

		// Assert
		evt.DocumentId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveAddedAsDefaultChangeType()
	{
		// Arrange & Act
		var evt = new FirestoreDataChangeEvent();

		// Assert
		evt.ChangeType.ShouldBe(FirestoreDataChangeType.Added);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(FirestoreDataChangeEvent).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreDataChangeEvent).IsPublic.ShouldBeTrue();
	}

	#endregion
}
