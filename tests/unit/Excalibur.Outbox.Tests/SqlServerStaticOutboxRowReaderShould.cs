// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerStaticOutboxRowReaderShould
{
	[Fact]
	public void PreserveEveryProjectedFieldIncludingBinaryPayloadAndOffset()
	{
		var stamp = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.FromMinutes(330));
		using var table = CreateTable();
		_ = table.Rows.Add("id", "type", new byte[] { 0, 255, 128, 1 }, "{\"nested\":{\"n\":3}}", "destination", stamp, stamp.AddMinutes(1), stamp.AddMinutes(2), 3, 7, "last error", stamp.AddSeconds(1), "correlation", "causation", "tenant", 11, "rabbit,kafka", true, "partition", "group", 9007199254740993L, "processor:with:colons:claim");
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = SqlServerOutboxRowReader.Read(reader, includeDispatcher: true);
		row.Id.ShouldBe("id");
		row.MessageType.ShouldBe("type");
		row.Payload.ShouldBe(new byte[] { 0, 255, 128, 1 });
		row.Headers.ShouldBe("{\"nested\":{\"n\":3}}");
		row.Destination.ShouldBe("destination");
		row.CreatedAt.ShouldBe(stamp);
		row.ScheduledAt.ShouldBe(stamp.AddMinutes(1));
		row.SentAt.ShouldBe(stamp.AddMinutes(2));
		row.Status.ShouldBe(3);
		row.RetryCount.ShouldBe(7);
		row.LastError.ShouldBe("last error");
		row.LastAttemptAt.ShouldBe(stamp.AddSeconds(1));
		row.CorrelationId.ShouldBe("correlation");
		row.CausationId.ShouldBe("causation");
		row.TenantId.ShouldBe("tenant");
		row.Priority.ShouldBe(11);
		row.TargetTransports.ShouldBe("rabbit,kafka");
		row.IsMultiTransport.ShouldBe(true);
		row.PartitionKey.ShouldBe("partition");
		row.GroupKey.ShouldBe("group");
		row.SequenceNumber.ShouldBe(9007199254740993L);
		row.DispatcherId.ShouldBe("processor:with:colons:claim");
	}

	[Fact]
	public void PreserveNullOptionalFieldsAndNumericDefaults()
	{
		using var table = CreateTable();
		_ = table.Rows.Add(Enumerable.Repeat<object>(DBNull.Value, table.Columns.Count).ToArray());
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = SqlServerOutboxRowReader.Read(reader, includeDispatcher: true);
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
		_ = table.Columns.Add("Id", typeof(string));
		_ = table.Columns.Add("MessageType", typeof(string));
		_ = table.Columns.Add("Payload", typeof(byte[]));
		_ = table.Columns.Add("Headers", typeof(string));
		_ = table.Columns.Add("Destination", typeof(string));
		_ = table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("ScheduledAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("SentAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("Status", typeof(int));
		_ = table.Columns.Add("RetryCount", typeof(int));
		_ = table.Columns.Add("LastError", typeof(string));
		_ = table.Columns.Add("LastAttemptAt", typeof(DateTimeOffset));
		_ = table.Columns.Add("CorrelationId", typeof(string));
		_ = table.Columns.Add("CausationId", typeof(string));
		_ = table.Columns.Add("TenantId", typeof(string));
		_ = table.Columns.Add("Priority", typeof(int));
		_ = table.Columns.Add("TargetTransports", typeof(string));
		_ = table.Columns.Add("IsMultiTransport", typeof(bool));
		_ = table.Columns.Add("PartitionKey", typeof(string));
		_ = table.Columns.Add("GroupKey", typeof(string));
		_ = table.Columns.Add("SequenceNumber", typeof(long));
		_ = table.Columns.Add("DispatcherId", typeof(string));
		return table;
	}
}
