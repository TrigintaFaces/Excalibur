// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Regression tests for DynamoDb EventStore metadata serialization (Bug yflon).
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that metadata is correctly serialized and deserialized using
/// the Base64-encoded JSON pattern. Bug yflon reported a mismatch between write
/// (JSON string) and read (byte[]) paths.
/// </para>
/// <para>
/// The correct implementation uses:
/// - Write: Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(evt.Metadata))
/// - Read: Convert.FromBase64String(metaAttr.S) → returns byte[]
/// </para>
/// <para>
/// Sprint 128 task 49y2g: Verify bug yflon is properly fixed.
/// </para>
/// </remarks>
[Collection("EventStore Telemetry Tests")]
public sealed class DynamoDbMetadataRoundTripShould : IClassFixture<DynamoDbEventStoreTelemetryTestFixture>, IAsyncLifetime
{
	private readonly DynamoDbEventStoreTelemetryTestFixture _fixture;

	public DynamoDbMetadataRoundTripShould(DynamoDbEventStoreTelemetryTestFixture fixture)
	{
		_fixture = fixture;
	}

	public Task InitializeAsync() => Task.CompletedTask;

	public async Task DisposeAsync()
	{
		if (_fixture.IsInitialized)
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}

		_fixture.ClearRecordedActivities();
	}

	#region Simple Metadata Tests

	/// <summary>
	/// Verifies that simple string metadata values round-trip correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_SimpleStringMetadata()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>
		{
			["UserId"] = "user-123",
			["TenantId"] = "tenant-456",
			["CorrelationId"] = Guid.NewGuid().ToString(),
		};

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();

		loadedMetadata["UserId"].ToString().ShouldBe("user-123");
		loadedMetadata["TenantId"].ToString().ShouldBe("tenant-456");
		loadedMetadata["CorrelationId"].ToString().ShouldBe(metadata["CorrelationId"].ToString());
	}

	/// <summary>
	/// Verifies that numeric metadata values round-trip correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_NumericMetadata()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>
		{
			["RetryCount"] = 3,
			["ProcessingTimeMs"] = 150.5,
			["Priority"] = 100,
		};

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();

		loadedMetadata["RetryCount"].GetInt32().ShouldBe(3);
		loadedMetadata["Priority"].GetInt32().ShouldBe(100);
	}

	#endregion Simple Metadata Tests

	#region Null and Empty Metadata Tests

	/// <summary>
	/// Verifies that null metadata is handled correctly.
	/// </summary>
	[Fact]
	public async Task Handle_NullMetadata()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var evt = CreateTestEventWithMetadata(null);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		loaded[0].Metadata.ShouldBeNull();
	}

	/// <summary>
	/// Verifies that empty dictionary metadata is handled correctly.
	/// </summary>
	[Fact]
	public async Task Handle_EmptyMetadata()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>();
		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		// Empty metadata may be preserved as empty object or null depending on implementation
		if (loaded[0].Metadata != null)
		{
			var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[0].Metadata);
			_ = loadedMetadata.ShouldNotBeNull();
			loadedMetadata.ShouldBeEmpty();
		}
	}

	#endregion Null and Empty Metadata Tests

	#region Special Characters Tests

	/// <summary>
	/// Verifies that metadata with special characters round-trips correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_SpecialCharacters()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>
		{
			["Quote"] = "He said \"Hello World\"",
			["Backslash"] = "Path\\To\\File",
			["NewLine"] = "Line1\nLine2",
			["Tab"] = "Col1\tCol2",
			["Unicode"] = "日本語テスト",
			["Emoji"] = "🎉✨🚀",
		};

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();

		loadedMetadata["Quote"].ToString().ShouldBe("He said \"Hello World\"");
		loadedMetadata["Backslash"].ToString().ShouldBe("Path\\To\\File");
		loadedMetadata["NewLine"].ToString().ShouldBe("Line1\nLine2");
		loadedMetadata["Tab"].ToString().ShouldBe("Col1\tCol2");
		loadedMetadata["Unicode"].ToString().ShouldBe("日本語テスト");
		loadedMetadata["Emoji"].ToString().ShouldBe("🎉✨🚀");
	}

	#endregion Special Characters Tests

	#region Nested Object Tests

	/// <summary>
	/// Verifies that metadata with nested objects round-trips correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_NestedObjects()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>
		{
			["Simple"] = "value",
			["Nested"] = new Dictionary<string, object>
			{
				["Level2"] = "nested-value",
				["Number"] = 42,
			},
		};

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();

		loadedMetadata["Simple"].GetString().ShouldBe("value");
		loadedMetadata["Nested"].ValueKind.ShouldBe(JsonValueKind.Object);

		var nested = loadedMetadata["Nested"];
		nested.GetProperty("Level2").GetString().ShouldBe("nested-value");
		nested.GetProperty("Number").GetInt32().ShouldBe(42);
	}

	/// <summary>
	/// Verifies that metadata with arrays round-trips correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_ArrayValues()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>
		{
			["Tags"] = new[] { "tag1", "tag2", "tag3" },
			["Numbers"] = new[] { 1, 2, 3, 4, 5 },
		};

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();

		loadedMetadata["Tags"].ValueKind.ShouldBe(JsonValueKind.Array);
		var tags = loadedMetadata["Tags"].EnumerateArray().Select(e => e.GetString()).ToList();
		tags.ShouldContain("tag1");
		tags.ShouldContain("tag2");
		tags.ShouldContain("tag3");
	}

	#endregion Nested Object Tests

	#region Multiple Events Tests

	/// <summary>
	/// Verifies that metadata is preserved across multiple events in a single append.
	/// </summary>
	[Fact]
	public async Task Preserve_MetadataAcrossMultipleEvents()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		var events = new List<IDomainEvent>
		{
			CreateTestEventWithMetadata(new Dictionary<string, object> { ["Index"] = 0, ["Type"] = "First" }),
			CreateTestEventWithMetadata(new Dictionary<string, object> { ["Index"] = 1, ["Type"] = "Second" }),
			CreateTestEventWithMetadata(new Dictionary<string, object> { ["Index"] = 2, ["Type"] = "Third" }),
		};

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(3);

		for (var i = 0; i < 3; i++)
		{
			_ = loaded[i].Metadata.ShouldNotBeNull();
			var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(loaded[i].Metadata);
			_ = meta.ShouldNotBeNull();
			meta["Index"].GetInt32().ShouldBe(i);
		}
	}

	/// <summary>
	/// Verifies that metadata is preserved across multiple append operations.
	/// </summary>
	[Fact]
	public async Task Preserve_MetadataAcrossMultipleAppends()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Three separate appends
		var evt1 = CreateTestEventWithMetadata(new Dictionary<string, object> { ["Batch"] = "First" });
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt1], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var evt2 = CreateTestEventWithMetadata(new Dictionary<string, object> { ["Batch"] = "Second" });
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt2], 0, CancellationToken.None)
			.ConfigureAwait(false);

		var evt3 = CreateTestEventWithMetadata(new Dictionary<string, object> { ["Batch"] = "Third" });
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt3], 1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.Count.ShouldBe(3);

		var meta1 = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[0].Metadata);
		var meta2 = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[1].Metadata);
		var meta3 = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[2].Metadata);

		meta1["Batch"].ToString().ShouldBe("First");
		meta2["Batch"].ToString().ShouldBe("Second");
		meta3["Batch"].ToString().ShouldBe("Third");
	}

	#endregion Multiple Events Tests

	#region Large Metadata Tests

	/// <summary>
	/// Verifies that large metadata (within DynamoDB item size limits) round-trips correctly.
	/// </summary>
	[Fact]
	public async Task Preserve_LargeMetadata()
	{
		// Arrange
		var eventStore = _fixture.CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create metadata with many entries (but within DynamoDB limits)
		var metadata = new Dictionary<string, object>();
		for (var i = 0; i < 50; i++)
		{
			metadata[$"Key_{i:D3}"] = $"Value_{i:D3}_{new string('x', 100)}";
		}

		var evt = CreateTestEventWithMetadata(metadata);

		// Act
		_ = await ((IEventStore)eventStore).AppendAsync(aggregateId, "TestAggregate", [evt], -1, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await ((IEventStore)eventStore).LoadAsync(aggregateId, "TestAggregate", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeEmpty();
		_ = loaded[0].Metadata.ShouldNotBeNull();

		var loadedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(loaded[0].Metadata);
		_ = loadedMetadata.ShouldNotBeNull();
		loadedMetadata.Count.ShouldBe(50);

		// Verify first and last entries
		loadedMetadata["Key_000"].ToString().ShouldStartWith("Value_000_");
		loadedMetadata["Key_049"].ToString().ShouldStartWith("Value_049_");
	}

	#endregion Large Metadata Tests

	#region Helper Methods

	private static DynamoDbMetadataTestDomainEvent CreateTestEventWithMetadata(IDictionary<string, object>? metadata)
	{
		return new DynamoDbMetadataTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 0,
			EventType = "MetadataTestEvent",
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = metadata,
			Value = "Test-" + Guid.NewGuid().ToString("N"),
		};
	}

	#endregion Helper Methods
}

/// <summary>
/// Test domain event for DynamoDb metadata round-trip tests.
/// </summary>
internal sealed class DynamoDbMetadataTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required string EventType { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public required string Value { get; init; }
}
