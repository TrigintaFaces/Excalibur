// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.Serialization;

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="NoFallbackAvailableException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class NoFallbackAvailableExceptionShould : UnitTestBase
{
	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		// Act
		var exception = new NoFallbackAvailableException();

		// Assert
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "No fallback available";

		// Act
		var exception = new NoFallbackAvailableException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "No fallback available";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new NoFallbackAvailableException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void CanBeThrown()
	{
		// Act & Assert
		_ = Should.Throw<NoFallbackAvailableException>(() =>
			throw new NoFallbackAvailableException("Test"));
	}

	[Fact]
	public void IsException()
	{
		// Arrange
		var exception = new NoFallbackAvailableException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HasSerializableAttribute()
	{
		// Arrange
		var exception = new NoFallbackAvailableException();

		// Assert - The class has the [Serializable] attribute
		var hasAttribute = exception.GetType().GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Length > 0;
		hasAttribute.ShouldBeTrue();
	}

#pragma warning disable SYSLIB0050 // Type or member is obsolete (SerializationInfo, StreamingContext are obsolete but needed for testing)
#pragma warning disable SYSLIB0051 // Type or member is obsolete (Serialization is obsolete but we need to test the constructor)
	[Fact]
	public void SerializationConstructor_DeserializesCorrectly()
	{
		// Arrange
		const string message = "Original exception message";
		var original = new NoFallbackAvailableException(message);

		// Create a derived type to access the protected serialization constructor
		var info = new SerializationInfo(typeof(NoFallbackAvailableException), new FormatterConverter());
		var context = new StreamingContext(StreamingContextStates.All);

		// Populate SerializationInfo with required exception data
		original.GetObjectData(info, context);

		// Act - Use the serialization constructor via derived type
		var deserialized = new TestableNoFallbackAvailableException(info, context);

		// Assert
		deserialized.Message.ShouldBe(message);
	}
#pragma warning restore SYSLIB0051
#pragma warning restore SYSLIB0050

	/// <summary>
	/// Test helper that exposes the protected serialization constructor.
	/// </summary>
#pragma warning disable SYSLIB0050 // Type or member is obsolete (required for testing serialization)
#pragma warning disable SYSLIB0051 // Type or member is obsolete (required for testing serialization)
	private sealed class TestableNoFallbackAvailableException : NoFallbackAvailableException
	{
		public TestableNoFallbackAvailableException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
#pragma warning restore SYSLIB0051
#pragma warning restore SYSLIB0050
}
