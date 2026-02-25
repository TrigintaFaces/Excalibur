// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="DeserializationResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify constructor, properties, and As&lt;T&gt; method.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class DeserializationResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = "Test" };
		var messageType = typeof(TestMessage);
		const int schemaId = 123;
		const int version = 2;

		// Act
		var result = new DeserializationResult(message, messageType, schemaId, version);

		// Assert
		result.Message.ShouldBe(message);
		result.MessageType.ShouldBe(messageType);
		result.SchemaId.ShouldBe(schemaId);
		result.Version.ShouldBe(version);
	}

	[Fact]
	public void Constructor_ThrowsForNullMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DeserializationResult(null!, typeof(object), 1, 1));
	}

	[Fact]
	public void Constructor_ThrowsForNullMessageType()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DeserializationResult(new object(), null!, 1, 1));
	}

	#endregion

	#region Property Tests

	[Fact]
	public void Message_ContainsOriginalObject()
	{
		// Arrange
		var message = new TestMessage { Id = 42, Name = "Answer" };

		// Act
		var result = new DeserializationResult(message, typeof(TestMessage), 1, 1);

		// Assert
		result.Message.ShouldBeSameAs(message);
	}

	[Fact]
	public void MessageType_MatchesActualType()
	{
		// Arrange
		var message = new TestMessage { Id = 1 };

		// Act
		var result = new DeserializationResult(message, typeof(TestMessage), 1, 1);

		// Assert
		result.MessageType.ShouldBe(typeof(TestMessage));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(123456)]
	[InlineData(int.MaxValue)]
	public void SchemaId_AcceptsValidValues(int schemaId)
	{
		// Act
		var result = new DeserializationResult(new object(), typeof(object), schemaId, 1);

		// Assert
		result.SchemaId.ShouldBe(schemaId);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(5)]
	[InlineData(100)]
	public void Version_AcceptsValidValues(int version)
	{
		// Act
		var result = new DeserializationResult(new object(), typeof(object), 1, version);

		// Assert
		result.Version.ShouldBe(version);
	}

	#endregion

	#region As<T> Tests

	[Fact]
	public void As_ReturnsTypedMessage()
	{
		// Arrange
		var message = new TestMessage { Id = 99, Name = "Typed" };
		var result = new DeserializationResult(message, typeof(TestMessage), 1, 1);

		// Act
		var typed = result.As<TestMessage>();

		// Assert
		typed.ShouldBe(message);
		typed.Id.ShouldBe(99);
		typed.Name.ShouldBe("Typed");
	}

	[Fact]
	public void As_ThrowsForInvalidCast()
	{
		// Arrange
		var message = new TestMessage { Id = 1 };
		var result = new DeserializationResult(message, typeof(TestMessage), 1, 1);

		// Act & Assert
		_ = Should.Throw<InvalidCastException>(() => result.As<string>());
	}

	[Fact]
	public void As_WorksWithBaseType()
	{
		// Arrange
		var message = new DerivedMessage { Id = 1, Extra = "Data" };
		var result = new DeserializationResult(message, typeof(DerivedMessage), 1, 1);

		// Act
		var baseType = result.As<TestMessage>();

		// Assert
		baseType.ShouldBeOfType<DerivedMessage>();
		baseType.Id.ShouldBe(1);
	}

	[Fact]
	public void As_WorksWithInterface()
	{
		// Arrange
		var message = new InterfaceImplementor { Value = 42 };
		var result = new DeserializationResult(message, typeof(InterfaceImplementor), 1, 1);

		// Act
		var interfaceType = result.As<ITestInterface>();

		// Assert
		interfaceType.ShouldBeOfType<InterfaceImplementor>();
		interfaceType.Value.ShouldBe(42);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Properties_AreReadOnly()
	{
		// Assert
		typeof(DeserializationResult).GetProperty(nameof(DeserializationResult.Message)).CanWrite.ShouldBeFalse();
		typeof(DeserializationResult).GetProperty(nameof(DeserializationResult.MessageType)).CanWrite.ShouldBeFalse();
		typeof(DeserializationResult).GetProperty(nameof(DeserializationResult.SchemaId)).CanWrite.ShouldBeFalse();
		typeof(DeserializationResult).GetProperty(nameof(DeserializationResult.Version)).CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(DeserializationResult).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private class TestMessage
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}

	private sealed class DerivedMessage : TestMessage
	{
		public string? Extra { get; set; }
	}

	private interface ITestInterface
	{
		int Value { get; }
	}

	private sealed class InterfaceImplementor : ITestInterface
	{
		public int Value { get; set; }
	}

	#endregion
}
