// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcRow"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcRowShould : UnitTestBase
{
	[Fact]
	public void InitializeWithRequiredProperties()
	{
		// Arrange
		var lsn = new byte[] { 0x00, 0x01, 0x02 };
		var seqVal = new byte[] { 0x03, 0x04, 0x05 };
		var changes = new Dictionary<string, object> { { "Id", 1 } };
		var dataTypes = new Dictionary<string, Type> { { "Id", typeof(int) } };

		// Act
		var cdcRow = new CdcRow
		{
			TableName = "Users",
			Lsn = lsn,
			SeqVal = seqVal,
			OperationCode = CdcOperationCodes.Insert,
			CommitTime = DateTime.UtcNow,
			Changes = changes,
			DataTypes = dataTypes
		};

		// Assert
		cdcRow.TableName.ShouldBe("Users");
		cdcRow.Lsn.ShouldBe(lsn);
		cdcRow.SeqVal.ShouldBe(seqVal);
		cdcRow.OperationCode.ShouldBe(CdcOperationCodes.Insert);
		cdcRow.Changes.ShouldBe(changes);
		cdcRow.DataTypes.ShouldBe(dataTypes);
	}

	[Fact]
	public void StoreCommitTime()
	{
		// Arrange
		var commitTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		var cdcRow = CreateCdcRow(commitTime: commitTime);

		// Assert
		cdcRow.CommitTime.ShouldBe(commitTime);
	}

	[Theory]
	[InlineData(CdcOperationCodes.Delete)]
	[InlineData(CdcOperationCodes.Insert)]
	[InlineData(CdcOperationCodes.UpdateBefore)]
	[InlineData(CdcOperationCodes.UpdateAfter)]
	public void SupportAllOperationCodes(CdcOperationCodes operationCode)
	{
		// Act
		var cdcRow = CreateCdcRow(operationCode: operationCode);

		// Assert
		cdcRow.OperationCode.ShouldBe(operationCode);
	}

	[Fact]
	public void StoreMultipleChanges()
	{
		// Arrange
		var changes = new Dictionary<string, object>
		{
			{ "Id", 42 },
			{ "Name", "Test User" },
			{ "IsActive", true },
			{ "Balance", 123.45m }
		};

		// Act
		var cdcRow = CreateCdcRow(changes: changes);

		// Assert
		cdcRow.Changes.Count.ShouldBe(4);
		cdcRow.Changes["Id"].ShouldBe(42);
		cdcRow.Changes["Name"].ShouldBe("Test User");
		cdcRow.Changes["IsActive"].ShouldBe(true);
		cdcRow.Changes["Balance"].ShouldBe(123.45m);
	}

	[Fact]
	public void StoreDataTypeMapping()
	{
		// Arrange
		var dataTypes = new Dictionary<string, Type>
		{
			{ "Id", typeof(int) },
			{ "Name", typeof(string) },
			{ "IsActive", typeof(bool) },
			{ "Balance", typeof(decimal) }
		};

		// Act
		var cdcRow = CreateCdcRow(dataTypes: dataTypes);

		// Assert
		cdcRow.DataTypes.Count.ShouldBe(4);
		cdcRow.DataTypes["Id"].ShouldBe(typeof(int));
		cdcRow.DataTypes["Name"].ShouldBe(typeof(string));
		cdcRow.DataTypes["IsActive"].ShouldBe(typeof(bool));
		cdcRow.DataTypes["Balance"].ShouldBe(typeof(decimal));
	}

	[Fact]
	public void BeEqualWhenPropertiesMatch()
	{
		// Arrange
		var lsn = new byte[] { 0x00, 0x01, 0x02 };
		var seqVal = new byte[] { 0x03, 0x04, 0x05 };
		var changes = new Dictionary<string, object> { { "Id", 1 } };
		var dataTypes = new Dictionary<string, Type> { { "Id", typeof(int) } };
		var commitTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		var row1 = new CdcRow
		{
			TableName = "Users",
			Lsn = lsn,
			SeqVal = seqVal,
			OperationCode = CdcOperationCodes.Insert,
			CommitTime = commitTime,
			Changes = changes,
			DataTypes = dataTypes
		};

		var row2 = new CdcRow
		{
			TableName = "Users",
			Lsn = lsn,
			SeqVal = seqVal,
			OperationCode = CdcOperationCodes.Insert,
			CommitTime = commitTime,
			Changes = changes,
			DataTypes = dataTypes
		};

		// Assert - records with same reference for collections should be equal
		row1.ShouldBe(row2);
	}

	[Fact]
	public void NotBeEqualWhenTableNameDiffers()
	{
		// Arrange
		var row1 = CreateCdcRow(tableName: "Users");
		var row2 = CreateCdcRow(tableName: "Orders");

		// Assert
		row1.ShouldNotBe(row2);
	}

	[Fact]
	public void NotBeEqualWhenOperationCodeDiffers()
	{
		// Arrange
		var row1 = CreateCdcRow(operationCode: CdcOperationCodes.Insert);
		var row2 = CreateCdcRow(operationCode: CdcOperationCodes.Delete);

		// Assert
		row1.ShouldNotBe(row2);
	}

	[Fact]
	public void SupportWithExpression()
	{
		// Arrange
		var original = CreateCdcRow(tableName: "Users");

		// Act
		var modified = original with { TableName = "Orders" };

		// Assert
		modified.TableName.ShouldBe("Orders");
		original.TableName.ShouldBe("Users");
	}

	private static CdcRow CreateCdcRow(
		string tableName = "TestTable",
		byte[]? lsn = null,
		byte[]? seqVal = null,
		CdcOperationCodes operationCode = CdcOperationCodes.Insert,
		DateTime? commitTime = null,
		IDictionary<string, object>? changes = null,
		Dictionary<string, Type>? dataTypes = null)
	{
		return new CdcRow
		{
			TableName = tableName,
			Lsn = lsn ?? [0x00, 0x01],
			SeqVal = seqVal ?? [0x02, 0x03],
			OperationCode = operationCode,
			CommitTime = commitTime ?? DateTime.UtcNow,
			Changes = changes ?? new Dictionary<string, object> { { "Id", 1 } },
			DataTypes = dataTypes ?? new Dictionary<string, Type> { { "Id", typeof(int) } }
		};
	}
}
