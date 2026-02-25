// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Tests for <see cref="ProtobufSerializationOptions"/>.
/// </summary>
/// <remarks>
/// Per T10.*, these tests verify:
/// - Default values are sensible and documented
/// - Property setters work correctly
/// - Options can be configured via Options pattern
/// - Wire format enum values are valid
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ProtobufSerializationOptionsShould
{
	[Fact]
	public void Have_Binary_As_Default_Wire_Format()
	{
		// Arrange & Act
		var options = new ProtobufSerializationOptions();

		// Assert
		options.WireFormat.ShouldBe(ProtobufWireFormat.Binary);
	}

	[Fact]
	public void Allow_Setting_WireFormat_To_Binary()
	{
		// Arrange
		var options = new ProtobufSerializationOptions();

		// Act
		options.WireFormat = ProtobufWireFormat.Binary;

		// Assert
		options.WireFormat.ShouldBe(ProtobufWireFormat.Binary);
	}

	[Fact]
	public void Allow_Setting_WireFormat_To_Json()
	{
		// Arrange
		var options = new ProtobufSerializationOptions();

		// Act
		options.WireFormat = ProtobufWireFormat.Json;

		// Assert
		options.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Support_Object_Initializer_Syntax()
	{
		// Arrange & Act
		var options = new ProtobufSerializationOptions
		{
			WireFormat = ProtobufWireFormat.Json,
		};

		// Assert
		options.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Be_Mutable_After_Construction()
	{
		// Arrange
		var options = new ProtobufSerializationOptions
		{
			WireFormat = ProtobufWireFormat.Binary,
		};

		// Act
		options.WireFormat = ProtobufWireFormat.Json;

		// Assert
		options.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Support_Multiple_Independent_Instances()
	{
		// Arrange
		var options1 = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Binary };
		var options2 = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };

		// Act & Assert
		options1.WireFormat.ShouldBe(ProtobufWireFormat.Binary);
		options2.WireFormat.ShouldBe(ProtobufWireFormat.Json);
		// Changing one should not affect the other
		options1.WireFormat = ProtobufWireFormat.Json;
		options2.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Work_With_Options_Pattern()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.Configure<ProtobufSerializationOptions>(options =>
		{
			options.WireFormat = ProtobufWireFormat.Json;
		});

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<ProtobufSerializationOptions>>();

		// Assert
		options.Value.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Support_Named_Options()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.Configure<ProtobufSerializationOptions>("BinaryConfig", options =>
		{
			options.WireFormat = ProtobufWireFormat.Binary;
		});
		_ = services.Configure<ProtobufSerializationOptions>("JsonConfig", options =>
		{
			options.WireFormat = ProtobufWireFormat.Json;
		});

		var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ProtobufSerializationOptions>>();

		// Act
		var binaryOptions = optionsMonitor.Get("BinaryConfig");
		var jsonOptions = optionsMonitor.Get("JsonConfig");

		// Assert
		binaryOptions.WireFormat.ShouldBe(ProtobufWireFormat.Binary);
		jsonOptions.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}
}
