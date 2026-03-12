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
	public void SetProperty_Should_StoreValueInItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
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
	public void GetProperty_Should_ReturnValueFromItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "value1" };
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = 42 };
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = context.GetProperty<string>("key1");

		// Assert
		result.ShouldBeNull();
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
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "value1" };
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var found = context.TryGetProperty<string>("missing", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBeNull();
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
	public void RemoveProperty_Should_RemoveFromItems()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["key1"] = "value" };
		A.CallTo(() => context.Items).Returns(items);

		// Act
		context.RemoveProperty("key1");

		// Assert
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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);
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
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = context.HasTransportBinding();

		// Assert
		result.ShouldBeFalse();
	}
}
