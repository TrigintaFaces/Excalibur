// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging.Delivery.EventStore.Snapshots;

/// <summary>
///     Unit tests for SnapshotMetadata and SnapshotMetadataFactory to verify snapshot metadata functionality.
/// </summary>
[Trait("Category", "Unit")]
public class SnapshotMetadataShould
{
	[Fact]
	public void ConstructorShouldInitializeAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var eventId = "event-123";
		var snapshotVersion = "1.2.3";
		var serializerVersion = "2.1.0";

		// Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = timestamp,
			LastAppliedEventId = eventId,
			SnapshotVersion = snapshotVersion,
			SerializerVersion = serializerVersion,
		};

		// Assert
		metadata.LastAppliedEventTimestamp.ShouldBe(timestamp);
		metadata.LastAppliedEventId.ShouldBe(eventId);
		metadata.SnapshotVersion.ShouldBe(snapshotVersion);
		metadata.SerializerVersion.ShouldBe(serializerVersion);
	}

	[Fact]
	public void ConstructorShouldSupportEmptyStrings()
	{
		// Arrange & Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = DateTimeOffset.MinValue,
			LastAppliedEventId = string.Empty,
			SnapshotVersion = string.Empty,
			SerializerVersion = string.Empty,
		};

		// Assert
		metadata.LastAppliedEventId.ShouldBe(string.Empty);
		metadata.SnapshotVersion.ShouldBe(string.Empty);
		metadata.SerializerVersion.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConstructorShouldPreserveDateTimeOffsetWithTimeZone()
	{
		// Arrange
		var timestampWithOffset = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.FromHours(-5));

		// Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = timestampWithOffset,
			LastAppliedEventId = "event-with-timezone",
			SnapshotVersion = "1.0.0",
			SerializerVersion = "1.0.0",
		};

		// Assert
		metadata.LastAppliedEventTimestamp.ShouldBe(timestampWithOffset);
		metadata.LastAppliedEventTimestamp.Offset.ShouldBe(TimeSpan.FromHours(-5));
	}

	[Fact]
	public void ConstructorShouldSupportLongStrings()
	{
		// Arrange
		var longEventId = new string('E', 1000);
		var longSnapshotVersion = new string('S', 500);
		var longSerializerVersion = new string('R', 250);

		// Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = DateTimeOffset.UtcNow,
			LastAppliedEventId = longEventId,
			SnapshotVersion = longSnapshotVersion,
			SerializerVersion = longSerializerVersion,
		};

		// Assert
		metadata.LastAppliedEventId.Length.ShouldBe(1000);
		metadata.SnapshotVersion.Length.ShouldBe(500);
		metadata.SerializerVersion.Length.ShouldBe(250);
	}

	[Fact]
	public void ConstructorShouldSupportSpecialCharacters()
	{
		// Arrange
		var eventIdWithSpecialChars = "event-Ã°Å¸Å¡â‚¬-Ã§â€°Â¹Ã¦Â®Å Ã¥Â­â€”Ã§Â¬Â¦-@#$%";
		var versionWithSpecialChars = "v1.0.0-beta+Ã§â€°Â¹Ã¦Â®Å ";
		var serializerWithSpecialChars = "serializer-v2.1.0-Ã°Å¸Å½Â¯";

		// Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = DateTimeOffset.UtcNow,
			LastAppliedEventId = eventIdWithSpecialChars,
			SnapshotVersion = versionWithSpecialChars,
			SerializerVersion = serializerWithSpecialChars,
		};

		// Assert
		metadata.LastAppliedEventId.ShouldBe(eventIdWithSpecialChars);
		metadata.SnapshotVersion.ShouldBe(versionWithSpecialChars);
		metadata.SerializerVersion.ShouldBe(serializerWithSpecialChars);
	}

	[Fact]
	public void FactoryCreateShouldCreateMetadataFromEventStoreMessage()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		var eventId = "test-event-123";
		var timestamp = DateTimeOffset.UtcNow;
		var serializerVersion = "1.0.0";
		var snapshotVersion = "2.1.0";

		_ = A.CallTo(() => eventMessage.EventId).Returns(eventId);
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(timestamp);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, serializerVersion, snapshotVersion);

		// Assert
		metadata.LastAppliedEventId.ShouldBe(eventId);
		metadata.LastAppliedEventTimestamp.ShouldBe(timestamp);
		metadata.SerializerVersion.ShouldBe(serializerVersion);
		metadata.SnapshotVersion.ShouldBe(snapshotVersion);
	}

	[Fact]
	public void FactoryCreateShouldCreateMetadataFromEventStoreMessageWithGuidKey()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<Guid>>();
		var eventId = Guid.NewGuid().ToString();
		var timestamp = DateTimeOffset.UtcNow;
		var serializerVersion = "2.0.0";
		var snapshotVersion = "3.0.0";

		_ = A.CallTo(() => eventMessage.EventId).Returns(eventId);
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(timestamp);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, serializerVersion, snapshotVersion);

		// Assert
		metadata.LastAppliedEventId.ShouldBe(eventId);
		metadata.LastAppliedEventTimestamp.ShouldBe(timestamp);
		metadata.SerializerVersion.ShouldBe(serializerVersion);
		metadata.SnapshotVersion.ShouldBe(snapshotVersion);
	}

	[Fact]
	public void FactoryCreateShouldCreateMetadataFromEventStoreMessageWithIntKey()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<int>>();
		var eventId = "int-key-event-456";
		var timestamp = DateTimeOffset.UtcNow;
		var serializerVersion = "1.5.0";
		var snapshotVersion = "2.5.0";

		_ = A.CallTo(() => eventMessage.EventId).Returns(eventId);
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(timestamp);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, serializerVersion, snapshotVersion);

		// Assert
		metadata.LastAppliedEventId.ShouldBe(eventId);
		metadata.LastAppliedEventTimestamp.ShouldBe(timestamp);
		metadata.SerializerVersion.ShouldBe(serializerVersion);
		metadata.SnapshotVersion.ShouldBe(snapshotVersion);
	}

	[Fact]
	public void FactoryCreateShouldThrowArgumentNullExceptionForNullEvent()
	{
		// Arrange
		IEventStoreMessage<string>? nullEvent = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _ = SnapshotMetadataFactory.Create(nullEvent, "1.0.0", "1.0.0"));
	}

	[Fact]
	public void FactoryCreateShouldAcceptEmptyVersionStrings()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns("test-event");
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, string.Empty, string.Empty);

		// Assert
		metadata.SerializerVersion.ShouldBe(string.Empty);
		metadata.SnapshotVersion.ShouldBe(string.Empty);
	}

	[Fact]
	public void FactoryCreateShouldPreserveDateTimeOffsetFromEvent()
	{
		// Arrange
		var specificTimestamp = new DateTimeOffset(2025, 6, 15, 9, 30, 45, 123, TimeSpan.FromHours(3));
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns("timestamp-test");
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(specificTimestamp);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, "1.0.0", "1.0.0");

		// Assert
		metadata.LastAppliedEventTimestamp.ShouldBe(specificTimestamp);
		metadata.LastAppliedEventTimestamp.Offset.ShouldBe(TimeSpan.FromHours(3));
		metadata.LastAppliedEventTimestamp.Millisecond.ShouldBe(123);
	}

	[Fact]
	public void FactoryCreateShouldHandleComplexKeyTypes()
	{
		// Arrange
		// Use a public type (Tuple) as the generic key type to avoid FakeItEasy proxy issues
		// with private nested classes that can't be accessed by DynamicProxyGenAssembly2
		var eventMessage = A.Fake<IEventStoreMessage<Tuple<string, int>>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns("complex-event-789");
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, "complex-1.0", "snapshot-2.0");

		// Assert
		metadata.LastAppliedEventId.ShouldBe("complex-event-789");
		metadata.SerializerVersion.ShouldBe("complex-1.0");
		metadata.SnapshotVersion.ShouldBe("snapshot-2.0");
	}

	[Fact]
	public void FactoryCreateShouldHandleNullVersionStrings()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns("null-version-test");
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, null!, null!);

		// Assert
		metadata.SerializerVersion.ShouldBe(null!);
		metadata.SnapshotVersion.ShouldBe(null!);
	}

	[Fact]
	public void FactoryCreateShouldHandleEventWithEmptyEventId()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns(string.Empty);
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, "1.0.0", "1.0.0");

		// Assert
		metadata.LastAppliedEventId.ShouldBe(string.Empty);
	}

	[Fact]
	public void FactoryCreateShouldHandleEventWithSpecialCharacterEventId()
	{
		// Arrange
		var specialEventId = "event-Ã°Å¸Å½Â¯-Ã§â€°Â¹Ã¦Â®Å -@#$%^&*()";
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns(specialEventId);
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, "1.0.0", "1.0.0");

		// Assert
		metadata.LastAppliedEventId.ShouldBe(specialEventId);
	}

	[Fact]
	public void FactoryCreateShouldHandleLongVersionStrings()
	{
		// Arrange
		var eventMessage = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => eventMessage.EventId).Returns("long-version-test");
		_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

		var longSerializerVersion = new string('S', 1000);
		var longSnapshotVersion = new string('N', 500);

		// Act
		var metadata = SnapshotMetadataFactory.Create(eventMessage, longSerializerVersion, longSnapshotVersion);

		// Assert
		metadata.SerializerVersion.Length.ShouldBe(1000);
		metadata.SnapshotVersion.Length.ShouldBe(500);
	}

	[Fact]
	public void MetadataShouldSupportEquality()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var metadata1 = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = timestamp,
			LastAppliedEventId = "same-event",
			SnapshotVersion = "1.0.0",
			SerializerVersion = "2.0.0",
		};

		var metadata2 = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = timestamp,
			LastAppliedEventId = "same-event",
			SnapshotVersion = "1.0.0",
			SerializerVersion = "2.0.0",
		};

		// Act & Assert
		// Note: These are different instances but should have same property values
		metadata1.LastAppliedEventTimestamp.ShouldBe(metadata2.LastAppliedEventTimestamp);
		metadata1.LastAppliedEventId.ShouldBe(metadata2.LastAppliedEventId);
		metadata1.SnapshotVersion.ShouldBe(metadata2.SnapshotVersion);
		metadata1.SerializerVersion.ShouldBe(metadata2.SerializerVersion);
	}

	[Fact]
	public void FactoryCreateShouldWorkWithMultipleConcurrentCalls()
	{
		// Arrange
		var tasks = new List<Task<SnapshotMetadata>>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			// Capture loop variable to avoid closure issue where all tasks see final i=10
			var index = i;
			tasks.Add(Task.Run(() =>
			{
				var eventMessage = A.Fake<IEventStoreMessage<string>>();
				_ = A.CallTo(() => eventMessage.EventId).Returns($"concurrent-event-{index}");
				_ = A.CallTo(() => eventMessage.OccurredOn).Returns(DateTimeOffset.UtcNow);

				return SnapshotMetadataFactory.Create(eventMessage, $"serializer-{index}", $"snapshot-{index}");
			}));
		}

		var results = Task.WhenAll(tasks).Result;

		// Assert
		results.Length.ShouldBe(10);
		results.All(static r => r != null).ShouldBeTrue();
		results.Select(static r => r.LastAppliedEventId).Distinct().Count().ShouldBe(10);
	}
}
