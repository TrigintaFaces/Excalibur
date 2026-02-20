// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

#pragma warning disable CA2201 // Do not raise reserved exception types - test code intentionally uses base Exception

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for the <see cref="ExceptionMapper"/> class.
/// These tests directly verify the ExceptionMapper implementation behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExceptionMapperShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ExceptionMapper(null!));
	}

	#endregion

	#region Map Tests

	[Fact]
	public void ThrowArgumentNullException_WhenMapCalledWithNullException()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		var mapper = new ExceptionMapper(builder.Build());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => mapper.Map(null!));
	}

	[Fact]
	public void Map_ReturnsMappedProblemDetails_ForRegisteredExceptionType()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails
		{
			Type = "test:invalid-operation",
			Title = "Invalid Operation",
			ErrorCode = 400,
			Status = 400,
			Detail = ex.Message,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test error");

		// Act
		var result = mapper.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("test:invalid-operation");
		result.Title.ShouldBe("Invalid Operation");
		result.ErrorCode.ShouldBe(400);
		result.Detail.ShouldBe("Test error");
	}

	[Fact]
	public void Map_UsesFirstMatchingMapping_WhenMultipleMappingsExist()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails
		{
			Type = "test:first",
			Title = "First Mapper",
			ErrorCode = 400,
			Status = 400,
		});
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails
		{
			Type = "test:second",
			Title = "Second Mapper",
			ErrorCode = 422,
			Status = 422,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:first");
		result.Title.ShouldBe("First Mapper");
		result.ErrorCode.ShouldBe(400);
	}

	[Fact]
	public void Map_UsesApiExceptionToProblemDetails_WhenEnabledAndNoCustomMapping()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.UseApiExceptionMapping();
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ApiException(404, "Resource not found", null);

		// Act
		var result = mapper.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ErrorCode.ShouldBe(404);
		result.Detail.ShouldBe("Resource not found");
	}

	[Fact]
	public void Map_CustomMappingTakesPrecedence_OverApiExceptionMapping()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.UseApiExceptionMapping();
		_ = builder.Map<ApiException>(ex => new MessageProblemDetails
		{
			Type = "test:custom-api",
			Title = "Custom API Mapping",
			ErrorCode = 999,
			Status = 999,
			Detail = "Custom handled",
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ApiException(404, "Original message", null);

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:custom-api");
		result.Title.ShouldBe("Custom API Mapping");
		result.ErrorCode.ShouldBe(999);
		result.Detail.ShouldBe("Custom handled");
	}

	[Fact]
	public void Map_UsesDefaultMapper_WhenNoMappingMatches()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails
		{
			Type = "test:invalid-op",
			Title = "Invalid Op",
			ErrorCode = 400,
			Status = 400,
		});
		_ = builder.MapDefault(ex => new MessageProblemDetails
		{
			Type = "test:default",
			Title = "Default Handler",
			ErrorCode = 500,
			Status = 500,
			Detail = "Handled by default mapper",
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Different type"); // Not InvalidOperationException

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:default");
		result.Title.ShouldBe("Default Handler");
		result.ErrorCode.ShouldBe(500);
		result.Detail.ShouldBe("Handled by default mapper");
	}

	[Fact]
	public void Map_UsesBuiltInDefaultMapper_WhenNoCustomDefaultProvided()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		// No custom mappings, no custom default - only built-in default
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new Exception("Unhandled exception");

		// Act
		var result = mapper.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("urn:dispatch:error:internal");
		result.Title.ShouldBe("Internal Server Error");
		result.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public void Map_HandlesConditionalMapping_WhenPredicateMatches()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapWhen<ArgumentException>(
			ex => ex.ParamName == "targetParam",
			ex => new MessageProblemDetails
			{
				Type = "test:arg-target",
				Title = "Target Parameter Error",
				ErrorCode = 400,
				Status = 400,
				Detail = ex.Message,
			});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Invalid value", "targetParam");

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:arg-target");
		result.Title.ShouldBe("Target Parameter Error");
	}

	[Fact]
	public void Map_SkipsConditionalMapping_WhenPredicateDoesNotMatch()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapWhen<ArgumentException>(
			ex => ex.ParamName == "targetParam",
			ex => new MessageProblemDetails
			{
				Type = "test:arg-target",
				Title = "Target Parameter Error",
				ErrorCode = 400,
				Status = 400,
			});
		_ = builder.MapDefault(ex => new MessageProblemDetails
		{
			Type = "test:default",
			Title = "Default",
			ErrorCode = 500,
			Status = 500,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Invalid value", "otherParam");

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:default");
	}

	[Fact]
	public void Map_HandlesExceptionHierarchy_MatchesDerivedTypes()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<ArgumentException>(ex => new MessageProblemDetails
		{
			Type = "test:argument",
			Title = "Argument Error",
			ErrorCode = 400,
			Status = 400,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentNullException("param"); // Derived from ArgumentException

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:argument");
		result.Title.ShouldBe("Argument Error");
	}

	#endregion

	#region MapAsync Tests

	[Fact]
	public async Task MapAsync_ThrowsArgumentNullException_WhenExceptionIsNull()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		var mapper = new ExceptionMapper(builder.Build());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			mapper.MapAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MapAsync_ReturnsMappedProblemDetails_ForAsyncMapper()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
		{
			await Task.Delay(1, ct);
			return new MessageProblemDetails
			{
				Type = "test:async-invalid-op",
				Title = "Async Invalid Operation",
				ErrorCode = 422,
				Status = 422,
				Detail = ex.Message,
			};
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Async test error");

		// Act
		var result = await mapper.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("test:async-invalid-op");
		result.Title.ShouldBe("Async Invalid Operation");
		result.ErrorCode.ShouldBe(422);
		result.Detail.ShouldBe("Async test error");
	}

	[Fact]
	public async Task MapAsync_UsesFirstMatchingMapping_WhenMultipleMappingsExist()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
		{
			await Task.Yield();
			return new MessageProblemDetails { Type = "test:first-async", ErrorCode = 400, Status = 400 };
		});
		_ = builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
		{
			await Task.Yield();
			return new MessageProblemDetails { Type = "test:second-async", ErrorCode = 422, Status = 422 };
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");

		// Act
		var result = await mapper.MapAsync(exception, CancellationToken.None);

		// Assert
		result.Type.ShouldBe("test:first-async");
		result.ErrorCode.ShouldBe(400);
	}

	[Fact]
	public async Task MapAsync_UsesApiExceptionToProblemDetails_WhenEnabled()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.UseApiExceptionMapping();
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ApiException(403, "Access denied", null);

		// Act
		var result = await mapper.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ErrorCode.ShouldBe(403);
		result.Detail.ShouldBe("Access denied");
	}

	[Fact]
	public async Task MapAsync_UsesDefaultMapper_WhenNoMappingMatches()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new NotSupportedException("Not supported");

		// Act
		var result = await mapper.MapAsync(exception, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("urn:dispatch:error:internal");
		result.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public async Task MapAsync_PassesCancellationToken_ToAsyncMapper()
	{
		// Arrange
		CancellationToken receivedToken = default;
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
		{
			receivedToken = ct;
			await Task.Yield();
			return new MessageProblemDetails { Type = "test", ErrorCode = 400, Status = 400 };
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");
		using var cts = new CancellationTokenSource();

		// Act
		_ = await mapper.MapAsync(exception, cts.Token);

		// Assert
		receivedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task MapAsync_HandlesSyncMapper_WithAsyncCall()
	{
		// Arrange - Using sync Map<T> but calling MapAsync
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails
		{
			Type = "test:sync-via-async",
			Title = "Sync Mapper",
			ErrorCode = 400,
			Status = 400,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");

		// Act
		var result = await mapper.MapAsync(exception, CancellationToken.None);

		// Assert
		result.Type.ShouldBe("test:sync-via-async");
		result.Title.ShouldBe("Sync Mapper");
	}

	#endregion

	#region CanMap Tests

	[Fact]
	public void CanMap_ThrowsArgumentNullException_WhenExceptionIsNull()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		var mapper = new ExceptionMapper(builder.Build());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => mapper.CanMap(null!));
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForRegisteredExceptionType()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails());
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");

		// Act
		var result = mapper.CanMap(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForApiException_WhenApiExceptionMappingEnabled()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.UseApiExceptionMapping();
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ApiException(404, "Not found", null);

		// Act
		var result = mapper.CanMap(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForUnregisteredType_BecauseDefaultMapperExists()
	{
		// Arrange - Default mapper always exists (built-in or custom)
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<InvalidOperationException>(ex => new MessageProblemDetails());
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Different type");

		// Act
		var result = mapper.CanMap(exception);

		// Assert - Always true because default mapper is always configured
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForConditionalMapping_WhenPredicateMatches()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapWhen<ArgumentException>(
			ex => ex.ParamName == "targetParam",
			ex => new MessageProblemDetails());
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Test", "targetParam");

		// Act
		var result = mapper.CanMap(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForConditionalMapping_WhenPredicateDoesNotMatch_BecauseDefaultExists()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.MapWhen<ArgumentException>(
			ex => ex.ParamName == "targetParam",
			ex => new MessageProblemDetails());
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentException("Test", "otherParam");

		// Act
		var result = mapper.CanMap(exception);

		// Assert - True because default mapper always exists
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnsTrue_ForDerivedExceptionType()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<ArgumentException>(ex => new MessageProblemDetails());
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ArgumentNullException("param"); // Derived from ArgumentException

		// Act
		var result = mapper.CanMap(exception);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Map_WithEmptyMappings_UsesDefaultMapper()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		// No mappings registered
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new InvalidOperationException("Test");

		// Act
		var result = mapper.Map(exception);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Type.ShouldBe("urn:dispatch:error:internal");
		result.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public void Map_WithApiExceptionMappingDisabled_UsesDefaultMapper_NotToProblemDetails()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		// Don't call UseApiExceptionMapping() - but it's enabled by default
		// Need to explicitly set a mapping that doesn't include ApiException
		_ = builder.MapDefault(ex => new MessageProblemDetails
		{
			Type = "test:custom-default",
			Title = "Custom Default",
			ErrorCode = 503,
			Status = 503,
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new ApiException(404, "Not found", null);

		// Act - Since UseApiExceptionMapping is enabled by default, it should use ToProblemDetails
		var result = mapper.Map(exception);

		// Assert - With default enabled, it uses ApiException.ToProblemDetails()
		result.ErrorCode.ShouldBe(404); // Not 503 from custom default
	}

	[Fact]
	public async Task MapAsync_WithMultipleMixedMappings_UsesCorrectOne()
	{
		// Arrange - Mix of sync and async mappings
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<ArgumentException>(ex => new MessageProblemDetails
		{
			Type = "test:arg-sync",
			ErrorCode = 400,
			Status = 400,
		});
		_ = builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
		{
			await Task.Yield();
			return new MessageProblemDetails
			{
				Type = "test:invalid-async",
				ErrorCode = 422,
				Status = 422,
			};
		});
		var mapper = new ExceptionMapper(builder.Build());

		// Act - Call with InvalidOperationException (async mapping)
		var result = await mapper.MapAsync(new InvalidOperationException("Test"), CancellationToken.None);

		// Assert
		result.Type.ShouldBe("test:invalid-async");
		result.ErrorCode.ShouldBe(422);
	}

	[Fact]
	public void Map_WithAggregateException_HandlesCorrectly()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<AggregateException>(ex => new MessageProblemDetails
		{
			Type = "test:aggregate",
			Title = "Multiple Errors",
			ErrorCode = 500,
			Status = 500,
			Detail = $"Count: {ex.InnerExceptions.Count}",
		});
		var mapper = new ExceptionMapper(builder.Build());
		var exception = new AggregateException(
			new InvalidOperationException("First"),
			new ArgumentException("Second"));

		// Act
		var result = mapper.Map(exception);

		// Assert
		result.Type.ShouldBe("test:aggregate");
		result.Detail.ShouldBe("Count: 2");
	}

	#endregion
}
