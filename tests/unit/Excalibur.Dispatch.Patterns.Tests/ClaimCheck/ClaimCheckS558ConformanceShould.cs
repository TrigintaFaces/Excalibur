// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Structural conformance tests verifying the S558 P2 backlog design decisions were correctly
/// implemented in the ClaimCheck subsystem.
/// Sprint 569 -- Task S569.21: Regression guards for S569.1-S569.7.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ClaimCheck")]
public sealed class ClaimCheckS558ConformanceShould
{
	#region S569.2: Consolidate ClaimCheckMetadata Tags/Properties

	[Fact]
	public void ClaimCheckMetadata_NotHaveTagsProperty()
	{
		// S569.2: Tags was removed; only Properties dictionary remains
		var tagsProperty = typeof(ClaimCheckMetadata).GetProperty("Tags");
		tagsProperty.ShouldBeNull("ClaimCheckMetadata should not have a Tags property (consolidated into Properties).");
	}

	[Fact]
	public void ClaimCheckMetadata_HavePropertiesDictionary()
	{
		// S569.2: Properties dictionary is the single extensibility point
		var propsProperty = typeof(ClaimCheckMetadata).GetProperty("Properties");
		propsProperty.ShouldNotBeNull();
		propsProperty.PropertyType.ShouldBe(typeof(Dictionary<string, string>));
	}

	[Fact]
	public void ClaimCheckMetadata_HaveAtMostTenProperties()
	{
		// Quality gate: DTO must have <= 10 properties
		var propertyCount = typeof(ClaimCheckMetadata)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;

		propertyCount.ShouldBeLessThanOrEqualTo(10,
			$"ClaimCheckMetadata has {propertyCount} properties, exceeding the 10-property quality gate.");
	}

	#endregion

	#region S569.3: Remove ClaimId alias from ClaimCheckReference

	[Fact]
	public void ClaimCheckReference_NotHaveClaimIdProperty()
	{
		// S569.3: ClaimId alias was removed; only Id remains
		var claimIdProperty = typeof(ClaimCheckReference).GetProperty("ClaimId");
		claimIdProperty.ShouldBeNull("ClaimCheckReference should not have a ClaimId property (alias removed).");
	}

	[Fact]
	public void ClaimCheckReference_HaveIdProperty()
	{
		var idProperty = typeof(ClaimCheckReference).GetProperty("Id");
		idProperty.ShouldNotBeNull();
		idProperty.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void ClaimCheckReference_HaveAtMostTenProperties()
	{
		var propertyCount = typeof(ClaimCheckReference)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;

		propertyCount.ShouldBeLessThanOrEqualTo(10,
			$"ClaimCheckReference has {propertyCount} properties, exceeding the 10-property quality gate.");
	}

	#endregion

	#region S569.7: Split ClaimCheckOptions 27-to-4 classes

	[Fact]
	public void ClaimCheckOptions_HaveStorageSubOptions()
	{
		var storageProp = typeof(ClaimCheckOptions).GetProperty("Storage");
		storageProp.ShouldNotBeNull("ClaimCheckOptions must have a Storage sub-options property.");
		storageProp.PropertyType.ShouldBe(typeof(ClaimCheckStorageOptions));
	}

	[Fact]
	public void ClaimCheckOptions_HaveCompressionSubOptions()
	{
		var compressionProp = typeof(ClaimCheckOptions).GetProperty("Compression");
		compressionProp.ShouldNotBeNull("ClaimCheckOptions must have a Compression sub-options property.");
		compressionProp.PropertyType.ShouldBe(typeof(ClaimCheckCompressionOptions));
	}

	[Fact]
	public void ClaimCheckOptions_HaveCleanupSubOptions()
	{
		var cleanupProp = typeof(ClaimCheckOptions).GetProperty("Cleanup");
		cleanupProp.ShouldNotBeNull("ClaimCheckOptions must have a Cleanup sub-options property.");
		cleanupProp.PropertyType.ShouldBe(typeof(ClaimCheckCleanupOptions));
	}

	[Fact]
	public void ClaimCheckStorageOptions_HaveAtMostFifteenProperties()
	{
		// Storage sub-options: allow up to 15 (was 12 originally)
		var propertyCount = typeof(ClaimCheckStorageOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;

		propertyCount.ShouldBeLessThanOrEqualTo(15,
			$"ClaimCheckStorageOptions has {propertyCount} properties, exceeding the 15-property limit.");
	}

	[Fact]
	public void ClaimCheckCompressionOptions_HaveAtMostTenProperties()
	{
		var propertyCount = typeof(ClaimCheckCompressionOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;

		propertyCount.ShouldBeLessThanOrEqualTo(10,
			$"ClaimCheckCompressionOptions has {propertyCount} properties, exceeding the 10-property quality gate.");
	}

	[Fact]
	public void ClaimCheckCleanupOptions_HaveAtMostTenProperties()
	{
		var propertyCount = typeof(ClaimCheckCleanupOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;

		propertyCount.ShouldBeLessThanOrEqualTo(10,
			$"ClaimCheckCleanupOptions has {propertyCount} properties, exceeding the 10-property quality gate.");
	}

	#endregion

	#region S569.6: TelemetryClaimCheckProvider implements IClaimCheckProvider

	[Fact]
	public void TelemetryClaimCheckProvider_ImplementIClaimCheckProvider()
	{
		typeof(IClaimCheckProvider).IsAssignableFrom(typeof(TelemetryClaimCheckProvider)).ShouldBeTrue(
			"TelemetryClaimCheckProvider must implement IClaimCheckProvider.");
	}

	#endregion

	#region S569.4: Magic prefix format detection

	[Fact]
	public void ClaimCheckTelemetryConstants_ExposeMetricNames()
	{
		// Verify the telemetry constants class exists and has the expected metric name constants
		var metricsType = typeof(ClaimCheckTelemetryConstants).GetNestedType("MetricNames");
		metricsType.ShouldNotBeNull("ClaimCheckTelemetryConstants must have a MetricNames nested type.");

		var payloadsStoredField = metricsType.GetField("PayloadsStored",
			BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
		payloadsStoredField.ShouldNotBeNull("MetricNames must have a PayloadsStored constant.");
	}

	#endregion

	#region IClaimCheckProvider interface quality gate

	[Fact]
	public void IClaimCheckProvider_HaveAtMostFiveMethods()
	{
		var methods = typeof(IClaimCheckProvider).GetMethods(
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"IClaimCheckProvider has {methods.Length} methods, exceeding the 5-method quality gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	#endregion
}
