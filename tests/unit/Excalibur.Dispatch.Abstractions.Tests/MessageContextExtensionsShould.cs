// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="MessageContextExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class MessageContextExtensionsShould
{
	[Fact]
	public void SetProperty_Should_StoreValueInProperties()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.SetProperty("key1", "value1");

		// Assert
		properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SetProperty_Should_FallbackToItems_WhenPropertiesIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(null!);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		context.SetProperty("key1", "value1");

		// Assert
		items["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SetProperty_Should_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.SetProperty("key", "value"));
	}

	[Fact]
	public void GetProperty_Should_ReturnValueFromProperties()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal) { ["key1"] = "value1" };
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var result = context.GetProperty<string>("key1");

		// Assert
		result.ShouldBe("value1");
	}

	[Fact]
	public void GetProperty_Should_ReturnDefault_WhenKeyNotFound()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		var result = context.GetProperty<string>("missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetProperty_Should_ReturnDefault_WhenTypeMismatch()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal) { ["key1"] = 42 };
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var result = context.GetProperty<string>("key1");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetProperty_Should_FallbackToItems_WhenNotInProperties()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "from-items" };
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = context.GetProperty<string>("key1");

		// Assert
		result.ShouldBe("from-items");
	}

	[Fact]
	public void GetProperty_Should_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.GetProperty<string>("key"));
	}

	[Fact]
	public void TryGetProperty_Should_ReturnTrue_WhenFound()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal) { ["key1"] = "value1" };
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var found = context.TryGetProperty<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("value1");
	}

	[Fact]
	public void TryGetProperty_Should_ReturnFalse_WhenNotFound()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var found = context.TryGetProperty<string>("missing", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryGetProperty_Should_FallbackToItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "items-value" };
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var found = context.TryGetProperty<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("items-value");
	}

	[Fact]
	public void TryGetProperty_Should_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.TryGetProperty<string>("key", out _));
	}

	[Fact]
	public void RemoveProperty_Should_RemoveFromBothDictionaries()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal) { ["key1"] = "p-value" };
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "i-value" };
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		context.RemoveProperty("key1");

		// Assert
		properties.ShouldNotContainKey("key1");
		items.ShouldNotContainKey("key1");
	}

	[Fact]
	public void RemoveProperty_Should_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.RemoveProperty("key"));
	}

	[Fact]
	public void TryGetValue_Should_ReturnTrue_WhenFoundInItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "value1" };
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var found = context.TryGetValue<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("value1");
	}

	[Fact]
	public void TryGetValue_Should_ReturnFalse_WhenNotFoundInItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var found = context.TryGetValue<string>("missing", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryGetValue_Should_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.TryGetValue<string>("key", out _));
	}

	[Fact]
	public void ValidationResult_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var validationObj = new object();
		context.ValidationResult(validationObj);

		// Assert
		context.ValidationResult().ShouldBe(validationObj);
	}

	[Fact]
	public void AuthorizationResult_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var authObj = new object();
		context.AuthorizationResult(authObj);

		// Assert
		context.AuthorizationResult().ShouldBe(authObj);
	}

	[Fact]
	public void VersionMetadata_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var metadata = new object();
		context.VersionMetadata(metadata);

		// Assert
		context.VersionMetadata().ShouldBe(metadata);
	}

	[Fact]
	public void DesiredVersion_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.DesiredVersion("2.0");

		// Assert
		context.DesiredVersion().ShouldBe("2.0");
	}

	[Fact]
	public void MessageVersion_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.MessageVersion("1.0");

		// Assert
		context.MessageVersion().ShouldBe("1.0");
	}

	[Fact]
	public void SerializerVersion_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.SerializerVersion("3.0");

		// Assert
		context.SerializerVersion().ShouldBe("3.0");
	}

	[Fact]
	public void ContractVersion_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.ContractVersion("1.1");

		// Assert
		context.ContractVersion().ShouldBe("1.1");
	}

	[Fact]
	public void PartitionKey_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.PartitionKey("partition-1");

		// Assert
		context.PartitionKey().ShouldBe("partition-1");
	}

	[Fact]
	public void ReplyTo_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		context.ReplyTo("reply-queue");

		// Assert
		context.ReplyTo().ShouldBe("reply-queue");
	}

	[Fact]
	public void Metadata_Should_SetAndGetValue()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal) { ["foo"] = "bar" };

		// Act
		context.Metadata(metadata);

		// Assert
		var retrieved = context.Metadata();
		retrieved.ShouldNotBeNull();
		retrieved["foo"].ShouldBe("bar");
	}

	[Fact]
	public void HasTransportBinding_Should_ReturnFalse_WhenNoBinding()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Properties).Returns(properties);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = context.HasTransportBinding();

		// Assert
		result.ShouldBeFalse();
	}
}
