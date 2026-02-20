// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Serialization;

namespace Excalibur.Dispatch.Tests.Options.Serialization;

/// <summary>
/// Unit tests for <see cref="MessageSerializerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessageSerializerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_SerializerMap_IsNotNull()
	{
		// Arrange & Act
		var options = new MessageSerializerOptions();

		// Assert
		_ = options.SerializerMap.ShouldNotBeNull();
	}

	[Fact]
	public void Default_SerializerMap_IsEmpty()
	{
		// Arrange & Act
		var options = new MessageSerializerOptions();

		// Assert
		options.SerializerMap.ShouldBeEmpty();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void SerializerMap_CanAddItems()
	{
		// Arrange
		var options = new MessageSerializerOptions();

		// Act
		options.SerializerMap[1] = typeof(object);

		// Assert
		options.SerializerMap.Count.ShouldBe(1);
		options.SerializerMap[1].ShouldBe(typeof(object));
	}

	[Fact]
	public void SerializerMap_CanAddMultipleVersions()
	{
		// Arrange
		var options = new MessageSerializerOptions();

		// Act
		options.SerializerMap[1] = typeof(string);
		options.SerializerMap[2] = typeof(int);
		options.SerializerMap[3] = typeof(object);

		// Assert
		options.SerializerMap.Count.ShouldBe(3);
	}

	[Fact]
	public void SerializerMap_CanUpdateExistingVersion()
	{
		// Arrange
		var options = new MessageSerializerOptions();
		options.SerializerMap[1] = typeof(string);

		// Act
		options.SerializerMap[1] = typeof(int);

		// Assert
		options.SerializerMap[1].ShouldBe(typeof(int));
	}

	[Fact]
	public void SerializerMap_CanCheckContainsKey()
	{
		// Arrange
		var options = new MessageSerializerOptions();
		options.SerializerMap[1] = typeof(string);

		// Act & Assert
		options.SerializerMap.ContainsKey(1).ShouldBeTrue();
		options.SerializerMap.ContainsKey(2).ShouldBeFalse();
	}

	[Fact]
	public void SerializerMap_CanRemoveItems()
	{
		// Arrange
		var options = new MessageSerializerOptions();
		options.SerializerMap[1] = typeof(string);

		// Act
		_ = options.SerializerMap.Remove(1);

		// Assert
		options.SerializerMap.ShouldBeEmpty();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForVersionedSerializers_RegistersMultipleVersions()
	{
		// Arrange
		var options = new MessageSerializerOptions();

		// Act - Register serializers for different versions
		options.SerializerMap[1] = typeof(object); // V1 serializer
		options.SerializerMap[2] = typeof(object); // V2 serializer

		// Assert
		options.SerializerMap.Count.ShouldBe(2);
		options.SerializerMap.ContainsKey(1).ShouldBeTrue();
		options.SerializerMap.ContainsKey(2).ShouldBeTrue();
	}

	[Fact]
	public void Options_ForLatestVersion_ReturnsCorrectSerializer()
	{
		// Arrange
		var options = new MessageSerializerOptions();
		options.SerializerMap[1] = typeof(string);
		options.SerializerMap[2] = typeof(int);
		options.SerializerMap[3] = typeof(object);

		// Act
		var latestVersion = options.SerializerMap.Keys.Max();
		var latestSerializer = options.SerializerMap[latestVersion];

		// Assert
		latestVersion.ShouldBe(3);
		latestSerializer.ShouldBe(typeof(object));
	}

	[Fact]
	public void Options_CanTryGetSerializer()
	{
		// Arrange
		var options = new MessageSerializerOptions();
		options.SerializerMap[1] = typeof(string);

		// Act
		var exists = options.SerializerMap.TryGetValue(1, out var serializer);

		// Assert
		exists.ShouldBeTrue();
		serializer.ShouldBe(typeof(string));
	}

	#endregion
}
