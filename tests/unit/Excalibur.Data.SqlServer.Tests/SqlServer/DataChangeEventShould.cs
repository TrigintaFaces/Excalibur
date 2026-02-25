// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class DataChangeEventShould
{
	[Fact]
	public void ThrowWhenDeleteChangeIsNull()
	{
		Should.Throw<ArgumentNullException>(() => DataChangeEvent.CreateDeleteEvent(null!));
	}

	[Fact]
	public void CreateDeleteEvent()
	{
		// Arrange
		var row = CreateCdcRow(CdcOperationCodes.Delete, new Dictionary<string, object> { ["Name"] = "John" });

		// Act
		var evt = DataChangeEvent.CreateDeleteEvent(row);

		// Assert
		evt.ChangeType.ShouldBe(DataChangeType.Delete);
		evt.TableName.ShouldBe("TestTable");
		evt.Changes.Count.ShouldBe(1);
		evt.Changes[0].OldValue.ShouldBe("John");
		evt.Changes[0].NewValue.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenInsertChangeIsNull()
	{
		Should.Throw<ArgumentNullException>(() => DataChangeEvent.CreateInsertEvent(null!));
	}

	[Fact]
	public void CreateInsertEvent()
	{
		// Arrange
		var row = CreateCdcRow(CdcOperationCodes.Insert, new Dictionary<string, object> { ["Name"] = "Jane" });

		// Act
		var evt = DataChangeEvent.CreateInsertEvent(row);

		// Assert
		evt.ChangeType.ShouldBe(DataChangeType.Insert);
		evt.Changes.Count.ShouldBe(1);
		evt.Changes[0].OldValue.ShouldBeNull();
		evt.Changes[0].NewValue.ShouldBe("Jane");
	}

	[Fact]
	public void ThrowWhenUpdateBeforeChangeIsNull()
	{
		var after = CreateCdcRow(CdcOperationCodes.UpdateAfter, new Dictionary<string, object> { ["Name"] = "after" });
		Should.Throw<ArgumentNullException>(() => DataChangeEvent.CreateUpdateEvent(null!, after));
	}

	[Fact]
	public void ThrowWhenUpdateAfterChangeIsNull()
	{
		var before = CreateCdcRow(CdcOperationCodes.UpdateBefore, new Dictionary<string, object> { ["Name"] = "before" });
		Should.Throw<ArgumentNullException>(() => DataChangeEvent.CreateUpdateEvent(before, null!));
	}

	[Fact]
	public void CreateUpdateEvent()
	{
		// Arrange
		var before = CreateCdcRow(CdcOperationCodes.UpdateBefore, new Dictionary<string, object> { ["Name"] = "Old" });
		var after = CreateCdcRow(CdcOperationCodes.UpdateAfter, new Dictionary<string, object> { ["Name"] = "New" });

		// Act
		var evt = DataChangeEvent.CreateUpdateEvent(before, after);

		// Assert
		evt.ChangeType.ShouldBe(DataChangeType.Update);
		evt.Changes.Count.ShouldBe(1);
		evt.Changes[0].OldValue.ShouldBe("Old");
		evt.Changes[0].NewValue.ShouldBe("New");
	}

	[Fact]
	public void HandleDbNullInDeleteEvent()
	{
		var row = CreateCdcRow(CdcOperationCodes.Delete, new Dictionary<string, object> { ["Name"] = DBNull.Value });
		DataChangeEvent.CreateDeleteEvent(row).Changes[0].OldValue.ShouldBeNull();
	}

	[Fact]
	public void HandleDbNullInInsertEvent()
	{
		var row = CreateCdcRow(CdcOperationCodes.Insert, new Dictionary<string, object> { ["Name"] = DBNull.Value });
		DataChangeEvent.CreateInsertEvent(row).Changes[0].NewValue.ShouldBeNull();
	}

	[Fact]
	public void CopyLsnFromCdcRow()
	{
		byte[] lsn = [0x01, 0x02];
		var row = new CdcRow
		{
			TableName = "T", Lsn = lsn, SeqVal = [0x03], CommitTime = DateTime.UtcNow,
			OperationCode = CdcOperationCodes.Insert,
			Changes = new Dictionary<string, object> { ["C"] = "V" },
			DataTypes = new Dictionary<string, Type> { ["C"] = typeof(string) },
		};
		DataChangeEvent.CreateInsertEvent(row).Lsn.ShouldBe(lsn);
	}

	[Fact]
	public void HaveDefaultEmptyCollections()
	{
		var evt = new DataChangeEvent();
		evt.Lsn.ShouldBeEmpty();
		evt.SeqVal.ShouldBeEmpty();
		evt.Changes.ShouldBeEmpty();
		evt.TableName.ShouldBe(string.Empty);
	}

	private static CdcRow CreateCdcRow(CdcOperationCodes op, Dictionary<string, object> changes) =>
		new()
		{
			TableName = "TestTable", Lsn = [0x01], SeqVal = [0x02], CommitTime = DateTime.UtcNow,
			OperationCode = op, Changes = changes,
			DataTypes = changes.Keys.ToDictionary(k => k, _ => typeof(string)),
		};
}
