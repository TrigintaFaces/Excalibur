// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

namespace Excalibur.Dispatch.Tests.Functional.Serialization;

/// <summary>
/// Functional tests for serialization patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Serialization")]
[Trait("Feature", "Patterns")]
public sealed class SerializationPatternsFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void SerializeAndDeserializeSimpleMessage()
	{
		// Arrange
		var message = new TestMessage
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			Value = 42,
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<TestMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(message.Id);
		deserialized.Name.ShouldBe(message.Name);
		deserialized.Value.ShouldBe(message.Value);
	}

	[Fact]
	public void PreservePolymorphicTypes()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
		};

		IMessagePayload payload = new DerivedPayload
		{
			BaseProperty = "Base",
			DerivedProperty = "Derived",
		};

		// Store type info for reconstruction
		var typeInfo = new
		{
			TypeName = payload.GetType().AssemblyQualifiedName,
			Data = JsonSerializer.Serialize(payload, payload.GetType(), options),
		};

		// Act - Deserialize back to concrete type
		var type = Type.GetType(typeInfo.TypeName);
		var deserialized = JsonSerializer.Deserialize(typeInfo.Data, type, options) as DerivedPayload;

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.BaseProperty.ShouldBe("Base");
		deserialized.DerivedProperty.ShouldBe("Derived");
	}

	[Fact]
	public void HandleNullValuesGracefully()
	{
		// Arrange
		var message = new NullableMessage
		{
			RequiredField = "Required",
			OptionalField = null,
			OptionalNumber = null,
		};

		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		};

		// Act
		var json = JsonSerializer.Serialize(message, options);
		var deserialized = JsonSerializer.Deserialize<NullableMessage>(json, options);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.RequiredField.ShouldBe("Required");
		deserialized.OptionalField.ShouldBeNull();
		deserialized.OptionalNumber.ShouldBeNull();
		json.ShouldNotContain("optionalField");
	}

	[Fact]
	public void SerializeDateTimesConsistently()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
		var message = new TimestampedMessage
		{
			CreatedAt = timestamp,
			ProcessedAt = timestamp.AddMinutes(5),
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<TimestampedMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.CreatedAt.ShouldBe(timestamp);
		deserialized.ProcessedAt.ShouldBe(timestamp.AddMinutes(5));
	}

	[Fact]
	public void HandleLargePayloadsEfficiently()
	{
		// Arrange
		var largeData = new byte[1024 * 100]; // 100KB
		new Random(42).NextBytes(largeData);

		var message = new LargePayloadMessage
		{
			Data = Convert.ToBase64String(largeData),
			Checksum = ComputeSimpleChecksum(largeData),
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<LargePayloadMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		var decodedData = Convert.FromBase64String(deserialized.Data);
		decodedData.Length.ShouldBe(largeData.Length);
		ComputeSimpleChecksum(decodedData).ShouldBe(message.Checksum);
	}

	[Fact]
	public void HandleEnumerationsCorrectly()
	{
		// Arrange
		var message = new EnumMessage
		{
			Status = MessageStatus.Processing,
			Priority = MessagePriority.High,
		};

		var options = new JsonSerializerOptions
		{
			Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
		};

		// Act
		var json = JsonSerializer.Serialize(message, options);
		var deserialized = JsonSerializer.Deserialize<EnumMessage>(json, options);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Status.ShouldBe(MessageStatus.Processing);
		deserialized.Priority.ShouldBe(MessagePriority.High);
		json.ShouldContain("Processing");
		json.ShouldContain("High");
	}

	[Fact]
	public void SerializeCollectionsPreservingOrder()
	{
		// Arrange
		var message = new CollectionMessage
		{
			Items = ["First", "Second", "Third"],
			Lookup = new Dictionary<string, int>
			{
				["a"] = 1,
				["b"] = 2,
				["c"] = 3,
			},
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<CollectionMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Items.Count.ShouldBe(3);
		deserialized.Items[0].ShouldBe("First");
		deserialized.Items[1].ShouldBe("Second");
		deserialized.Items[2].ShouldBe("Third");
		deserialized.Lookup["a"].ShouldBe(1);
		deserialized.Lookup["b"].ShouldBe(2);
		deserialized.Lookup["c"].ShouldBe(3);
	}

	[Fact]
	public void HandleNestedObjectGraphs()
	{
		// Arrange
		var message = new NestedMessage
		{
			Root = new TreeNode
			{
				Name = "Root",
				Children =
				[
					new TreeNode
					{
						Name = "Child1",
						Children =
						[
							new TreeNode { Name = "Grandchild1", Children = [] },
						],
					},
					new TreeNode { Name = "Child2", Children = [] },
				],
			},
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<NestedMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Root.Name.ShouldBe("Root");
		deserialized.Root.Children.Count.ShouldBe(2);
		deserialized.Root.Children[0].Name.ShouldBe("Child1");
		deserialized.Root.Children[0].Children[0].Name.ShouldBe("Grandchild1");
		deserialized.Root.Children[1].Name.ShouldBe("Child2");
	}

	[Fact]
	public void HandleSpecialCharactersInStrings()
	{
		// Arrange
		var message = new TestMessage
		{
			Id = Guid.NewGuid(),
			Name = "Test with \"quotes\" and \\ backslashes and \n newlines",
			Value = 0,
		};

		// Act
		var json = JsonSerializer.Serialize(message);
		var deserialized = JsonSerializer.Deserialize<TestMessage>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Name.ShouldContain("\"quotes\"");
		deserialized.Name.ShouldContain("\\");
		deserialized.Name.ShouldContain("\n");
	}

	[Fact]
	public void SupportVersionedSerialization()
	{
		// Arrange - Simulate v1 message
		var v1Message = new { Id = Guid.NewGuid(), Name = "Test" };
		var v1Json = JsonSerializer.Serialize(v1Message);

		// Act - Deserialize to v2 message with additional field
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
		};
		var v2Message = JsonSerializer.Deserialize<V2Message>(v1Json, options);

		// Assert - New field should have default value
		_ = v2Message.ShouldNotBeNull();
		v2Message.Id.ShouldBe(v1Message.Id);
		v2Message.Name.ShouldBe(v1Message.Name);
		v2Message.NewField.ShouldBe("DefaultValue"); // Default value
	}

	private static int ComputeSimpleChecksum(byte[] data)
	{
		unchecked
		{
			var checksum = 0;
			foreach (var b in data)
			{
				checksum = (checksum * 31) + b;
			}

			return checksum;
		}
	}

	private sealed class TestMessage
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public int Value { get; init; }
	}

	private interface IMessagePayload
	{
		string BaseProperty { get; }
	}

	private sealed class DerivedPayload : IMessagePayload
	{
		public string BaseProperty { get; init; } = string.Empty;
		public string DerivedProperty { get; init; } = string.Empty;
	}

	private sealed class NullableMessage
	{
		public string RequiredField { get; init; } = string.Empty;
		public string? OptionalField { get; init; }
		public int? OptionalNumber { get; init; }
	}

	private sealed class TimestampedMessage
	{
		public DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset ProcessedAt { get; init; }
	}

	private sealed class LargePayloadMessage
	{
		public string Data { get; init; } = string.Empty;
		public int Checksum { get; init; }
	}

	private enum MessageStatus
	{
		Pending,
		Processing,
		Completed,
		Failed,
	}

	private enum MessagePriority
	{
		Low,
		Normal,
		High,
		Critical,
	}

	private sealed class EnumMessage
	{
		public MessageStatus Status { get; init; }
		public MessagePriority Priority { get; init; }
	}

	private sealed class CollectionMessage
	{
		public List<string> Items { get; init; } = [];
		public Dictionary<string, int> Lookup { get; init; } = [];
	}

	private sealed class TreeNode
	{
		public string Name { get; init; } = string.Empty;
		public List<TreeNode> Children { get; init; } = [];
	}

	private sealed class NestedMessage
	{
		public TreeNode Root { get; init; } = new();
	}

	private sealed class V2Message
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public string NewField { get; init; } = "DefaultValue";
	}
}
