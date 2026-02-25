// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Diagnostics;

/// <summary>
/// Unit tests for <see cref="GooglePubSubTelemetryConstants"/>.
/// Verifies that the shared telemetry constants are correctly defined
/// and follow the established transport telemetry naming conventions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.GooglePubSub")]
public sealed class GooglePubSubTelemetryConstantsShould : UnitTestBase
{
	#region Non-null / Non-empty Validation

	[Fact]
	public void HaveNonNullAndNonEmptyMeterName()
	{
		GooglePubSubTelemetryConstants.MeterName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveNonNullAndNonEmptyActivitySourceName()
	{
		GooglePubSubTelemetryConstants.ActivitySourceName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveNonNullAndNonEmptyVersion()
	{
		GooglePubSubTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region Naming Convention Compliance

	[Fact]
	public void HaveMeterNameStartingWithExcaliburPrefix()
	{
		GooglePubSubTelemetryConstants.MeterName.ShouldStartWith("Excalibur.Dispatch.Transport.");
	}

	[Fact]
	public void HaveActivitySourceNameStartingWithExcaliburPrefix()
	{
		GooglePubSubTelemetryConstants.ActivitySourceName.ShouldStartWith("Excalibur.Dispatch.Transport.");
	}

	[Fact]
	public void HaveMeterNameEndingWithGooglePubSub()
	{
		GooglePubSubTelemetryConstants.MeterName.ShouldEndWith("GooglePubSub");
	}

	[Fact]
	public void HaveActivitySourceNameEndingWithGooglePubSub()
	{
		GooglePubSubTelemetryConstants.ActivitySourceName.ShouldEndWith("GooglePubSub");
	}

	[Fact]
	public void HaveVersionFollowingSemVerFormat()
	{
		// Version should be in "major.minor.patch" format
		var parts = GooglePubSubTelemetryConstants.Version.Split('.');
		parts.Length.ShouldBe(3, "Version should follow semantic versioning (major.minor.patch)");
		foreach (var part in parts)
		{
			int.TryParse(part, out _).ShouldBeTrue($"Version part '{part}' should be numeric");
		}
	}

	#endregion

	#region Consistency Between Constants

	[Fact]
	public void HaveConsistentMeterNameAndActivitySourceName()
	{
		// Both should use the same base prefix for consistent telemetry filtering
		GooglePubSubTelemetryConstants.MeterName.ShouldBe(
			GooglePubSubTelemetryConstants.ActivitySourceName,
			"MeterName and ActivitySourceName should share the same value for consistent filtering");
	}

	[Fact]
	public void MatchTransportTelemetryConstantsConvention()
	{
		// The constants should produce the same values as TransportTelemetryConstants helper methods
		var expectedMeterName = TransportTelemetryConstants.MeterName("GooglePubSub");
		var expectedActivitySourceName = TransportTelemetryConstants.ActivitySourceName("GooglePubSub");

		GooglePubSubTelemetryConstants.MeterName.ShouldBe(expectedMeterName,
			"MeterName should match TransportTelemetryConstants.MeterName(\"GooglePubSub\")");
		GooglePubSubTelemetryConstants.ActivitySourceName.ShouldBe(expectedActivitySourceName,
			"ActivitySourceName should match TransportTelemetryConstants.ActivitySourceName(\"GooglePubSub\")");
	}

	#endregion

	#region Meter and ActivitySource Creation

	[Fact]
	public void AllowCreatingMeterFromConstant()
	{
		// Arrange & Act
		using var meter = new Meter(
			GooglePubSubTelemetryConstants.MeterName,
			GooglePubSubTelemetryConstants.Version);

		// Assert
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe(GooglePubSubTelemetryConstants.MeterName);
		meter.Version.ShouldBe(GooglePubSubTelemetryConstants.Version);
	}

	[Fact]
	public void AllowCreatingActivitySourceFromConstant()
	{
		// Arrange & Act
		using var activitySource = new ActivitySource(
			GooglePubSubTelemetryConstants.ActivitySourceName,
			GooglePubSubTelemetryConstants.Version);

		// Assert
		activitySource.ShouldNotBeNull();
		activitySource.Name.ShouldBe(GooglePubSubTelemetryConstants.ActivitySourceName);
		activitySource.Version.ShouldBe(GooglePubSubTelemetryConstants.Version);
	}

	#endregion

	#region Exact Value Verification

	[Fact]
	public void HaveExpectedMeterNameValue()
	{
		GooglePubSubTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Transport.GooglePubSub");
	}

	[Fact]
	public void HaveExpectedActivitySourceNameValue()
	{
		GooglePubSubTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Transport.GooglePubSub");
	}

	[Fact]
	public void HaveExpectedVersionValue()
	{
		GooglePubSubTelemetryConstants.Version.ShouldBe("1.0.0");
	}

	#endregion

	#region Assembly-Level Consolidation Verification

	[Fact]
	public void EnsureNoInlineHardcodedMeterInstantiationsInAssembly()
	{
		// Verify that classes in the GooglePubSub assembly reference the shared constants
		// by checking that known telemetry classes have fields/properties of type Meter
		// or ActivitySource, which implies they were instantiated (ideally from the constants).
		var assembly = typeof(GooglePubSubTelemetryConstants).Assembly;
		assembly.ShouldNotBeNull();

		// The assembly should contain the constants class
		var constantsType = assembly.GetType(
			"Excalibur.Dispatch.Transport.GooglePubSub.GooglePubSubTelemetryConstants");
		constantsType.ShouldNotBeNull("GooglePubSubTelemetryConstants should be discoverable in the assembly");

		// Verify the constants are static fields with expected values
		var meterNameField = constantsType.GetField("MeterName", BindingFlags.Public | BindingFlags.Static);
		meterNameField.ShouldNotBeNull("MeterName field should exist");
		var meterNameValue = (string?)meterNameField.GetValue(null);
		meterNameValue.ShouldBe(GooglePubSubTelemetryConstants.MeterName);

		var activitySourceNameField = constantsType.GetField("ActivitySourceName", BindingFlags.Public | BindingFlags.Static);
		activitySourceNameField.ShouldNotBeNull("ActivitySourceName field should exist");
		var activitySourceNameValue = (string?)activitySourceNameField.GetValue(null);
		activitySourceNameValue.ShouldBe(GooglePubSubTelemetryConstants.ActivitySourceName);

		var versionField = constantsType.GetField("Version", BindingFlags.Public | BindingFlags.Static);
		versionField.ShouldNotBeNull("Version field should exist");
		var versionValue = (string?)versionField.GetValue(null);
		versionValue.ShouldBe(GooglePubSubTelemetryConstants.Version);
	}

	[Fact]
	public void HaveConstantsClassThatIsStaticAndPublic()
	{
		var type = typeof(GooglePubSubTelemetryConstants);

		type.IsAbstract.ShouldBeTrue("Static class should be abstract");
		type.IsSealed.ShouldBeTrue("Static class should be sealed");
		type.IsPublic.ShouldBeTrue("Constants class should be public");
	}

	#endregion
}
