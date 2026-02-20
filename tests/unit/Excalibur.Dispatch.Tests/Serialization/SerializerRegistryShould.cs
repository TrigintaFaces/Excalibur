// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializerRegistry"/> validating registration,
/// lookup, and thread-safety behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerializerRegistryShould
{
	#region Registration Tests

	[Fact]
	public void Register_WithValidId_AddsSerializer()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");

		// Act
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Assert
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
		registry.IsRegistered("TestSerializer").ShouldBeTrue();
		registry.GetById(SerializerIds.MemoryPack).ShouldBe(serializer);
	}

	[Fact]
	public void Register_WithCustomRangeId_AddsSerializer()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("CustomSerializer");

		// Act
		registry.Register(SerializerIds.CustomRangeStart, serializer);

		// Assert
		registry.IsRegistered(SerializerIds.CustomRangeStart).ShouldBeTrue();
		registry.GetById(SerializerIds.CustomRangeStart).ShouldBe(serializer);
	}

	[Fact]
	public void Register_WithDuplicateId_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("Serializer1");
		var serializer2 = CreateFakeSerializer("Serializer2");
		registry.Register(SerializerIds.MemoryPack, serializer1);

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.MemoryPack, serializer2));
		ex.Message.ShouldContain("already registered");
	}

	[Fact]
	public void Register_WithDuplicateName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("SameName");
		var serializer2 = CreateFakeSerializer("SameName");
		registry.Register(SerializerIds.MemoryPack, serializer1);

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.SystemTextJson, serializer2));
		ex.Message.ShouldContain("SameName");
		ex.Message.ShouldContain("already registered");

		// Verify rollback - ID should not be registered
		registry.IsRegistered(SerializerIds.SystemTextJson).ShouldBeFalse();
	}

	[Fact]
	public void Register_WithReservedId0_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("InvalidSerializer");

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.Invalid, serializer));
		ex.Message.ShouldContain("reserved");
		ex.Message.ShouldContain("Invalid");
	}

	[Fact]
	public void Register_WithReservedId255_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("UnknownSerializer");

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.Unknown, serializer));
		ex.Message.ShouldContain("reserved");
		ex.Message.ShouldContain("Unknown");
	}

	[Fact]
	public void Register_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			registry.Register(SerializerIds.MemoryPack, null!));
	}

	[Fact]
	public void Register_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("");

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.MemoryPack, serializer));
		ex.Message.ShouldContain("null or whitespace");
	}

	#endregion Registration Tests

	#region SetCurrent Tests

	[Fact]
	public void SetCurrent_WithRegisteredName_SetsCurrentSerializer()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act
		registry.SetCurrent("TestSerializer");

		// Assert
		var (id, current) = registry.GetCurrent();
		id.ShouldBe(SerializerIds.MemoryPack);
		current.ShouldBe(serializer);
	}

	[Fact]
	public void SetCurrent_WithUnregisteredName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.SetCurrent("NonExistent"));
		ex.Message.ShouldContain("not registered");
	}

	[Fact]
	public void SetCurrent_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.SetCurrent(null!));
	}

	[Fact]
	public void SetCurrent_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.SetCurrent(""));
	}

	[Fact]
	public void SetCurrent_CanChangeCurrent_UpdatesValue()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("First");
		var serializer2 = CreateFakeSerializer("Second");
		registry.Register(SerializerIds.MemoryPack, serializer1);
		registry.Register(SerializerIds.SystemTextJson, serializer2);
		registry.SetCurrent("First");

		// Act
		registry.SetCurrent("Second");

		// Assert
		var (id, current) = registry.GetCurrent();
		id.ShouldBe(SerializerIds.SystemTextJson);
		current.ShouldBe(serializer2);
	}

	#endregion SetCurrent Tests

	#region GetCurrent Tests

	[Fact]
	public void GetCurrent_WhenNotSet_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			registry.GetCurrent());
		ex.Message.ShouldContain("No current serializer configured");
	}

	#endregion GetCurrent Tests

	#region GetById Tests

	[Fact]
	public void GetById_WithRegisteredId_ReturnsSerializer()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act
		var result = registry.GetById(SerializerIds.MemoryPack);

		// Assert
		result.ShouldBe(serializer);
	}

	[Fact]
	public void GetById_WithUnregisteredId_ReturnsNull()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act
		var result = registry.GetById(SerializerIds.MemoryPack);

		// Assert
		result.ShouldBeNull();
	}

	#endregion GetById Tests

	#region GetByName Tests

	[Fact]
	public void GetByName_WithRegisteredName_ReturnsSerializer()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act
		var result = registry.GetByName("TestSerializer");

		// Assert
		result.ShouldBe(serializer);
	}

	[Fact]
	public void GetByName_WithUnregisteredName_ReturnsNull()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act
		var result = registry.GetByName("NonExistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetByName_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.GetByName(null!));
	}

	[Fact]
	public void GetByName_IsCaseSensitive()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act
		var result = registry.GetByName("testserializer"); // lowercase

		// Assert
		result.ShouldBeNull(); // Case-sensitive, so should not match
	}

	#endregion GetByName Tests

	#region GetAll Tests

	[Fact]
	public void GetAll_WithNoSerializers_ReturnsEmptyCollection()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act
		var result = registry.GetAll();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetAll_WithMultipleSerializers_ReturnsAllRegistered()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("First");
		var serializer2 = CreateFakeSerializer("Second");
		var serializer3 = CreateFakeSerializer("Third");
		registry.Register(SerializerIds.MemoryPack, serializer1);
		registry.Register(SerializerIds.SystemTextJson, serializer2);
		registry.Register(SerializerIds.MessagePack, serializer3);

		// Act
		var result = registry.GetAll();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldContain(r => r.Id == SerializerIds.MemoryPack && r.Name == "First");
		result.ShouldContain(r => r.Id == SerializerIds.SystemTextJson && r.Name == "Second");
		result.ShouldContain(r => r.Id == SerializerIds.MessagePack && r.Name == "Third");
	}

	#endregion GetAll Tests

	#region IsRegistered Tests

	[Fact]
	public void IsRegisteredById_WithRegisteredId_ReturnsTrue()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act & Assert
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
	}

	[Fact]
	public void IsRegisteredById_WithUnregisteredId_ReturnsFalse()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeFalse();
	}

	[Fact]
	public void IsRegisteredByName_WithRegisteredName_ReturnsTrue()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act & Assert
		registry.IsRegistered("TestSerializer").ShouldBeTrue();
	}

	[Fact]
	public void IsRegisteredByName_WithUnregisteredName_ReturnsFalse()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		registry.IsRegistered("NonExistent").ShouldBeFalse();
	}

	[Fact]
	public void IsRegisteredByName_WithNullOrEmpty_ReturnsFalse()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		registry.IsRegistered(null!).ShouldBeFalse();
		registry.IsRegistered("").ShouldBeFalse();
		registry.IsRegistered("   ").ShouldBeFalse();
	}

	#endregion IsRegistered Tests

	#region Thread-Safety Tests

	[Fact]
	public void ConcurrentRegistration_WithDifferentIds_AllSucceed()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var exceptions = new List<Exception>();
		var threads = new Thread[10];

		// Act - Register 10 different serializers concurrently
		for (var i = 0; i < 10; i++)
		{
			var id = (byte)(i + 10); // Use IDs 10-19
			var name = $"Serializer{i}";
			threads[i] = new Thread(() =>
			{
				try
				{
					var serializer = CreateFakeSerializer(name);
					registry.Register(id, serializer);
				}
				catch (Exception ex)
				{
					lock (exceptions)
					{
						exceptions.Add(ex);
					}
				}
			});
		}

		foreach (var thread in threads)
		{
			thread.Start();
		}

		foreach (var thread in threads)
		{
			thread.Join();
		}

		// Assert
		exceptions.ShouldBeEmpty();
		registry.GetAll().Count.ShouldBe(10);
	}

	[Fact]
	public void ConcurrentGetCurrent_ReturnsConsistentValue()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);
		registry.SetCurrent("TestSerializer");

		var results = new List<(byte Id, IPluggableSerializer Serializer)>();
		var threads = new Thread[100];

		// Act - Read current serializer from 100 threads concurrently
		for (var i = 0; i < 100; i++)
		{
			threads[i] = new Thread(() =>
			{
				var result = registry.GetCurrent();
				lock (results)
				{
					results.Add(result);
				}
			});
		}

		foreach (var thread in threads)
		{
			thread.Start();
		}

		foreach (var thread in threads)
		{
			thread.Join();
		}

		// Assert - All results should be consistent
		results.Count.ShouldBe(100);
		results.All(r => r.Id == SerializerIds.MemoryPack).ShouldBeTrue();
		results.All(r => r.Serializer == serializer).ShouldBeTrue();
	}

	[Fact]
	public async Task ConcurrentReadAndWrite_MaintainsConsistency()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("First");
		var serializer2 = CreateFakeSerializer("Second");
		registry.Register(SerializerIds.MemoryPack, serializer1);
		registry.Register(SerializerIds.SystemTextJson, serializer2);
		registry.SetCurrent("First");

		const int iterationsPerTask = 1_000;
		var tasks = new List<Task>();

		// Act - Concurrent reads (10 tasks, fixed iteration count)
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < iterationsPerTask; j++)
				{
					var (id, _) = registry.GetCurrent();
					(id == SerializerIds.MemoryPack || id == SerializerIds.SystemTextJson).ShouldBeTrue();
				}
			}));
		}

		// Concurrent writes (5 tasks, fixed iteration count)
		for (var i = 0; i < 5; i++)
		{
			var toggle = i % 2 == 0;
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < iterationsPerTask; j++)
				{
					registry.SetCurrent(toggle ? "First" : "Second");
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - No exceptions thrown, test completed successfully
		// If we got here without deadlock or exception, the test passed
	}

	#endregion Thread-Safety Tests

	#region Additional Edge Case Tests

	[Fact]
	public void GetByName_WithWhitespaceOnlyName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.GetByName("   "));
	}

	[Fact]
	public void SetCurrent_WithWhitespaceOnlyName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.SetCurrent("   "));
	}

	[Fact]
	public void GetAll_ReturnsNewCollection_NotInternalReference()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("TestSerializer");
		registry.Register(SerializerIds.MemoryPack, serializer);

		// Act
		var result1 = registry.GetAll();
		var result2 = registry.GetAll();

		// Assert - Should be different collection instances
		ReferenceEquals(result1, result2).ShouldBeFalse();
		result1.Count.ShouldBe(result2.Count);
	}

	[Fact]
	public void Register_WithWhitespaceOnlyName_ThrowsArgumentException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer("   "); // Whitespace-only name

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			registry.Register(SerializerIds.MemoryPack, serializer));
		ex.Message.ShouldContain("null or whitespace");
	}

	[Fact]
	public void Register_MultipleSerializers_MaintainsCorrectOrder()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer1 = CreateFakeSerializer("Alpha");
		var serializer2 = CreateFakeSerializer("Beta");
		var serializer3 = CreateFakeSerializer("Gamma");

		// Act
		registry.Register(5, serializer1);
		registry.Register(3, serializer2);
		registry.Register(10, serializer3);

		// Assert - GetAll should return all three
		var all = registry.GetAll();
		all.Count.ShouldBe(3);
		all.ShouldContain(r => r.Id == 5 && r.Name == "Alpha");
		all.ShouldContain(r => r.Id == 3 && r.Name == "Beta");
		all.ShouldContain(r => r.Id == 10 && r.Name == "Gamma");
	}

	[Theory]
	[InlineData((byte)1)]   // MemoryPack
	[InlineData((byte)2)]   // SystemTextJson
	[InlineData((byte)3)]   // MessagePack
	[InlineData((byte)4)]   // Protobuf
	[InlineData((byte)200)] // CustomRangeStart
	[InlineData((byte)254)] // CustomRangeEnd
	public void Register_WithAllValidIds_Succeeds(byte validId)
	{
		// Arrange
		var registry = new SerializerRegistry();
		var serializer = CreateFakeSerializer($"Serializer_{validId}");

		// Act
		registry.Register(validId, serializer);

		// Assert
		registry.IsRegistered(validId).ShouldBeTrue();
	}

	#endregion Additional Edge Case Tests

	#region Helper Methods

	private static IPluggableSerializer CreateFakeSerializer(string name)
	{
		var fake = A.Fake<IPluggableSerializer>();
		_ = A.CallTo(() => fake.Name).Returns(name);
		_ = A.CallTo(() => fake.Version).Returns("1.0.0");
		return fake;
	}

	#endregion Helper Methods
}
