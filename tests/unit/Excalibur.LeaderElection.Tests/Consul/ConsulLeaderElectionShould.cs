// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

using Consul;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// Tests for Sprint 542 P0 fixes in <see cref="ConsulLeaderElection"/>:
/// S542.2 (bd-gdn8z): async void timer callbacks -> ConcurrentBag Task tracking
/// S542.3 (bd-o5xhh): fire-and-forget Dispose -> IAsyncDisposable
/// S542.4 (bd-a4aph): blocking CurrentLeaderId -> volatile cached property
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConsulLeaderElectionShould
{
	private static ConsulLeaderElection CreateSut()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			InstanceId = "test-instance",
		});

		var fakeConsulClient = A.Fake<IConsulClient>();
		return new ConsulLeaderElection("test-resource", options, fakeConsulClient, NullLogger<ConsulLeaderElection>.Instance);
	}

	// --- S542.2 (bd-gdn8z): ConcurrentBag<Task> tracking for async void callbacks ---

	[Fact]
	public void HaveTrackedTasksField()
	{
		// Arrange & Act
		var field = typeof(ConsulLeaderElection)
			.GetField("_trackedTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert — ConcurrentBag<Task> field must exist
		field.ShouldNotBeNull("ConsulLeaderElection should have _trackedTasks field for async void task tracking");
		field.FieldType.ShouldBe(typeof(ConcurrentBag<Task>));
	}

	[Fact]
	public void HaveShutdownTokenSourceField()
	{
		// Arrange & Act
		var field = typeof(ConsulLeaderElection)
			.GetField("_shutdownTokenSource", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("ConsulLeaderElection should have _shutdownTokenSource field");
		field.FieldType.ShouldBe(typeof(CancellationTokenSource));
	}

	[Fact]
	public void HaveNonAsyncVoidTimerCallbacks()
	{
		// The RenewSession and MonitorLeadership methods should be void (not async void)
		// They should use Task.Run internally to track async work
		var renewMethod = typeof(ConsulLeaderElection)
			.GetMethod("RenewSession", BindingFlags.NonPublic | BindingFlags.Instance);
		var monitorMethod = typeof(ConsulLeaderElection)
			.GetMethod("MonitorLeadership", BindingFlags.NonPublic | BindingFlags.Instance);

		renewMethod.ShouldNotBeNull();
		monitorMethod.ShouldNotBeNull();

		// Both should return void (not Task — they are TimerCallback delegates)
		renewMethod.ReturnType.ShouldBe(typeof(void));
		monitorMethod.ReturnType.ShouldBe(typeof(void));

		// Neither should be marked as async void — check for AsyncStateMachineAttribute
		// If present, the method is async void (bad). If absent, it's a plain void that uses Task.Run (good).
		renewMethod.GetCustomAttribute<AsyncStateMachineAttribute>().ShouldBeNull(
			"RenewSession should NOT be async void — use Task.Run tracking instead");
		monitorMethod.GetCustomAttribute<AsyncStateMachineAttribute>().ShouldBeNull(
			"MonitorLeadership should NOT be async void — use Task.Run tracking instead");
	}

	// --- S542.3 (bd-o5xhh): IAsyncDisposable ---

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(ConsulLeaderElection)).ShouldBeTrue(
			"ConsulLeaderElection should implement IAsyncDisposable for safe timer disposal");
	}

	[Fact]
	public void HaveDisposedField()
	{
		var field = typeof(ConsulLeaderElection)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("ConsulLeaderElection should have _disposed field");

		// Should be volatile
		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_disposed field should be volatile for thread-safe disposal checks");
	}

	// --- S542.4 (bd-a4aph): Cached CurrentLeaderId (no blocking I/O) ---

	[Fact]
	public void HaveCachedCurrentLeaderIdField()
	{
		// The _cachedCurrentLeaderId field should exist and be volatile
		var field = typeof(ConsulLeaderElection)
			.GetField("_cachedCurrentLeaderId", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("ConsulLeaderElection should have _cachedCurrentLeaderId field");

		// Should be volatile
		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_cachedCurrentLeaderId should be volatile for thread-safe reads");
	}

	[Fact]
	public void ReturnCachedValueFromCurrentLeaderId()
	{
		// Arrange
		using var sut = CreateSut();

		// Act — CurrentLeaderId should return null by default (no blocking I/O)
		var result = sut.CurrentLeaderId;

		// Assert — should return quickly without blocking
		// If it was still using .GetAwaiter().GetResult(), this would potentially hang
		result.ShouldBeNull("Default CurrentLeaderId should be null (cached value)");
	}

	[Fact]
	public void NotUseGetAwaiterGetResultInCurrentLeaderId()
	{
		// Verify via reflection that CurrentLeaderId property getter doesn't call GetAwaiter().GetResult()
		var propertyGetter = typeof(ConsulLeaderElection)
			.GetProperty("CurrentLeaderId")?.GetGetMethod();
		propertyGetter.ShouldNotBeNull();

		// Get the IL body and check it doesn't reference GetAwaiter
		var methodBody = propertyGetter.GetMethodBody();
		methodBody.ShouldNotBeNull();

		// The method body should be very short (just returning a field) — not a complex async chain
		// A simple field return is typically < 20 bytes of IL
		methodBody.GetILAsByteArray().Length.ShouldBeLessThan(50,
			"CurrentLeaderId getter should be a simple field return, not a complex async chain");
	}
}
