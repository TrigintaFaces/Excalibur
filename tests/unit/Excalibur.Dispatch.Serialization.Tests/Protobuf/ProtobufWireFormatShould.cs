// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Unit tests for <see cref="ProtobufWireFormat"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization.Protobuf")]
[Trait("Feature", "Wire Format")]
public sealed class ProtobufWireFormatShould : UnitTestBase
{
	[Fact]
	public void HaveBinaryAsDefaultValue()
	{
		// Assert
		((int)ProtobufWireFormat.Binary).ShouldBe(0);
	}

	[Fact]
	public void HaveJsonValue()
	{
		// Assert
		((int)ProtobufWireFormat.Json).ShouldBe(1);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<ProtobufWireFormat>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData("Binary", ProtobufWireFormat.Binary)]
	[InlineData("Json", ProtobufWireFormat.Json)]
	public void ParseFromString(string name, ProtobufWireFormat expected)
	{
		// Act
		var result = Enum.Parse<ProtobufWireFormat>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(ProtobufWireFormat.Binary).ShouldBeTrue();
		Enum.IsDefined(ProtobufWireFormat.Json).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToBinary()
	{
		// Arrange & Act
		var defaultValue = default(ProtobufWireFormat);

		// Assert
		defaultValue.ShouldBe(ProtobufWireFormat.Binary);
	}
}
