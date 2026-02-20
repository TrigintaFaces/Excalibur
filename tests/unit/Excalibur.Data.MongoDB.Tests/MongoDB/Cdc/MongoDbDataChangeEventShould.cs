// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;

using MongoDB.Bson;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbDataChangeEventShould
{
	[Fact]
	public void CreateInsertEvent()
	{
		var position = MongoDbCdcPosition.Start;
		var evt = MongoDbDataChangeEvent.CreateInsert(position, "testdb", "orders", null, null, null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Insert);
		evt.DatabaseName.ShouldBe("testdb");
		evt.CollectionName.ShouldBe("orders");
		evt.FullNamespace.ShouldBe("testdb.orders");
	}

	[Fact]
	public void CreateUpdateEvent()
	{
		var evt = MongoDbDataChangeEvent.CreateUpdate(
			MongoDbCdcPosition.Start, "testdb", "orders", null, null, null, null, null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Update);
	}

	[Fact]
	public void CreateReplaceEvent()
	{
		var evt = MongoDbDataChangeEvent.CreateReplace(
			MongoDbCdcPosition.Start, "testdb", "orders", null, null, null, null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Replace);
	}

	[Fact]
	public void CreateDeleteEvent()
	{
		var evt = MongoDbDataChangeEvent.CreateDelete(
			MongoDbCdcPosition.Start, "testdb", "orders", null, null, null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Delete);
	}

	[Fact]
	public void CreateDropEvent()
	{
		var evt = MongoDbDataChangeEvent.CreateDrop(
			MongoDbCdcPosition.Start, "testdb", "orders", null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Drop);
	}

	[Fact]
	public void CreateInvalidateEvent()
	{
		var evt = MongoDbDataChangeEvent.CreateInvalidate(MongoDbCdcPosition.Start, null, null);

		evt.ChangeType.ShouldBe(MongoDbDataChangeType.Invalidate);
		evt.DatabaseName.ShouldBe(string.Empty);
		evt.CollectionName.ShouldBe(string.Empty);
	}

	[Fact]
	public void ComputeFullNamespace()
	{
		var evt = MongoDbDataChangeEvent.CreateInsert(
			MongoDbCdcPosition.Start, "mydb", "mycoll", null, null, null, null);

		evt.FullNamespace.ShouldBe("mydb.mycoll");
	}

	[Fact]
	public void StoreUpdateDescription()
	{
		var desc = new MongoDbUpdateDescription
		{
			UpdatedFields = new BsonDocument("name", "new-value"),
			RemovedFields = ["oldField"]
		};

		var evt = MongoDbDataChangeEvent.CreateUpdate(
			MongoDbCdcPosition.Start, "db", "coll", null, null, null, desc, null, null);

		evt.UpdateDescription.ShouldNotBeNull();
		evt.UpdateDescription.RemovedFields.Count.ShouldBe(1);
	}

	[Fact]
	public void HaveDefaultValuesForUpdateDescription()
	{
		var desc = new MongoDbUpdateDescription();

		desc.UpdatedFields.ShouldBeNull();
		desc.RemovedFields.ShouldBeEmpty();
		desc.TruncatedArrays.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultValuesForArrayTruncation()
	{
		var truncation = new MongoDbArrayTruncation();

		truncation.Field.ShouldBe(string.Empty);
		truncation.NewSize.ShouldBe(0);
	}
}
