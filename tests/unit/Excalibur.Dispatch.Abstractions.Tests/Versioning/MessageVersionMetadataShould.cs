namespace Excalibur.Dispatch.Abstractions.Tests.Versioning;

/// <summary>
/// Unit tests for MessageVersionMetadata.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageVersionMetadataShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreAllOne()
	{
		// Act
		var metadata = new MessageVersionMetadata();

		// Assert
		metadata.SchemaVersion.ShouldBe(1);
		metadata.SerializerVersion.ShouldBe(1);
		metadata.Version.ShouldBe(1);
	}

	[Fact]
	public void SchemaVersion_CanBeSet()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SchemaVersion = 5;

		// Assert
		metadata.SchemaVersion.ShouldBe(5);
	}

	[Fact]
	public void SerializerVersion_CanBeSet()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.SerializerVersion = 3;

		// Assert
		metadata.SerializerVersion.ShouldBe(3);
	}

	[Fact]
	public void Version_CanBeSet()
	{
		// Arrange
		var metadata = new MessageVersionMetadata();

		// Act
		metadata.Version = 10;

		// Assert
		metadata.Version.ShouldBe(10);
	}

	[Fact]
	public void MetadataKey_HasExpectedValue()
	{
		// Assert
		MessageVersionMetadata.MetadataKey.ShouldBe("Excalibur.MessageVersionMetadata");
	}

	[Fact]
	public void AllProperties_CanBeSetIndependently()
	{
		// Act
		var metadata = new MessageVersionMetadata
		{
			SchemaVersion = 2,
			SerializerVersion = 3,
			Version = 4,
		};

		// Assert
		metadata.SchemaVersion.ShouldBe(2);
		metadata.SerializerVersion.ShouldBe(3);
		metadata.Version.ShouldBe(4);
	}
}
