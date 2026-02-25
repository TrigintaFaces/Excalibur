// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;
using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackSerializationOptions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessagePackSerializationOptionsShould : UnitTestBase
{
	[Fact]
	public void Defaults_UseLz4Compression_IsFalse()
	{
		// Arrange & Act
		var options = new MessagePackSerializationOptions();

		// Assert
		options.UseLz4Compression.ShouldBeFalse();
	}

	[Fact]
	public void SetUseLz4Compression_ReturnsUpdatedValue()
	{
		// Arrange
		var options = new MessagePackSerializationOptions();

		// Act
		options.UseLz4Compression = true;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	[Fact]
	public void MessagePackSerializerOptions_WithoutCompression_ReturnsStandard()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = false
		};

		// Act
		var serializerOptions = options.MessagePackSerializerOptions;

		// Assert
		_ = serializerOptions.ShouldNotBeNull();
	}

	[Fact]
	public void MessagePackSerializerOptions_WithCompression_IncludesLz4()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = true
		};

		// Act
		var serializerOptions = options.MessagePackSerializerOptions;

		// Assert
		_ = serializerOptions.ShouldNotBeNull();
		serializerOptions.Compression.ShouldBe(MessagePackCompression.Lz4Block);
	}
}
