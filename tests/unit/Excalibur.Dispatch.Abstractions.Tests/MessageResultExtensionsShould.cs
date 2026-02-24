// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="MessageResultExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class MessageResultExtensionsShould
{
	#region Map Tests

	[Fact]
	public void Map_TransformSuccessValue()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var mapped = result.Map(x => x.ToString());

		// Assert
		mapped.Succeeded.ShouldBeTrue();
		mapped.ReturnValue.ShouldBe("42");
	}

	[Fact]
	public void Map_PropagateFailureWithProblemDetails()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Test Error",
			ErrorCode = 400,
			Detail = "Test failure",
		};
		var result = MessageResult.Failed<int>("Error", problemDetails);

		// Act
		var mapped = result.Map(x => x.ToString());

		// Assert
		mapped.Succeeded.ShouldBeFalse();
		_ = mapped.ProblemDetails.ShouldNotBeNull();
		mapped.ProblemDetails.Type.ShouldBe("test:error");
	}

	[Fact]
	public void Map_TreatNullReturnValueAsFailure()
	{
		// Arrange - success result with null value via factory
		var result = MessageResult.Failed<string?>(null as string, null);

		// Act
		var mapped = result.Map(x => x.Length);

		// Assert
		mapped.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void Map_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.Map(x => x.ToString()));
	}

	[Fact]
	public void Map_ThrowArgumentNullException_WhenMapperIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.Map<int, string>(null!));
	}

	[Fact]
	public async Task MapAsync_TransformSuccessValueAsync()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var mapped = await result.MapAsync(async x =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			return x.ToString();
		});

		// Assert
		mapped.Succeeded.ShouldBeTrue();
		mapped.ReturnValue.ShouldBe("42");
	}

	[Fact]
	public async Task Map_OnTask_TransformSuccessValue()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act
		var mapped = await resultTask.Map(x => x.ToString());

		// Assert
		mapped.Succeeded.ShouldBeTrue();
		mapped.ReturnValue.ShouldBe("42");
	}

	#endregion

	#region Bind Tests

	[Fact]
	public void Bind_ChainSuccessfulResults()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var bound = result.Bind(x => MessageResult.Success(x * 2));

		// Assert
		bound.Succeeded.ShouldBeTrue();
		bound.ReturnValue.ShouldBe(84);
	}

	[Fact]
	public void Bind_PropagateFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 404 };
		var result = MessageResult.Failed<int>("Not found", problemDetails);

		// Act
		var bound = result.Bind(x => MessageResult.Success(x * 2));

		// Assert
		bound.Succeeded.ShouldBeFalse();
		_ = bound.ProblemDetails.ShouldNotBeNull();
		bound.ProblemDetails.ErrorCode.ShouldBe(404);
	}

	[Fact]
	public void Bind_ReturnBinderFailure_WhenBinderFails()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var binderProblem = new MessageProblemDetails { ErrorCode = 500 };

		// Act
		var bound = result.Bind(x => MessageResult.Failed<int>("Binder failed", binderProblem));

		// Assert
		bound.Succeeded.ShouldBeFalse();
		bound.ProblemDetails.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public async Task BindAsync_ChainSuccessfulResultsAsync()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var bound = await result.BindAsync(async x =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			return MessageResult.Success(x * 2);
		});

		// Assert
		bound.Succeeded.ShouldBeTrue();
		bound.ReturnValue.ShouldBe(84);
	}

	[Fact]
	public async Task Bind_OnTask_ChainSuccessfulResults()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act
		var bound = await resultTask.Bind(x => MessageResult.Success(x * 2));

		// Assert
		bound.Succeeded.ShouldBeTrue();
		bound.ReturnValue.ShouldBe(84);
	}

	#endregion

	#region Match Tests

	[Fact]
	public void Match_ExecuteOnSuccess_WhenSuccessful()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var matched = result.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: _ => "Failed");

		// Assert
		matched.ShouldBe("Value: 42");
	}

	[Fact]
	public void Match_ExecuteOnFailure_WhenFailed()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { Detail = "Test error" };
		var result = MessageResult.Failed<int>("Error", problemDetails);

		// Act
		var matched = result.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: p => $"Error: {p?.Detail}");

		// Assert
		matched.ShouldBe("Error: Test error");
	}

	[Fact]
	public void Match_ExecuteOnFailure_WhenSuccessValueIsNull()
	{
		// Arrange
		var result = MessageResult.Failed<string?>(null as string, null);

		// Act
		var matched = result.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: _ => "No value");

		// Assert
		matched.ShouldBe("No value");
	}

	[Fact]
	public async Task Match_OnTask_ExecuteOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act
		var matched = await resultTask.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: _ => "Failed");

		// Assert
		matched.ShouldBe("Value: 42");
	}

	[Fact]
	public async Task MatchAsync_ExecuteAsyncOnSuccess()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var matched = await result.MatchAsync(
			onSuccess: async x =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				return $"Value: {x}";
			},
			onFailure: async _ =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				return "Failed";
			});

		// Assert
		matched.ShouldBe("Value: 42");
	}

	#endregion

	#region Tap Tests

	[Fact]
	public void Tap_ExecuteSideEffect_WhenSuccessful()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var sideEffectExecuted = false;

		// Act
		var tapped = result.Tap(x => sideEffectExecuted = x == 42);

		// Assert
		sideEffectExecuted.ShouldBeTrue();
		tapped.ShouldBeSameAs(result);
	}

	[Fact]
	public void Tap_NotExecuteSideEffect_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);
		var sideEffectExecuted = false;

		// Act
		var tapped = result.Tap(_ => sideEffectExecuted = true);

		// Assert
		sideEffectExecuted.ShouldBeFalse();
		tapped.ShouldBeSameAs(result);
	}

	[Fact]
	public void Tap_ReturnOriginalResult_Unchanged()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var tapped = result.Tap(x => { /* do nothing */ });

		// Assert
		tapped.Succeeded.ShouldBeTrue();
		tapped.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public async Task TapAsync_ExecuteAsyncSideEffect()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var sideEffectExecuted = false;

		// Act
		var tapped = await result.TapAsync(async x =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			sideEffectExecuted = x == 42;
		});

		// Assert
		sideEffectExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task Tap_OnTask_ExecuteSideEffect()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);
		var sideEffectExecuted = false;

		// Act
		var tapped = await resultTask.Tap(x => sideEffectExecuted = x == 42);

		// Assert
		sideEffectExecuted.ShouldBeTrue();
	}

	#endregion

	#region GetValueOrDefault Tests

	[Fact]
	public void GetValueOrDefault_ReturnValue_WhenSuccessful()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var value = result.GetValueOrDefault();

		// Assert
		value.ShouldBe(42);
	}

	[Fact]
	public void GetValueOrDefault_ReturnDefault_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);

		// Act
		var value = result.GetValueOrDefault();

		// Assert
		value.ShouldBe(0); // default(int)
	}

	[Fact]
	public void GetValueOrDefault_ReturnSpecifiedDefault_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);

		// Act
		var value = result.GetValueOrDefault(-1);

		// Assert
		value.ShouldBe(-1);
	}

	[Fact]
	public void GetValueOrDefault_ReturnDefault_WhenSuccessButNullValue()
	{
		// Arrange
		var result = MessageResult.Failed<string?>(null as string, null);

		// Act
		var value = result.GetValueOrDefault("fallback");

		// Assert
		value.ShouldBe("fallback");
	}

	#endregion

	#region GetValueOrThrow Tests

	[Fact]
	public void GetValueOrThrow_ReturnValue_WhenSuccessful()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var value = result.GetValueOrThrow();

		// Assert
		value.ShouldBe(42);
	}

	[Fact]
	public void GetValueOrThrow_ThrowInvalidOperationException_WhenFailed()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { Detail = "Test failure" };
		var result = MessageResult.Failed<int>("Error", problemDetails);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => result.GetValueOrThrow());
		ex.Message.ShouldBe("Test failure");
		ex.Data["ProblemDetails"].ShouldBe(problemDetails);
	}

	[Fact]
	public void GetValueOrThrow_ThrowWithErrorMessage_WhenNoProblemDetails()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Custom error message", null);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => result.GetValueOrThrow());
		ex.Message.ShouldBe("Custom error message");
	}

	[Fact]
	public void GetValueOrThrow_ThrowWithDefaultMessage_WhenNoDetailsAvailable()
	{
		// Arrange
		var result = MessageResult.Failed<int>(null as string, null);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => result.GetValueOrThrow());
		ex.Message.ShouldBe("Result did not contain a value.");
	}

	[Fact]
	public async Task GetValueOrThrow_OnTask_ReturnValue()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act
		var value = await resultTask.GetValueOrThrow();

		// Assert
		value.ShouldBe(42);
	}

	[Fact]
	public async Task GetValueOrThrow_OnTask_ThrowWhenFailed()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Failed<int>("Error", null) as IMessageResult<int>);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () => await resultTask.GetValueOrThrow());
	}

	#endregion

	#region Chaining Tests

	[Fact]
	public void Chain_MapAndMatch()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act
		var output = result
			.Map(x => x * 2)
			.Map(x => x.ToString())
			.Match(
				onSuccess: x => $"Result: {x}",
				onFailure: _ => "Failed");

		// Assert
		output.ShouldBe("Result: 84");
	}

	[Fact]
	public void Chain_BindAndMap()
	{
		// Arrange
		var result = MessageResult.Success(10);

		// Act
		var output = result
			.Bind(x => x > 5 ? MessageResult.Success(x * 2) : MessageResult.Failed<int>("Too small", null))
			.Map(x => x.ToString());

		// Assert
		output.Succeeded.ShouldBeTrue();
		output.ReturnValue.ShouldBe("20");
	}

	[Fact]
	public void Chain_FailurePropagatesToEnd()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 404 };
		var result = MessageResult.Failed<int>("Not found", problemDetails);

		// Act
		var output = result
			.Map(x => x * 2)
			.Map(x => x.ToString())
			.Match(
				onSuccess: x => $"Result: {x}",
				onFailure: p => $"Error: {p?.ErrorCode}");

		// Assert
		output.ShouldBe("Error: 404");
	}

	[Fact]
	public void Chain_TapInMiddle()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var tappedValue = 0;

		// Act
		var output = result
			.Map(x => x * 2)
			.Tap(x => tappedValue = x)
			.Map(x => x.ToString());

		// Assert
		tappedValue.ShouldBe(84);
		output.ReturnValue.ShouldBe("84");
	}

	#endregion

	#region Null Guard Tests (ArgumentNullException)

	[Fact]
	public void MapAsync_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.MapAsync(async x => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return x.ToString(); }));
	}

	[Fact]
	public void MapAsync_ThrowArgumentNullException_WhenMapperIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.MapAsync<int, string>(null!));
	}

	[Fact]
	public async Task Map_OnTask_ThrowArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<IMessageResult<int>> resultTask = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Map(x => x.ToString()));
	}

	[Fact]
	public async Task Map_OnTask_ThrowArgumentNullException_WhenMapperIsNull()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Map<int, string>(null!));
	}

	[Fact]
	public void Bind_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			result.Bind(x => MessageResult.Success(x * 2)));
	}

	[Fact]
	public void Bind_ThrowArgumentNullException_WhenBinderIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			result.Bind<int, int>(null!));
	}

	[Fact]
	public void BindAsync_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.BindAsync(async x => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return MessageResult.Success(x * 2); }));
	}

	[Fact]
	public void BindAsync_ThrowArgumentNullException_WhenBinderIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.BindAsync<int, int>(null!));
	}

	[Fact]
	public async Task Bind_OnTask_ThrowArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<IMessageResult<int>> resultTask = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Bind(x => MessageResult.Success(x * 2)));
	}

	[Fact]
	public async Task Bind_OnTask_ThrowArgumentNullException_WhenBinderIsNull()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Bind<int, int>(null!));
	}

	[Fact]
	public void Match_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			result.Match(x => x.ToString(), _ => "Failed"));
	}

	[Fact]
	public void Match_ThrowArgumentNullException_WhenOnSuccessIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			result.Match(null!, _ => "Failed"));
	}

	[Fact]
	public void Match_ThrowArgumentNullException_WhenOnFailureIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			result.Match(x => x.ToString(), null!));
	}

	[Fact]
	public async Task Match_OnTask_ThrowArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<IMessageResult<int>> resultTask = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Match(x => x.ToString(), _ => "Failed"));
	}

	[Fact]
	public void MatchAsync_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.MatchAsync(async x => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return x.ToString(); },
				async _ => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return "Failed"; }));
	}

	[Fact]
	public void MatchAsync_ThrowArgumentNullException_WhenOnSuccessIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.MatchAsync(null!,
				async _ => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return "Failed"; }));
	}

	[Fact]
	public void MatchAsync_ThrowArgumentNullException_WhenOnFailureIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.MatchAsync(async x => { await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); return x.ToString(); },
				null!));
	}

	[Fact]
	public void Tap_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.Tap(_ => { }));
	}

	[Fact]
	public void Tap_ThrowArgumentNullException_WhenActionIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.Tap(null!));
	}

	[Fact]
	public void TapAsync_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.TapAsync(async _ => await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1)));
	}

	[Fact]
	public void TapAsync_ThrowArgumentNullException_WhenActionIsNull()
	{
		// Arrange
		var result = MessageResult.Success(42);

		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await result.TapAsync(null!));
	}

	[Fact]
	public async Task Tap_OnTask_ThrowArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<IMessageResult<int>> resultTask = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Tap(_ => { }));
	}

	[Fact]
	public async Task Tap_OnTask_ThrowArgumentNullException_WhenActionIsNull()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(42) as IMessageResult<int>);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.Tap(null!));
	}

	[Fact]
	public void GetValueOrDefault_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.GetValueOrDefault());
	}

	[Fact]
	public void GetValueOrThrow_ThrowArgumentNullException_WhenResultIsNull()
	{
		// Arrange
		IMessageResult<int> result = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => result.GetValueOrThrow());
	}

	[Fact]
	public async Task GetValueOrThrow_OnTask_ThrowArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<IMessageResult<int>> resultTask = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await resultTask.GetValueOrThrow());
	}

	#endregion

	#region Exception Propagation Tests

	[Fact]
	public void Map_PropagateMapperException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Mapper failed");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			result.Map<int, string>(x => throw expectedException));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task MapAsync_PropagateMapperException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Async mapper failed");

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await result.MapAsync<int, string>(async x =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				throw expectedException;
			}));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public void Bind_PropagateBinderException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Binder failed");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			result.Bind<int, int>(x => throw expectedException));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task BindAsync_PropagateBinderException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Async binder failed");

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await result.BindAsync<int, int>(async x =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				throw expectedException;
			}));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public void Tap_PropagateActionException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Tap action failed");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			result.Tap(_ => throw expectedException));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task TapAsync_PropagateActionException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Async tap action failed");

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await result.TapAsync(async _ =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				throw expectedException;
			}));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public void Match_PropagateOnSuccessException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("OnSuccess failed");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			result.Match(
				onSuccess: _ => throw expectedException,
				onFailure: _ => "Failed"));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public void Match_PropagateOnFailureException()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);
		var expectedException = new InvalidOperationException("OnFailure failed");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			result.Match(
				onSuccess: x => x.ToString(),
				onFailure: _ => throw expectedException));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task MatchAsync_PropagateOnSuccessException()
	{
		// Arrange
		var result = MessageResult.Success(42);
		var expectedException = new InvalidOperationException("Async OnSuccess failed");

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await result.MatchAsync(
				onSuccess: async _ =>
				{
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
					throw expectedException;
				},
				onFailure: async _ =>
				{
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
					return "Failed";
				}));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task MatchAsync_PropagateOnFailureException()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);
		var expectedException = new InvalidOperationException("Async OnFailure failed");

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await result.MatchAsync(
				onSuccess: async x =>
				{
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
					return x.ToString();
				},
				onFailure: async _ =>
				{
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
					throw expectedException;
				}));
		ex.ShouldBeSameAs(expectedException);
	}

	#endregion

	#region Async Failure Path Tests

	[Fact]
	public async Task MapAsync_PropagateFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 404 };
		var result = MessageResult.Failed<int>("Not found", problemDetails);
		var mapperCalled = false;

		// Act
		var mapped = await result.MapAsync(async x =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			mapperCalled = true;
			return x.ToString();
		});

		// Assert
		mapped.Succeeded.ShouldBeFalse();
		mapped.ProblemDetails.ErrorCode.ShouldBe(404);
		mapperCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task BindAsync_PropagateFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 500 };
		var result = MessageResult.Failed<int>("Error", problemDetails);
		var binderCalled = false;

		// Act
		var bound = await result.BindAsync(async x =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			binderCalled = true;
			return MessageResult.Success(x * 2);
		});

		// Assert
		bound.Succeeded.ShouldBeFalse();
		bound.ProblemDetails.ErrorCode.ShouldBe(500);
		binderCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task Map_OnTask_PropagateFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 403 };
		var resultTask = Task.FromResult(MessageResult.Failed<int>("Forbidden", problemDetails) as IMessageResult<int>);
		var mapperCalled = false;

		// Act
		var mapped = await resultTask.Map(x =>
		{
			mapperCalled = true;
			return x.ToString();
		});

		// Assert
		mapped.Succeeded.ShouldBeFalse();
		mapped.ProblemDetails.ErrorCode.ShouldBe(403);
		mapperCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task Bind_OnTask_PropagateFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 409 };
		var resultTask = Task.FromResult(MessageResult.Failed<int>("Conflict", problemDetails) as IMessageResult<int>);
		var binderCalled = false;

		// Act
		var bound = await resultTask.Bind(x =>
		{
			binderCalled = true;
			return MessageResult.Success(x * 2);
		});

		// Assert
		bound.Succeeded.ShouldBeFalse();
		bound.ProblemDetails.ErrorCode.ShouldBe(409);
		binderCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task Match_OnTask_ExecuteOnFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { Detail = "Task error" };
		var resultTask = Task.FromResult(MessageResult.Failed<int>("Error", problemDetails) as IMessageResult<int>);

		// Act
		var matched = await resultTask.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: p => $"Error: {p?.Detail}");

		// Assert
		matched.ShouldBe("Error: Task error");
	}

	[Fact]
	public async Task MatchAsync_ExecuteAsyncOnFailure()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { Detail = "Async error" };
		var result = MessageResult.Failed<int>("Error", problemDetails);

		// Act
		var matched = await result.MatchAsync(
			onSuccess: async x =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				return $"Value: {x}";
			},
			onFailure: async p =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				return $"Error: {p?.Detail}";
			});

		// Assert
		matched.ShouldBe("Error: Async error");
	}

	[Fact]
	public async Task TapAsync_NotExecuteSideEffect_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);
		var sideEffectExecuted = false;

		// Act
		var tapped = await result.TapAsync(async _ =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			sideEffectExecuted = true;
		});

		// Assert
		sideEffectExecuted.ShouldBeFalse();
		tapped.ShouldBeSameAs(result);
	}

	[Fact]
	public async Task Tap_OnTask_NotExecuteSideEffect_WhenFailed()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Failed<int>("Error", null) as IMessageResult<int>);
		var sideEffectExecuted = false;

		// Act
		var tapped = await resultTask.Tap(_ => sideEffectExecuted = true);

		// Assert
		sideEffectExecuted.ShouldBeFalse();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void Map_PreserveErrorMessage()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Custom error message", null);

		// Act
		var mapped = result.Map(x => x.ToString());

		// Assert
		mapped.ErrorMessage.ShouldBe("Custom error message");
	}

	[Fact]
	public void Bind_PreserveOriginalErrorMessage()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Original error", null);

		// Act
		var bound = result.Bind(x => MessageResult.Success(x.ToString()));

		// Assert
		bound.ErrorMessage.ShouldBe("Original error");
	}

	[Fact]
	public void Match_HandleNullProblemDetails()
	{
		// Arrange
		var result = MessageResult.Failed<int>("Error", null);

		// Act
		var matched = result.Match(
			onSuccess: x => $"Value: {x}",
			onFailure: p => p is null ? "Null problem" : "Has problem");

		// Assert
		matched.ShouldBe("Null problem");
	}

	[Fact]
	public void GetValueOrDefault_WithReferenceType_ReturnNull_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<string>("Error", null);

		// Act
		var value = result.GetValueOrDefault();

		// Assert
		value.ShouldBeNull();
	}

	[Fact]
	public void GetValueOrDefault_WithNullableValueType_ReturnNull_WhenFailed()
	{
		// Arrange
		var result = MessageResult.Failed<int?>("Error", null);

		// Act
		var value = result.GetValueOrDefault();

		// Assert
		value.ShouldBeNull();
	}

	[Fact]
	public void GetValueOrThrow_IncludeProblemDetailsInExceptionData()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails
		{
			Type = "urn:error:test",
			Title = "Test Error",
			Detail = "Test failure",
			ErrorCode = 500,
		};
		var result = MessageResult.Failed<int>("Error", problemDetails);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => result.GetValueOrThrow());
		var data = ex.Data["ProblemDetails"] as IMessageProblemDetails;
		_ = data.ShouldNotBeNull();
		data.Type.ShouldBe("urn:error:test");
		data.Title.ShouldBe("Test Error");
		data.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public async Task Map_OnTask_WithFaultedTask_PropagateException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Task faulted");
		var resultTask = Task.FromException<IMessageResult<int>>(expectedException);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await resultTask.Map(x => x.ToString()));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task Bind_OnTask_WithFaultedTask_PropagateException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Task faulted");
		var resultTask = Task.FromException<IMessageResult<int>>(expectedException);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await resultTask.Bind(x => MessageResult.Success(x * 2)));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task Match_OnTask_WithFaultedTask_PropagateException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Task faulted");
		var resultTask = Task.FromException<IMessageResult<int>>(expectedException);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await resultTask.Match(x => x.ToString(), _ => "Failed"));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task Tap_OnTask_WithFaultedTask_PropagateException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Task faulted");
		var resultTask = Task.FromException<IMessageResult<int>>(expectedException);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await resultTask.Tap(_ => { }));
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task GetValueOrThrow_OnTask_WithFaultedTask_PropagateException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Task faulted");
		var resultTask = Task.FromException<IMessageResult<int>>(expectedException);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await resultTask.GetValueOrThrow());
		ex.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public void Chain_MultipleMapOperations()
	{
		// Arrange
		var result = MessageResult.Success(5);

		// Act
		var output = result
			.Map(x => x + 1)   // 6
			.Map(x => x * 2)   // 12
			.Map(x => x - 4)   // 8
			.Map(x => x / 2);  // 4

		// Assert
		output.Succeeded.ShouldBeTrue();
		output.ReturnValue.ShouldBe(4);
	}

	[Fact]
	public void Chain_MultipleBind_FirstFailureWins()
	{
		// Arrange
		var result = MessageResult.Success(10);
		var firstProblem = new MessageProblemDetails { ErrorCode = 400 };
		var secondProblem = new MessageProblemDetails { ErrorCode = 500 };

		// Act
		var output = result
			.Bind(x => MessageResult.Failed<int>("First failure", firstProblem))
			.Bind(x => MessageResult.Failed<int>("Second failure", secondProblem))
			.Bind(x => MessageResult.Success(x * 2));

		// Assert
		output.Succeeded.ShouldBeFalse();
		output.ProblemDetails.ErrorCode.ShouldBe(400);
		output.ErrorMessage.ShouldBe("First failure");
	}

	[Fact]
	public async Task Chain_AsyncAndSyncOperations()
	{
		// Arrange
		var resultTask = Task.FromResult(MessageResult.Success(10) as IMessageResult<int>);

		// Act
		var output = await resultTask
			.Map(x => x * 2)    // 20 (sync)
			.Bind(x => MessageResult.Success(x + 5));  // 25 (sync via Task extension)

		// Assert
		output.Succeeded.ShouldBeTrue();
		output.ReturnValue.ShouldBe(25);
	}

	#endregion
}
