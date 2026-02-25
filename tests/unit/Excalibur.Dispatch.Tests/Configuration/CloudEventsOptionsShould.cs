// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventsOptionsShould
{
	// --- CloudEventOptions ---

	[Fact]
	public void CloudEventOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CloudEventOptions();

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Structured);
		options.SpecVersion.ShouldBe(CloudEventsSpecVersion.V1_0);
		options.DefaultSource.ShouldBe(new Uri("urn:dispatch"));
		options.ValidateSchema.ShouldBeTrue();
		options.IncludeSchemaVersion.ShouldBeTrue();
		options.AutoRegisterSchemas.ShouldBeFalse();
		options.SchemaProvider.ShouldBeNull();
		options.SchemaVersionProvider.ShouldBeNull();
		options.CustomValidator.ShouldBeNull();
		options.OutgoingTransformer.ShouldBeNull();
		options.ExcludedExtensions.ShouldNotBeNull();
		options.ExcludedExtensions.ShouldBeEmpty();
		options.PreserveEnvelopeProperties.ShouldBeTrue();
		options.DispatchExtensionPrefix.ShouldBe("dispatch");
		options.UseCompression.ShouldBeFalse();
		options.CompressionThreshold.ShouldBe(1024);
		options.EnableDoDCompliance.ShouldBeFalse();
		options.DefaultMode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public void CloudEventOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CloudEventOptions
		{
			Mode = CloudEventMode.Binary,
			SpecVersion = CloudEventsSpecVersion.V1_0,
			DefaultSource = new Uri("https://example.com/events"),
			ValidateSchema = false,
			IncludeSchemaVersion = false,
			AutoRegisterSchemas = true,
			SchemaProvider = _ => "schema",
			SchemaVersionProvider = _ => "1.0",
			PreserveEnvelopeProperties = false,
			DispatchExtensionPrefix = "custom",
			UseCompression = true,
			CompressionThreshold = 2048,
			EnableDoDCompliance = true,
			DefaultMode = CloudEventMode.Binary,
		};

		// Assert
		options.Mode.ShouldBe(CloudEventMode.Binary);
		options.DefaultSource.ShouldBe(new Uri("https://example.com/events"));
		options.ValidateSchema.ShouldBeFalse();
		options.IncludeSchemaVersion.ShouldBeFalse();
		options.AutoRegisterSchemas.ShouldBeTrue();
		options.SchemaProvider.ShouldNotBeNull();
		options.SchemaVersionProvider.ShouldNotBeNull();
		options.PreserveEnvelopeProperties.ShouldBeFalse();
		options.DispatchExtensionPrefix.ShouldBe("custom");
		options.UseCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBe(2048);
		options.EnableDoDCompliance.ShouldBeTrue();
		options.DefaultMode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public void CloudEventOptions_ExcludedExtensions_CanAddEntries()
	{
		// Arrange
		var options = new CloudEventOptions();

		// Act
		options.ExcludedExtensions.Add("traceid");
		options.ExcludedExtensions.Add("tenantid");

		// Assert
		options.ExcludedExtensions.Count.ShouldBe(2);
		options.ExcludedExtensions.ShouldContain("traceid");
		options.ExcludedExtensions.ShouldContain("tenantid");
	}

	[Fact]
	public void CloudEventOptions_FuncDelegates_AreInvokable()
	{
		// Arrange
		var options = new CloudEventOptions
		{
			SchemaProvider = t => $"schema-{t.Name}",
			SchemaVersionProvider = t => $"v1-{t.Name}",
		};

		// Act & Assert
		options.SchemaProvider!(typeof(string)).ShouldBe("schema-String");
		options.SchemaVersionProvider!(typeof(int)).ShouldBe("v1-Int32");
	}

	// --- CloudEventBatchOptions ---

	[Fact]
	public void CloudEventBatchOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CloudEventBatchOptions();

		// Assert
		options.MaxEvents.ShouldBe(100);
		options.MaxBatchSizeBytes.ShouldBe(1024L * 1024);
		options.InitialCapacity.ShouldBe(10);
	}

	[Fact]
	public void CloudEventBatchOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CloudEventBatchOptions
		{
			MaxEvents = 500,
			MaxBatchSizeBytes = 2 * 1024 * 1024,
			InitialCapacity = 50,
		};

		// Assert
		options.MaxEvents.ShouldBe(500);
		options.MaxBatchSizeBytes.ShouldBe(2L * 1024 * 1024);
		options.InitialCapacity.ShouldBe(50);
	}

	// --- CloudEventMode ---

	[Fact]
	public void CloudEventMode_HaveExpectedValues()
	{
		// Assert
		CloudEventMode.Structured.ShouldBe((CloudEventMode)0);
		CloudEventMode.Binary.ShouldBe((CloudEventMode)1);
	}

	[Fact]
	public void CloudEventMode_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<CloudEventMode>();

		// Assert
		values.Length.ShouldBe(2);
	}
}
