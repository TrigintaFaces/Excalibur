// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Jobs.SqlServer;

namespace Excalibur.Jobs.Tests.SqlServer;

/// <summary>
/// Binds the payload serialization used by the SQL Server coordinator's job-distribution and
/// completion-reporting paths. A coordinator distributes arbitrary job payloads, so serialization
/// must work for any type a caller hands it, not only for a <see cref="JsonElement"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerJobCoordinatorPayloadSerializationShould : UnitTestBase
{
	private sealed record OutboxSweepPayload(string BatchId, int MaxMessages);

	[Fact]
	public void SerializeAPocoPayloadToItsRuntimeShape()
	{
		var json = SqlServerJobCoordinator.SerializePayload(new OutboxSweepPayload("batch-7", 250));

		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("BatchId").GetString().ShouldBe("batch-7");
		document.RootElement.GetProperty("MaxMessages").GetInt32().ShouldBe(250);
	}

	[Fact]
	public void SerializeAnAnonymousTypePayload()
	{
		var json = SqlServerJobCoordinator.SerializePayload(new { Work = "sync", Attempt = 3 });

		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("Work").GetString().ShouldBe("sync");
		document.RootElement.GetProperty("Attempt").GetInt32().ShouldBe(3);
	}

	[Fact]
	public void SerializeAPrimitivePayload()
	{
		SqlServerJobCoordinator.SerializePayload(42).ShouldBe("42");
		SqlServerJobCoordinator.SerializePayload("ready").ShouldBe("\"ready\"");
	}

	[Fact]
	public void SerializeAJsonElementPayloadUnchanged()
	{
		var element = JsonSerializer.Deserialize<JsonElement>("""{"Work":"sync"}""");

		var json = SqlServerJobCoordinator.SerializePayload(element);

		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("Work").GetString().ShouldBe("sync");
	}
}
