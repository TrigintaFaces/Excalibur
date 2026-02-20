// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.15 (bd-38c8t):
/// RedisDistributedJobLock + RedisLeadershipToken DisposeAsync uses CancellationTokenSource(5s)
/// instead of CancellationToken.None, preventing indefinite hangs during disposal.
/// </summary>
/// <remarks>
/// Both types are internal sealed, so we access them via Assembly.GetType() reflection.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class RedisDisposeAsyncShould
{
	private static readonly Assembly JobsAssembly = typeof(Excalibur.Jobs.Coordination.IDistributedJobLock).Assembly;

	private static Type GetInternalType(string fullName)
	{
		var type = JobsAssembly.GetType(fullName);
		type.ShouldNotBeNull($"Type '{fullName}' should exist in Excalibur.Jobs assembly");
		return type;
	}

	// --- RedisDistributedJobLock ---

	[Fact]
	public void RedisDistributedJobLock_ImplementsIAsyncDisposable()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisDistributedJobLock");
		typeof(IAsyncDisposable).IsAssignableFrom(type).ShouldBeTrue(
			"RedisDistributedJobLock should implement IAsyncDisposable");
	}

	[Fact]
	public void RedisDistributedJobLock_HasDisposeAsyncMethod()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisDistributedJobLock");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("RedisDistributedJobLock should have DisposeAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	[Fact]
	public void RedisDistributedJobLock_DisposeAsyncIsAsync()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisDistributedJobLock");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();

		var stateMachineAttr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
		stateMachineAttr.ShouldNotBeNull("DisposeAsync should be async (state machine) — needed for timeout CTS pattern");
	}

	[Fact]
	public void RedisDistributedJobLock_DisposeAsyncCatchesOperationCanceledException()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisDistributedJobLock");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();

		var stateMachineAttr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
		stateMachineAttr.ShouldNotBeNull();

		var moveNext = stateMachineAttr.StateMachineType
			.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

		moveNext.ShouldNotBeNull();
		var body = moveNext.GetMethodBody();
		body.ShouldNotBeNull();

		var handlers = body.ExceptionHandlingClauses;
		var catchesOce = handlers.Any(h =>
			h.Flags == ExceptionHandlingClauseOptions.Clause &&
			h.CatchType == typeof(OperationCanceledException));

		catchesOce.ShouldBeTrue(
			"DisposeAsync should catch OperationCanceledException for disposal timeout (S542.15)");
	}

	// --- RedisLeadershipToken ---

	[Fact]
	public void RedisLeadershipToken_ImplementsIAsyncDisposable()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisLeadershipToken");
		typeof(IAsyncDisposable).IsAssignableFrom(type).ShouldBeTrue(
			"RedisLeadershipToken should implement IAsyncDisposable");
	}

	[Fact]
	public void RedisLeadershipToken_HasDisposeAsyncMethod()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisLeadershipToken");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("RedisLeadershipToken should have DisposeAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	[Fact]
	public void RedisLeadershipToken_DisposeAsyncIsAsync()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisLeadershipToken");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();

		var stateMachineAttr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
		stateMachineAttr.ShouldNotBeNull("DisposeAsync should be async (state machine) — needed for timeout CTS pattern");
	}

	[Fact]
	public void RedisLeadershipToken_DisposeAsyncCatchesOperationCanceledException()
	{
		var type = GetInternalType("Excalibur.Jobs.Coordination.RedisLeadershipToken");
		var method = type.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();

		var stateMachineAttr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
		stateMachineAttr.ShouldNotBeNull();

		var moveNext = stateMachineAttr.StateMachineType
			.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

		moveNext.ShouldNotBeNull();
		var body = moveNext.GetMethodBody();
		body.ShouldNotBeNull();

		var handlers = body.ExceptionHandlingClauses;
		var catchesOce = handlers.Any(h =>
			h.Flags == ExceptionHandlingClauseOptions.Clause &&
			h.CatchType == typeof(OperationCanceledException));

		catchesOce.ShouldBeTrue(
			"DisposeAsync should catch OperationCanceledException for disposal timeout (S542.15)");
	}
}
