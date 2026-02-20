// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for exception mapping classes.
/// </summary>
/// <remarks>
/// Tests TypedExceptionMapping, AsyncTypedExceptionMapping, and ConditionalExceptionMapping.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class ExceptionMappingShould
{
	#region TypedExceptionMapping Tests

	[Fact]
	public void TypedExceptionMapping_Constructor_WithNullMapper_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TypedExceptionMapping<InvalidOperationException>(null!));
	}

	[Fact]
	public void TypedExceptionMapping_Constructor_WithValidMapper_Succeeds()
	{
		// Act
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));

		// Assert
		_ = mapping.ShouldNotBeNull();
	}

	[Fact]
	public void TypedExceptionMapping_ExceptionType_ReturnsCorrectType()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));

		// Act & Assert
		mapping.ExceptionType.ShouldBe(typeof(InvalidOperationException));
	}

	[Fact]
	public void TypedExceptionMapping_IsAsync_ReturnsFalse()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));

		// Act & Assert
		mapping.IsAsync.ShouldBeFalse();
	}

	[Fact]
	public void TypedExceptionMapping_CanHandle_WithMatchingException_ReturnsTrue()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeTrue();
	}

	[Fact]
	public void TypedExceptionMapping_CanHandle_WithDerivedExceptionType_ReturnsTrue()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<Exception>(
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("Test");

		// Act & Assert - InvalidOperationException is derived from Exception
		mapping.CanHandle(exception).ShouldBeTrue();
	}

	[Fact]
	public void TypedExceptionMapping_CanHandle_WithNonMatchingException_ReturnsFalse()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));
		var exception = new ArgumentException("Test");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeFalse();
	}

	[Fact]
	public void TypedExceptionMapping_Map_WithMatchingException_ReturnsResult()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("mapped-error", ex.Message));
		var exception = new InvalidOperationException("Test message");

		// Act
		var result = mapping.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("mapped-error");
		result.Detail.ShouldBe("Test message");
	}

	[Fact]
	public void TypedExceptionMapping_Map_WithNonMatchingException_ThrowsArgumentException()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));
		var exception = new ArgumentException("Test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapping.Map(exception));
	}

	[Fact]
	public async Task TypedExceptionMapping_MapAsync_ReturnsCompletedTask()
	{
		// Arrange
		var mapping = new TypedExceptionMapping<InvalidOperationException>(
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("Test");

		// Act
		var result = await mapping.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	#endregion

	#region AsyncTypedExceptionMapping Tests

	[Fact]
	public void AsyncTypedExceptionMapping_Constructor_WithNullMapper_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AsyncTypedExceptionMapping<InvalidOperationException>(null!));
	}

	[Fact]
	public void AsyncTypedExceptionMapping_ExceptionType_ReturnsCorrectType()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<ArgumentNullException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("test", "Test")));

		// Act & Assert
		mapping.ExceptionType.ShouldBe(typeof(ArgumentNullException));
	}

	[Fact]
	public void AsyncTypedExceptionMapping_IsAsync_ReturnsTrue()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<InvalidOperationException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("test", "Test")));

		// Act & Assert
		mapping.IsAsync.ShouldBeTrue();
	}

	[Fact]
	public void AsyncTypedExceptionMapping_CanHandle_WithMatchingException_ReturnsTrue()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<InvalidOperationException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("test", "Test")));
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeTrue();
	}

	[Fact]
	public async Task AsyncTypedExceptionMapping_MapAsync_WithMatchingException_ReturnsResult()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<InvalidOperationException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("async-error", ex.Message)));
		var exception = new InvalidOperationException("Async test message");

		// Act
		var result = await mapping.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("async-error");
		result.Detail.ShouldBe("Async test message");
	}

	[Fact]
	public async Task AsyncTypedExceptionMapping_MapAsync_WithNonMatchingException_ThrowsArgumentException()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<InvalidOperationException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("test", "Test")));
		var exception = new ArgumentException("Test");

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			mapping.MapAsync(exception, CancellationToken.None));
	}

	[Fact]
	public void AsyncTypedExceptionMapping_Map_BlocksAndReturnsResult()
	{
		// Arrange
		var mapping = new AsyncTypedExceptionMapping<InvalidOperationException>(
			(ex, ct) => Task.FromResult(CreateProblemDetails("sync-fallback", "Blocking call")));
		var exception = new InvalidOperationException("Test");

		// Act
		var result = mapping.Map(exception);

		// Assert - Sync Map should work (blocking)
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("sync-fallback");
	}

	#endregion

	#region ConditionalExceptionMapping Tests

	[Fact]
	public void ConditionalExceptionMapping_Constructor_WithNullPredicate_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ConditionalExceptionMapping<InvalidOperationException>(
				null!,
				ex => CreateProblemDetails("test", "Test")));
	}

	[Fact]
	public void ConditionalExceptionMapping_Constructor_WithNullMapper_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ConditionalExceptionMapping<InvalidOperationException>(
				ex => true,
				null!));
	}

	[Fact]
	public void ConditionalExceptionMapping_ExceptionType_ReturnsCorrectType()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("test", "Test"));

		// Act & Assert
		mapping.ExceptionType.ShouldBe(typeof(InvalidOperationException));
	}

	[Fact]
	public void ConditionalExceptionMapping_IsAsync_ReturnsFalse()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("test", "Test"));

		// Act & Assert
		mapping.IsAsync.ShouldBeFalse();
	}

	[Fact]
	public void ConditionalExceptionMapping_CanHandle_WhenPredicateTrue_ReturnsTrue()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => ex.Message.Contains("special"),
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("This is special");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeTrue();
	}

	[Fact]
	public void ConditionalExceptionMapping_CanHandle_WhenPredicateFalse_ReturnsFalse()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => ex.Message.Contains("special"),
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("This is normal");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeFalse();
	}

	[Fact]
	public void ConditionalExceptionMapping_CanHandle_WithNonMatchingType_ReturnsFalse()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("test", "Test"));
		var exception = new ArgumentException("Test");

		// Act & Assert
		mapping.CanHandle(exception).ShouldBeFalse();
	}

	[Fact]
	public void ConditionalExceptionMapping_Map_WithMatchingException_ReturnsResult()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("conditional-error", ex.Message));
		var exception = new InvalidOperationException("Conditional message");

		// Act
		var result = mapping.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("conditional-error");
		result.Detail.ShouldBe("Conditional message");
	}

	[Fact]
	public void ConditionalExceptionMapping_Map_WithNonMatchingException_ThrowsArgumentException()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("test", "Test"));
		var exception = new ArgumentException("Test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapping.Map(exception));
	}

	[Fact]
	public async Task ConditionalExceptionMapping_MapAsync_ReturnsCompletedTask()
	{
		// Arrange
		var mapping = new ConditionalExceptionMapping<InvalidOperationException>(
			ex => true,
			ex => CreateProblemDetails("test", "Test"));
		var exception = new InvalidOperationException("Test");

		// Act
		var result = await mapping.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	#endregion

	#region Helper Methods

	private static IMessageProblemDetails CreateProblemDetails(string type, string detail) =>
		new TestProblemDetails { Type = type, Detail = detail };

	private sealed class TestProblemDetails : IMessageProblemDetails
	{
		public required string Type { get; set; }
		public string Title { get; set; } = "Test Error";
		public int ErrorCode { get; set; }
		public required string Detail { get; set; }
		public string Instance { get; set; } = string.Empty;
		public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>();
	}

	#endregion
}
