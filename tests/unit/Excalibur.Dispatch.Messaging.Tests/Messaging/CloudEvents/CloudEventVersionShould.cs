// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Unit tests for <see cref="CloudEventVersion"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventVersionShould
{
	[Fact]
	public void HaveSpecVersionConstant()
	{
		// Assert
		CloudEventVersion.SpecVersion.ShouldBe("1.0");
	}

	[Fact]
	public void HaveSchemaVersionAttributeConstant()
	{
		// Assert
		CloudEventVersion.SchemaVersionAttribute.ShouldBe("schemaversion");
	}

	[Fact]
	public void HaveSchemaCompatibilityAttributeConstant()
	{
		// Assert
		CloudEventVersion.SchemaCompatibilityAttribute.ShouldBe("schemacompatibility");
	}

	[Fact]
	public void ThrowOnGetSchemaVersionWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.GetSchemaVersion(null!));
	}

	[Fact]
	public void ReturnNullSchemaVersionWhenNotSet()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var version = cloudEvent.GetSchemaVersion();

		// Assert
		version.ShouldBeNull();
	}

	[Fact]
	public void GetSchemaVersionCorrectly()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent["schemaversion"] = "1.0.0";

		// Act
		var version = cloudEvent.GetSchemaVersion();

		// Assert
		version.ShouldBe("1.0.0");
	}

	[Fact]
	public void ThrowOnSetSchemaVersionWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.SetSchemaVersion(null!, "1.0.0"));
	}

	[Fact]
	public void ThrowOnSetSchemaVersionWithNullVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.SetSchemaVersion(null!));
	}

	[Fact]
	public void ThrowOnSetSchemaVersionWithEmptyVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.SetSchemaVersion(""));
	}

	[Fact]
	public void ThrowOnSetSchemaVersionWithWhitespaceVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.SetSchemaVersion("   "));
	}

	[Fact]
	public void SetSchemaVersionCorrectly()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		cloudEvent.SetSchemaVersion("2.0.0");

		// Assert
		cloudEvent.GetSchemaVersion().ShouldBe("2.0.0");
	}

	[Fact]
	public void ThrowOnGetSchemaCompatibilityWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.GetSchemaCompatibility(null!));
	}

	[Fact]
	public void ReturnNoneCompatibilityWhenNotSet()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var compatibility = cloudEvent.GetSchemaCompatibility();

		// Assert
		compatibility.ShouldBe(SchemaCompatibilityMode.None);
	}

	[Theory]
	[InlineData("NONE", SchemaCompatibilityMode.None)]
	[InlineData("FORWARD", SchemaCompatibilityMode.Forward)]
	[InlineData("BACKWARD", SchemaCompatibilityMode.Backward)]
	[InlineData("FULL", SchemaCompatibilityMode.Full)]
	public void GetSchemaCompatibilityCorrectly(string attributeValue, SchemaCompatibilityMode expected)
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent["schemacompatibility"] = attributeValue;

		// Act
		var compatibility = cloudEvent.GetSchemaCompatibility();

		// Assert
		compatibility.ShouldBe(expected);
	}

	[Theory]
	[InlineData("forward", SchemaCompatibilityMode.Forward)]
	[InlineData("FORWARD", SchemaCompatibilityMode.Forward)]
	[InlineData("backward", SchemaCompatibilityMode.Backward)]
	[InlineData("BACKWARD", SchemaCompatibilityMode.Backward)]
	[InlineData("full", SchemaCompatibilityMode.Full)]
	[InlineData("FULL", SchemaCompatibilityMode.Full)]
	public void GetSchemaCompatibilityCaseInsensitive(string attributeValue, SchemaCompatibilityMode expected)
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent["schemacompatibility"] = attributeValue;

		// Act
		var compatibility = cloudEvent.GetSchemaCompatibility();

		// Assert
		compatibility.ShouldBe(expected);
	}

	[Fact]
	public void ReturnNoneForUnknownCompatibility()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent["schemacompatibility"] = "unknown";

		// Act
		var compatibility = cloudEvent.GetSchemaCompatibility();

		// Assert
		compatibility.ShouldBe(SchemaCompatibilityMode.None);
	}

	[Fact]
	public void ThrowOnSetSchemaCompatibilityWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.SetSchemaCompatibility(null!, SchemaCompatibilityMode.Forward));
	}

	[Theory]
	[InlineData(SchemaCompatibilityMode.None)]
	[InlineData(SchemaCompatibilityMode.Forward)]
	[InlineData(SchemaCompatibilityMode.Backward)]
	[InlineData(SchemaCompatibilityMode.Full)]
	public void SetSchemaCompatibilityCorrectly(SchemaCompatibilityMode mode)
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		cloudEvent.SetSchemaCompatibility(mode);

		// Assert
		cloudEvent.GetSchemaCompatibility().ShouldBe(mode);
	}

	[Fact]
	public void CreateVersionedCloudEvent()
	{
		// Arrange & Act
		var cloudEvent = CloudEventVersion.CreateVersioned(
			"//test-service/source",
			"test.event.created",
			new { Id = 1, Name = "Test" },
			"1.0.0",
			SchemaCompatibilityMode.Full);

		// Assert
		cloudEvent.ShouldNotBeNull();
		cloudEvent.Id.ShouldNotBeNullOrEmpty();
		cloudEvent.Source.ShouldNotBeNull();
		cloudEvent.Type.ShouldBe("test.event.created");
		cloudEvent.Time.ShouldNotBeNull();
		cloudEvent.DataContentType.ShouldBe("application/json");
		cloudEvent.Data.ShouldNotBeNull();
		cloudEvent.GetSchemaVersion().ShouldBe("1.0.0");
		cloudEvent.GetSchemaCompatibility().ShouldBe(SchemaCompatibilityMode.Full);
	}

	[Fact]
	public void CreateVersionedCloudEventWithDefaultCompatibility()
	{
		// Arrange & Act
		var cloudEvent = CloudEventVersion.CreateVersioned(
			"//test-service/source",
			"test.event.created",
			new { Id = 1 },
			"1.0.0");

		// Assert
		cloudEvent.GetSchemaCompatibility().ShouldBe(SchemaCompatibilityMode.None);
	}

	[Fact]
	public void ThrowOnIsCompatibleWithWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.IsCompatibleWith(null!, "1.0.0"));
	}

	[Fact]
	public void ThrowOnIsCompatibleWithWithNullTargetVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.IsCompatibleWith(null!));
	}

	[Fact]
	public void ThrowOnIsCompatibleWithWithEmptyTargetVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.IsCompatibleWith(""));
	}

	[Fact]
	public void ReturnFalseWhenNoSchemaVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForExactVersionMatch()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0.0");

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0.0");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForDifferentVersionWithNoCompatibility()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.None);

		// Act
		var result = cloudEvent.IsCompatibleWith("2.0.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForForwardCompatibility()
	{
		// Arrange - Forward compatibility: current 1.0 can read from 1.1
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Forward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.1");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForForwardCompatibilityWithOlderTarget()
	{
		// Arrange - Forward compatibility doesn't apply backwards
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.1");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Forward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForBackwardCompatibility()
	{
		// Arrange - Backward compatibility: current 1.1 can read from 1.0
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.1");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Backward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForBackwardCompatibilityWithNewerTarget()
	{
		// Arrange - Backward compatibility doesn't apply forwards
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Backward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.1");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForFullCompatibilityWithinSameMajor()
	{
		// Arrange - Full compatibility within same major version
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.5");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForFullCompatibilityAcrossMajorVersions()
	{
		// Arrange - Full compatibility doesn't apply across major versions
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("2.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void SupportVersionsWithVPrefix()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("v1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.1");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForInvalidVersionFormat()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("not-a-version");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeFalse();
	}

	private static CloudEvent CreateTestCloudEvent() =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("//test/source"),
			Type = "test.event",
			Time = DateTimeOffset.UtcNow,
		};
}
