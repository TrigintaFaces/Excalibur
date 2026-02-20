// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.Options.CloudEvents;

/// <summary>
/// Unit tests for <see cref="CloudEventOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CloudEventOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Mode_IsStructured()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public void Default_SpecVersion_IsV1_0()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.SpecVersion.ShouldBe(CloudEventsSpecVersion.V1_0);
	}

	[Fact]
	public void Default_DefaultSource_IsUrnDispatch()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.DefaultSource.ToString().ShouldBe("urn:dispatch");
	}

	[Fact]
	public void Default_ValidateSchema_IsTrue()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.ValidateSchema.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeSchemaVersion_IsTrue()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.IncludeSchemaVersion.ShouldBeTrue();
	}

	[Fact]
	public void Default_AutoRegisterSchemas_IsFalse()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.AutoRegisterSchemas.ShouldBeFalse();
	}

	[Fact]
	public void Default_SchemaProvider_IsNull()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.SchemaProvider.ShouldBeNull();
	}

	[Fact]
	public void Default_PreserveEnvelopeProperties_IsTrue()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.PreserveEnvelopeProperties.ShouldBeTrue();
	}

	[Fact]
	public void Default_DispatchExtensionPrefix_IsDispatch()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.DispatchExtensionPrefix.ShouldBe("dispatch");
	}

	[Fact]
	public void Default_UseCompression_IsFalse()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.UseCompression.ShouldBeFalse();
	}

	[Fact]
	public void Default_CompressionThreshold_Is1024()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.CompressionThreshold.ShouldBe(1024);
	}

	[Fact]
	public void Default_EnableDoDCompliance_IsFalse()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.EnableDoDCompliance.ShouldBeFalse();
	}

	[Fact]
	public void Default_DefaultMode_IsStructured()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		options.DefaultMode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public void Default_ExcludedExtensions_IsEmpty()
	{
		// Arrange & Act
		var options = new CloudEventOptions();

		// Assert
		_ = options.ExcludedExtensions.ShouldNotBeNull();
		options.ExcludedExtensions.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Mode_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.Mode = CloudEventMode.Binary;

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public void DefaultSource_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.DefaultSource = new Uri("https://example.com/events");

		// Assert
		options.DefaultSource.ToString().ShouldBe("https://example.com/events");
	}

	[Fact]
	public void ValidateSchema_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.ValidateSchema = false;

		// Assert
		options.ValidateSchema.ShouldBeFalse();
	}

	[Fact]
	public void UseCompression_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.UseCompression = true;

		// Assert
		options.UseCompression.ShouldBeTrue();
	}

	[Fact]
	public void CompressionThreshold_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.CompressionThreshold = 4096;

		// Assert
		options.CompressionThreshold.ShouldBe(4096);
	}

	[Fact]
	public void EnableDoDCompliance_CanBeSet()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.EnableDoDCompliance = true;

		// Assert
		options.EnableDoDCompliance.ShouldBeTrue();
	}

	[Fact]
	public void ExcludedExtensions_CanAddItems()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		_ = options.ExcludedExtensions.Add("sensitivedata");
		_ = options.ExcludedExtensions.Add("internalfield");

		// Assert
		options.ExcludedExtensions.Count.ShouldBe(2);
		options.ExcludedExtensions.ShouldContain("sensitivedata");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new CloudEventOptions
		{
			Mode = CloudEventMode.Binary,
			ValidateSchema = false,
			UseCompression = true,
			CompressionThreshold = 2048,
			EnableDoDCompliance = true,
			DispatchExtensionPrefix = "custom",
		};

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Binary);
		options.ValidateSchema.ShouldBeFalse();
		options.UseCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBe(2048);
		options.EnableDoDCompliance.ShouldBeTrue();
		options.DispatchExtensionPrefix.ShouldBe("custom");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForDoDCompliance_EnablesStrictValidation()
	{
		// Act
		var options = new CloudEventOptions
		{
			EnableDoDCompliance = true,
			ValidateSchema = true,
			PreserveEnvelopeProperties = true,
		};

		// Assert
		options.EnableDoDCompliance.ShouldBeTrue();
		options.ValidateSchema.ShouldBeTrue();
		options.PreserveEnvelopeProperties.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForBinaryMode_UsesBinaryTransport()
	{
		// Act
		var options = new CloudEventOptions
		{
			Mode = CloudEventMode.Binary,
			DefaultMode = CloudEventMode.Binary,
		};

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Binary);
		options.DefaultMode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public void Options_ForCompression_EnablesLargePayloadHandling()
	{
		// Act
		var options = new CloudEventOptions
		{
			UseCompression = true,
			CompressionThreshold = 512,
		};

		// Assert
		options.UseCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBeLessThan(1024);
	}

	#endregion
}
