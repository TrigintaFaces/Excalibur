// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Shouldly;
using Excalibur.Outbox.Postgres;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class PostgresRecoveryRowReaderShould
{
	[Fact]
	public void PreserveTheClosedRecoveryProjection()
	{
		var stamp = new DateTimeOffset(2026, 11, 13, 7, 11, 23, TimeSpan.FromHours(2));
		using var table = CreateTable();
		_ = table.Rows.Add("id", "tenant", "type", "{\"n\":2}", new byte[] { 0, 255, 1 }, stamp, 8, "failure");
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = PostgresRecoveryRowReader.Read(reader);
		row.MessageId.ShouldBe("id");
		row.TenantId.ShouldBe("tenant");
		row.MessageType.ShouldBe("type");
		row.MessageMetadata.ShouldBe("{\"n\":2}");
		row.MessageBody.ShouldBe(new byte[] { 0, 255, 1 });
		row.OccurredOn.ShouldBe(stamp);
		row.Attempts.ShouldBe(8);
		row.ErrorMessage.ShouldBe("failure");
	}

	[Fact]
	public void RetainConstructorDefaultsForDatabaseNulls()
	{
		using var table = CreateTable();
		_ = table.Rows.Add(Enumerable.Repeat<object>(DBNull.Value, table.Columns.Count).ToArray());
		using var reader = table.CreateDataReader();
		reader.Read().ShouldBeTrue();
		var row = PostgresRecoveryRowReader.Read(reader);
		row.MessageId.ShouldBe(string.Empty);
		row.TenantId.ShouldBe(string.Empty);
		row.MessageType.ShouldBe(string.Empty);
		row.MessageMetadata.ShouldBe(string.Empty);
		row.MessageBody.ShouldBeEmpty();
		row.OccurredOn.ShouldBe(default(DateTimeOffset));
		row.Attempts.ShouldBe(0);
		row.ErrorMessage.ShouldBeNull();
	}

	private static DataTable CreateTable()
	{
		var table = new DataTable();
		foreach (var name in new[] { "MessageId", "TenantId", "MessageType", "MessageMetadata" })
		{
			_ = table.Columns.Add(name, typeof(string));
		}
		_ = table.Columns.Add("MessageBody", typeof(byte[]));
		_ = table.Columns.Add("OccurredOn", typeof(DateTimeOffset));
		_ = table.Columns.Add("Attempts", typeof(int));
		_ = table.Columns.Add("ErrorMessage", typeof(string));
		return table;
	}
}
