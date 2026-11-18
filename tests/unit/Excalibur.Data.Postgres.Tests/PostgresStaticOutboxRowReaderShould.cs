// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Outbox.Postgres;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class PostgresStaticOutboxRowReaderShould
{
	[Fact]
	public void PreserveEveryProjectedFieldIncludingBinaryPayloadAndOffset()
	{
		var stamp = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.FromMinutes(330));
		using var table = CreateTable();
		_ = table.Rows.Add("id", "type", "{\"nested\":{\"n\":3}}", new byte[] { 0, 255, 128, 1 }, "tenant", "destination", "correlation", "causation", 11, stamp.AddMinutes(1), "partition", "group", 9007199254740993L, "rabbit,kafka", true, stamp, 7, "last error", "processor:with:colons:claim", stamp.AddMinutes(2));
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = PostgresOutboxRowReader.Read(reader);
		row.MessageId.ShouldBe("id");
		row.MessageType.ShouldBe("type");
		row.MessageMetadata.ShouldBe("{\"nested\":{\"n\":3}}");
		row.MessageBody.ShouldBe(new byte[] { 0, 255, 128, 1 });
		row.TenantId.ShouldBe("tenant");
		row.Destination.ShouldBe("destination");
		row.CorrelationId.ShouldBe("correlation");
		row.CausationId.ShouldBe("causation");
		row.Priority.ShouldBe(11);
		row.ScheduledAt.ShouldBe(stamp.AddMinutes(1));
		row.PartitionKey.ShouldBe("partition");
		row.GroupKey.ShouldBe("group");
		row.SequenceNumber.ShouldBe(9007199254740993L);
		row.TargetTransports.ShouldBe("rabbit,kafka");
		row.IsMultiTransport.ShouldBe(true);
		row.CreatedAt.ShouldBe(stamp);
		row.Attempts.ShouldBe(7);
		row.LastError.ShouldBe("last error");
		row.DispatcherId.ShouldBe("processor:with:colons:claim");
		row.DispatcherTimeout.ShouldBe(stamp.AddMinutes(2));
	}

	[Fact]
	public void PreserveNullOptionalFieldsAndNumericDefaults()
	{
		using var table = CreateTable();
		_ = table.Rows.Add(Enumerable.Repeat<object>(DBNull.Value, table.Columns.Count).ToArray());
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = PostgresOutboxRowReader.Read(reader);
		row.TenantId.ShouldBeNull();
		row.ScheduledAt.ShouldBeNull();
		row.DispatcherId.ShouldBeNull();
		row.SequenceNumber.ShouldBe(0);
		row.IsMultiTransport.ShouldBeFalse();
		row.CreatedAt.ShouldBe(default);
	}

	private static DataTable CreateTable()
	{
		var table = new DataTable();
		_ = table.Columns.Add("MessageId", typeof(string));
		_ = table.Columns.Add("MessageType", typeof(string));
		_ = table.Columns.Add("MessageMetadata", typeof(string));
		_ = table.Columns.Add("MessageBody", typeof(byte[]));
		_ = table.Columns.Add("TenantId", typeof(string));
		_ = table.Columns.Add("Destination", typeof(string));
		_ = table.Columns.Add("CorrelationId", typeof(string));
		_ = table.Columns.Add("CausationId", typeof(string));
		_ = table.Columns.Add("Priority", typeof(int));
		_ = table.Columns.Add("ScheduledAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("PartitionKey", typeof(string));
		_ = table.Columns.Add("GroupKey", typeof(string));
		_ = table.Columns.Add("SequenceNumber", typeof(long));
		_ = table.Columns.Add("TargetTransports", typeof(string));
		_ = table.Columns.Add("IsMultiTransport", typeof(bool));
		_ = table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("Attempts", typeof(int));
		_ = table.Columns.Add("LastError", typeof(string));
		_ = table.Columns.Add("DispatcherId", typeof(string));
		_ = table.Columns.Add("DispatcherTimeout", typeof(DateTimeOffset));
		return table;
	}
}
