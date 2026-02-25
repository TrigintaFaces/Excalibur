// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Data.EventStore;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreJsonContextShould
{
	[Fact]
	public void Instance_IsNotNull()
	{
		EventStoreJsonContext.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void Instance_ReturnsSameReference()
	{
		var first = EventStoreJsonContext.Instance;
		var second = EventStoreJsonContext.Instance;
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void Instance_HasCamelCaseNaming()
	{
		EventStoreJsonContext.Instance.Options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void Instance_HasCaseInsensitivePropertyNames()
	{
		EventStoreJsonContext.Instance.Options.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact]
	public void Instance_HasCompactFormatting()
	{
		EventStoreJsonContext.Instance.Options.WriteIndented.ShouldBeFalse();
	}

	[Fact]
	public void SerializeEventMetadata()
	{
		var metadata = new EventMetadata
		{
			EventType = "OrderCreated",
			EventVersion = 1,
			UserId = "user-123",
			CorrelationId = "corr-456"
		};

		var json = JsonSerializer.Serialize(metadata, EventStoreJsonContext.Instance.EventMetadata);

		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("OrderCreated");
	}

	[Fact]
	public void DeserializeEventMetadata()
	{
		var json = """{"eventType":"OrderCreated","eventVersion":1,"userId":"user-123"}""";

		var metadata = JsonSerializer.Deserialize(json, EventStoreJsonContext.Instance.EventMetadata);

		metadata.ShouldNotBeNull();
		metadata.EventType.ShouldBe("OrderCreated");
		metadata.EventVersion.ShouldBe(1);
		metadata.UserId.ShouldBe("user-123");
	}
}
