using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for JsonSerializerExtensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonSerializerExtensionsShould : UnitTestBase
{
	[Fact]
	public void Serialize_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => JsonSerializerExtensions.Serialize(null!, "test"));
	}

	[Fact]
	public void Serialize_DelegatesToSerializerWithType()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("serialized");

		// Act
		var result = serializer.Serialize("test-value");

		// Assert
		result.ShouldBe("serialized");
		A.CallTo(() => serializer.Serialize("test-value", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Deserialize_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => JsonSerializerExtensions.Deserialize<string>(null!, "{}"));
	}

	[Fact]
	public void Deserialize_DelegatesToSerializerWithType()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Deserialize(A<string>._, A<Type>._)).Returns("deserialized");

		// Act
		var result = serializer.Deserialize<string>("{\"value\":1}");

		// Assert
		result.ShouldBe("deserialized");
		A.CallTo(() => serializer.Deserialize("{\"value\":1}", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Deserialize_WhenSerializerReturnsNull_ReturnsNull()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Deserialize(A<string>._, A<Type>._)).Returns(null);

		// Act
		var result = serializer.Deserialize<string>("null");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task SerializeAsync_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => JsonSerializerExtensions.SerializeAsync(null!, "test"));
	}

	[Fact]
	public async Task SerializeAsync_DelegatesToSerializerWithType()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.SerializeAsync(A<object>._, A<Type>._)).Returns(Task.FromResult("async-serialized"));

		// Act
		var result = await serializer.SerializeAsync("test-value");

		// Assert
		result.ShouldBe("async-serialized");
		A.CallTo(() => serializer.SerializeAsync("test-value", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeserializeAsync_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => JsonSerializerExtensions.DeserializeAsync<string>(null!, "{}"));
	}

	[Fact]
	public async Task DeserializeAsync_DelegatesToSerializerWithType()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._)).Returns(Task.FromResult<object?>("async-deserialized"));

		// Act
		var result = await serializer.DeserializeAsync<string>("{\"value\":1}");

		// Assert
		result.ShouldBe("async-deserialized");
		A.CallTo(() => serializer.DeserializeAsync("{\"value\":1}", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeserializeAsync_WhenSerializerReturnsNull_ReturnsNull()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._)).Returns(Task.FromResult<object?>(null));

		// Act
		var result = await serializer.DeserializeAsync<string>("null");

		// Assert
		result.ShouldBeNull();
	}
}
