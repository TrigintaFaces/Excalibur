// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventVersionShould
{
	[Fact]
	public void DefineSpecVersion()
	{
		CloudEventVersion.SpecVersion.ShouldBe("1.0");
	}

	[Fact]
	public void DefineSchemaVersionAttribute()
	{
		CloudEventVersion.SchemaVersionAttribute.ShouldBe("schemaversion");
	}

	[Fact]
	public void DefineSchemaCompatibilityAttribute()
	{
		CloudEventVersion.SchemaCompatibilityAttribute.ShouldBe("schemacompatibility");
	}

	[Fact]
	public void GetSchemaVersionReturnsNullWhenNotSet()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var version = cloudEvent.GetSchemaVersion();

		// Assert
		version.ShouldBeNull();
	}

	[Fact]
	public void SetAndGetSchemaVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		cloudEvent.SetSchemaVersion("1.0");

		// Assert
		cloudEvent.GetSchemaVersion().ShouldBe("1.0");
	}

	[Fact]
	public void SetSchemaVersionThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.SetSchemaVersion(null!, "1.0"));
	}

	[Fact]
	public void SetSchemaVersionThrowsForNullVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.SetSchemaVersion(null!));
		Should.Throw<ArgumentException>(() => cloudEvent.SetSchemaVersion(""));
	}

	[Fact]
	public void GetSchemaVersionThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.GetSchemaVersion(null!));
	}

	[Fact]
	public void GetSchemaCompatibilityReturnsNoneByDefault()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var compatibility = cloudEvent.GetSchemaCompatibility();

		// Assert
		compatibility.ShouldBe(SchemaCompatibilityMode.None);
	}

	[Theory]
	[InlineData(SchemaCompatibilityMode.None)]
	[InlineData(SchemaCompatibilityMode.Forward)]
	[InlineData(SchemaCompatibilityMode.Backward)]
	[InlineData(SchemaCompatibilityMode.Full)]
	public void SetAndGetSchemaCompatibility(SchemaCompatibilityMode mode)
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		cloudEvent.SetSchemaCompatibility(mode);

		// Assert
		cloudEvent.GetSchemaCompatibility().ShouldBe(mode);
	}

	[Fact]
	public void SetSchemaCompatibilityThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.SetSchemaCompatibility(null!, SchemaCompatibilityMode.Full));
	}

	[Fact]
	public void GetSchemaCompatibilityThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.GetSchemaCompatibility(null!));
	}

	[Fact]
	public void CreateVersionedCloudEvent()
	{
		// Act
		var cloudEvent = CloudEventVersion.CreateVersioned(
			"urn:test:source",
			"test.type.v1",
			new { Name = "test" },
			"1.0");

		// Assert
		cloudEvent.ShouldNotBeNull();
		cloudEvent.Source.ShouldNotBeNull();
		cloudEvent.Type.ShouldBe("test.type.v1");
		cloudEvent.GetSchemaVersion().ShouldBe("1.0");
		cloudEvent.GetSchemaCompatibility().ShouldBe(SchemaCompatibilityMode.None);
		cloudEvent.DataContentType.ShouldBe("application/json");
	}

	[Fact]
	public void CreateVersionedCloudEventWithCompatibility()
	{
		// Act
		var cloudEvent = CloudEventVersion.CreateVersioned(
			"urn:test:source",
			"test.type.v1",
			new { Name = "test" },
			"1.0",
			SchemaCompatibilityMode.Full);

		// Assert
		cloudEvent.GetSchemaCompatibility().ShouldBe(SchemaCompatibilityMode.Full);
	}

	[Fact]
	public void IsCompatibleWithExactMatch()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsNotCompatibleWhenNoVersionSet()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsNotCompatibleWithDifferentVersionAndNoCompatibilityMode()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.None);

		// Act
		var result = cloudEvent.IsCompatibleWith("2.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsForwardCompatibleWithHigherMinorVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Forward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.2");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsNotForwardCompatibleWithLowerMinorVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.2");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Forward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsBackwardCompatibleWithLowerMinorVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.2");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Backward);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.0");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsFullyCompatibleWithSameMajorVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("1.5");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsNotFullyCompatibleWithDifferentMajorVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("2.0");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsCompatibleWithThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CloudEventVersion.IsCompatibleWith(null!, "1.0"));
	}

	[Fact]
	public void IsCompatibleWithThrowsForNullTargetVersion()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() => cloudEvent.IsCompatibleWith(null!));
	}

	[Fact]
	public void HandleVersionPrefixedWithV()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("v1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		// Act
		var result = cloudEvent.IsCompatibleWith("v1.5");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HandleInvalidVersionFormats()
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

	[Fact]
	public void UseSchemaRegistryWhenProvided()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();
		cloudEvent.SetSchemaVersion("1.0");
		cloudEvent.SetSchemaCompatibility(SchemaCompatibilityMode.Full);

		var registry = A.Fake<ISchemaRegistry>();
		A.CallTo(() => registry.IsCompatible(
			A<string>._, A<string>._, A<string>._, A<SchemaCompatibilityMode>._)).Returns(true);

		// Act
		var result = cloudEvent.IsCompatibleWith("2.0", registry);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => registry.IsCompatible(
			A<string>._, "1.0", "2.0", SchemaCompatibilityMode.Full)).MustHaveHappenedOnceExactly();
	}

	private static CloudEvent CreateTestCloudEvent() =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("urn:test"),
			Type = "test.type",
			Time = DateTimeOffset.UtcNow,
		};
}
