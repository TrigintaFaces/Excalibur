// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using MessageVersionMetadata = Excalibur.Dispatch.Delivery.MessageVersionMetadata;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="MessageVersionMetadata"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageVersionMetadataShould
{
	[Fact]
	public void ImplementIMessageVersionMetadata()
	{
		// Arrange & Act
		var metadata = new MessageVersionMetadata();

		// Assert
		metadata.ShouldBeAssignableTo<IMessageVersionMetadata>();
	}

	[Fact]
	public void HaveDefaultZeroValues()
	{
		// Arrange & Act
		var metadata = new MessageVersionMetadata();

		// Assert
		metadata.SchemaVersion.ShouldBe(0);
		metadata.SerializerVersion.ShouldBe(0);
		metadata.Version.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingSchemaVersion()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SchemaVersion = 3;

		// Assert
		metadata.SchemaVersion.ShouldBe(3);
	}

	[Fact]
	public void AllowSettingSerializerVersion()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SerializerVersion = 2;

		// Assert
		metadata.SerializerVersion.ShouldBe(2);
	}

	[Fact]
	public void AllowSettingVersion()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.Version = 5;

		// Assert
		metadata.Version.ShouldBe(5);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var metadata = new MessageVersionMetadata
		{
			SchemaVersion = 1,
			SerializerVersion = 2,
			Version = 3,
		};

		// Assert
		metadata.SchemaVersion.ShouldBe(1);
		metadata.SerializerVersion.ShouldBe(2);
		metadata.Version.ShouldBe(3);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void AcceptVariousSchemaVersionValues(int version)
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SchemaVersion = version;

		// Assert
		metadata.SchemaVersion.ShouldBe(version);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void AcceptVariousSerializerVersionValues(int version)
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SerializerVersion = version;

		// Assert
		metadata.SerializerVersion.ShouldBe(version);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void AcceptVariousVersionValues(int version)
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.Version = version;

		// Assert
		metadata.Version.ShouldBe(version);
	}

	[Fact]
	public void TrackTypicalVersionEvolution()
	{
		// Arrange & Act - Track a message through version evolution
		var v1Metadata = new MessageVersionMetadata
		{
			SchemaVersion = 1,
			SerializerVersion = 1,
			Version = 1,
		};

		var v2Metadata = new MessageVersionMetadata
		{
			SchemaVersion = 2,
			SerializerVersion = 1,
			Version = 2,
		};

		// Assert - Version 2 should have higher schema version
		v2Metadata.SchemaVersion.ShouldBeGreaterThan(v1Metadata.SchemaVersion);
		v2Metadata.Version.ShouldBeGreaterThan(v1Metadata.Version);
	}
}
