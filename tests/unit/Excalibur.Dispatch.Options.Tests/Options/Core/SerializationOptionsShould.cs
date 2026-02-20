// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="SerializationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SerializationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_EmbedMessageType_IsTrue()
	{
		// Arrange & Act
		var options = new SerializationOptions();

		// Assert
		options.EmbedMessageType.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeAssemblyInfo_IsFalse()
	{
		// Arrange & Act
		var options = new SerializationOptions();

		// Assert
		options.IncludeAssemblyInfo.ShouldBeFalse();
	}

	[Fact]
	public void Default_DefaultBufferSize_IsFourtyNinetySix()
	{
		// Arrange & Act
		var options = new SerializationOptions();

		// Assert
		options.DefaultBufferSize.ShouldBe(4096);
	}

	[Fact]
	public void Default_UseBufferPooling_IsTrue()
	{
		// Arrange & Act
		var options = new SerializationOptions();

		// Assert
		options.UseBufferPooling.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void EmbedMessageType_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.EmbedMessageType = false;

		// Assert
		options.EmbedMessageType.ShouldBeFalse();
	}

	[Fact]
	public void IncludeAssemblyInfo_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.IncludeAssemblyInfo = true;

		// Assert
		options.IncludeAssemblyInfo.ShouldBeTrue();
	}

	[Fact]
	public void DefaultBufferSize_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.DefaultBufferSize = 8192;

		// Assert
		options.DefaultBufferSize.ShouldBe(8192);
	}

	[Fact]
	public void UseBufferPooling_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.UseBufferPooling = false;

		// Assert
		options.UseBufferPooling.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SerializationOptions
		{
			EmbedMessageType = false,
			IncludeAssemblyInfo = true,
			DefaultBufferSize = 16384,
			UseBufferPooling = false,
		};

		// Assert
		options.EmbedMessageType.ShouldBeFalse();
		options.IncludeAssemblyInfo.ShouldBeTrue();
		options.DefaultBufferSize.ShouldBe(16384);
		options.UseBufferPooling.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForCrossAssemblyDeserialization_IncludesAssemblyInfo()
	{
		// Act
		var options = new SerializationOptions
		{
			EmbedMessageType = true,
			IncludeAssemblyInfo = true,
		};

		// Assert
		options.EmbedMessageType.ShouldBeTrue();
		options.IncludeAssemblyInfo.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForThroughput_UsesPooling()
	{
		// Act
		var options = new SerializationOptions
		{
			UseBufferPooling = true,
			DefaultBufferSize = 8192,
		};

		// Assert
		options.UseBufferPooling.ShouldBeTrue();
		options.DefaultBufferSize.ShouldBeGreaterThan(4096);
	}

	[Fact]
	public void Options_ForLowMemory_HasSmallBufferSize()
	{
		// Act
		var options = new SerializationOptions
		{
			DefaultBufferSize = 1024,
			UseBufferPooling = true,
		};

		// Assert
		options.DefaultBufferSize.ShouldBeLessThan(4096);
	}

	#endregion
}
