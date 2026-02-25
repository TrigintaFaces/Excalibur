// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging.Delivery.EventStore.Snapshots;

/// <summary>
///     Unit tests for SnapshotEnvelope to verify snapshot envelope functionality.
/// </summary>
[Trait("Category", "Unit")]
public class SnapshotEnvelopeShould
{
	[Fact]
	public void ConstructorShouldInitializeWithStringKey()
	{
		// Arrange
		var aggregateId = "test-aggregate-123";
		var applicationState = "{\"property\": \"value\", \"count\": 42}";
		var snapshotMetadata = "{\"version\": \"1.0\", \"timestamp\": \"2025-01-01T00:00:00Z\"}";

		// Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = aggregateId,
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void ConstructorShouldInitializeWithGuidKey()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var applicationState = "{\"userId\": \"12345\", \"name\": \"John Doe\", \"isActive\": true}";
		var snapshotMetadata = "{\"createdAt\": \"2025-01-01T10:30:00Z\", \"schemaVersion\": \"2.1\"}";

		// Act
		var envelope = new SnapshotEnvelope<Guid>
		{
			AggregateId = aggregateId,
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void ConstructorShouldInitializeWithIntKey()
	{
		// Arrange
		var aggregateId = 98765;
		var applicationState = "{\"orderId\": 98765, \"total\": 199.99, \"status\": \"shipped\"}";
		var snapshotMetadata = "{\"lastModified\": \"2025-01-01T15:45:00Z\", \"revision\": 3}";

		// Act
		var envelope = new SnapshotEnvelope<int>
		{
			AggregateId = aggregateId,
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldImplementISnapshotEnvelope()
	{
		// Arrange & Act
		var envelope = new SnapshotEnvelope<string> { AggregateId = "test-id", ApplicationState = "{}", SnapshotMetadata = "{}" };

		// Assert
		_ = envelope.ShouldBeAssignableTo<ISnapshotEnvelope<string>>();
	}

	[Fact]
	public void EnvelopeShouldSupportEmptyApplicationState()
	{
		// Arrange & Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "test-id",
			ApplicationState = string.Empty,
			SnapshotMetadata = "{\"version\": \"1.0\"}",
		};

		// Assert
		envelope.ApplicationState.ShouldBe(string.Empty);
	}

	[Fact]
	public void EnvelopeShouldSupportEmptySnapshotMetadata()
	{
		// Arrange & Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "test-id",
			ApplicationState = "{\"data\": \"value\"}",
			SnapshotMetadata = string.Empty,
		};

		// Assert
		envelope.SnapshotMetadata.ShouldBe(string.Empty);
	}

	[Fact]
	public void EnvelopeShouldSupportEmptyAggregateId()
	{
		// Arrange & Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = string.Empty,
			ApplicationState = "{\"data\": \"value\"}",
			SnapshotMetadata = "{\"version\": \"1.0\"}",
		};

		// Assert
		envelope.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void EnvelopeShouldSupportComplexAggregateKeyTypes()
	{
		// Arrange
		var complexKey = new ComplexAggregateKey { TenantId = "tenant-123", EntityId = Guid.NewGuid(), Version = 42 };
		var applicationState = "{\"complexData\": \"test\"}";
		var snapshotMetadata = "{\"keyType\": \"complex\"}";

		// Act
		var envelope = new SnapshotEnvelope<ComplexAggregateKey>
		{
			AggregateId = complexKey,
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.AggregateId.ShouldBe(complexKey);
		envelope.AggregateId.TenantId.ShouldBe("tenant-123");
		envelope.AggregateId.Version.ShouldBe(42);
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldSupportLargeApplicationState()
	{
		// Arrange
		var largeState = new string('A', 10000); // 10KB string
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "large-state-test",
			ApplicationState = largeState,
			SnapshotMetadata = "{\"size\": \"large\"}",
		};

		// Act & Assert
		envelope.ApplicationState.Length.ShouldBe(10000);
		envelope.ApplicationState.ShouldBe(largeState);
	}

	[Fact]
	public void EnvelopeShouldSupportLargeSnapshotMetadata()
	{
		// Arrange
		var largeMetadata = """
			{"description": "
			""" + new string('M', 5000) + """
   "}
   """;
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "metadata-test",
			ApplicationState = "{\"simple\": \"state\"}",
			SnapshotMetadata = largeMetadata,
		};

		// Act & Assert
		envelope.SnapshotMetadata.Length.ShouldBeGreaterThan(5000);
		envelope.SnapshotMetadata.ShouldBe(largeMetadata);
	}

	[Fact]
	public void EnvelopeShouldSupportSpecialCharactersInProperties()
	{
		// Arrange
		var aggregateId = "test-Ã°Å¸Å¡â‚¬-Ã§â€°Â¹Ã¦Â®Å Ã¥Â­â€”Ã§Â¬Â¦-@#$%";
		var applicationState = "{\"message\": \"Hello Ã¤Â¸â€“Ã§â€¢Å’! Ã°Å¸Å’Â\", \"symbols\": \"@#$%^&*()\"}";
		var snapshotMetadata = "{\"comment\": \"Test with Ã§â€°Â¹Ã¦Â®Å Ã¥Â­â€”Ã§Â¬Â¦ and emojis Ã°Å¸Å½Â¯\"}";

		// Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = aggregateId,
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldSupportJsonEscapeCharacters()
	{
		// Arrange
		var applicationState = "{\"text\": \"Line 1\\nLine 2\\tTabbed\\\\\", \"quotes\": \"He said \\\"Hello\\\"\"}";
		var snapshotMetadata = "{\"path\": \"C:\\\\Users\\\\test\\\\\", \"escaped\": \"\\\"value\\\"\"}";

		// Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "escape-test",
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldPreserveWhitespaceInProperties()
	{
		// Arrange
		var applicationState = " {\n \"property\": \"value with spaces\",\n \"number\": 42\n} ";
		var snapshotMetadata = "\t{\n\t\t\"formatted\": true,\n\t\t\"spaces\": \" \"\n\t}";

		// Act
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "whitespace-test",
			ApplicationState = applicationState,
			SnapshotMetadata = snapshotMetadata,
		};

		// Assert
		envelope.ApplicationState.ShouldBe(applicationState);
		envelope.SnapshotMetadata.ShouldBe(snapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldSupportNullableValueTypeKeys()
	{
		// Arrange
		int? nullableId = 123;
		var envelope = new SnapshotEnvelope<int?>
		{
			AggregateId = nullableId,
			ApplicationState = "{\"hasValue\": true}",
			SnapshotMetadata = "{\"keyType\": \"nullable\"}",
		};

		// Act & Assert
		envelope.AggregateId.ShouldBe(nullableId);
		envelope.AggregateId.HasValue.ShouldBeTrue();
		envelope.AggregateId.Value.ShouldBe(123);
	}

	[Fact]
	public void EnvelopeShouldEqualityCheckForSameValues()
	{
		// Arrange
		var envelope1 = new SnapshotEnvelope<string>
		{
			AggregateId = "same-id",
			ApplicationState = "{\"same\": \"state\"}",
			SnapshotMetadata = "{\"same\": \"metadata\"}",
		};

		var envelope2 = new SnapshotEnvelope<string>
		{
			AggregateId = "same-id",
			ApplicationState = "{\"same\": \"state\"}",
			SnapshotMetadata = "{\"same\": \"metadata\"}",
		};

		// Act & Assert
		// Note: These are different instances, so reference equality will be false but property values should be the same
		envelope1.AggregateId.ShouldBe(envelope2.AggregateId);
		envelope1.ApplicationState.ShouldBe(envelope2.ApplicationState);
		envelope1.SnapshotMetadata.ShouldBe(envelope2.SnapshotMetadata);
	}

	[Fact]
	public void EnvelopeShouldSupportDifferentGenericTypeInstances()
	{
		// Arrange
		var stringEnvelope = new SnapshotEnvelope<string> { AggregateId = "string-key", ApplicationState = "{}", SnapshotMetadata = "{}" };

		var guidEnvelope = new SnapshotEnvelope<Guid> { AggregateId = Guid.NewGuid(), ApplicationState = "{}", SnapshotMetadata = "{}" };

		var intEnvelope = new SnapshotEnvelope<int> { AggregateId = 42, ApplicationState = "{}", SnapshotMetadata = "{}" };

		// Act & Assert
		_ = stringEnvelope.ShouldBeAssignableTo<ISnapshotEnvelope<string>>();
		_ = guidEnvelope.ShouldBeAssignableTo<ISnapshotEnvelope<Guid>>();
		_ = intEnvelope.ShouldBeAssignableTo<ISnapshotEnvelope<int>>();
	}

	private sealed class ComplexAggregateKey
	{
		public required string TenantId { get; set; } = string.Empty;

		public Guid EntityId { get; set; }

		public int Version { get; set; }

		public override bool Equals(object? obj) =>
			obj is ComplexAggregateKey other &&
			TenantId == other.TenantId &&
			EntityId == other.EntityId &&
			Version == other.Version;

		public override int GetHashCode() => HashCode.Combine(TenantId, EntityId, Version);
	}
}
