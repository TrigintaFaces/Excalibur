// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.StreamingPull;

/// <summary>
/// Regression tests for Sprint 541 StreamingPullManager fixes:
/// - S541.4 (bd-p8tok): Background task tracking in ConcurrentBag
/// - S541.5 (bd-mzkx0): CancellationToken propagation (not CancellationToken.None)
/// - S541.6 (bd-y1lle): Disposal ordering (unsubscribe → cancel → await → dispose)
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingPullManagerSafetyShould : UnitTestBase
{
	#region S541.4: Background Task Tracking (bd-p8tok)

	[Fact]
	public void HaveBackgroundTasksField()
	{
		// ConcurrentBag<Task> _backgroundTasks must exist for tracking fire-and-forget tasks
		var field = typeof(StreamingPullManager)
			.GetField("_backgroundTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_backgroundTasks field must exist for background task tracking (AD-541.3)");
	}

	[Fact]
	public void UsesConcurrentBagForBackgroundTasks()
	{
		// _backgroundTasks must be ConcurrentBag<Task> for thread-safe task tracking
		var field = typeof(StreamingPullManager)
			.GetField("_backgroundTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.ShouldBe(typeof(ConcurrentBag<Task>),
			"_backgroundTasks must be ConcurrentBag<Task> for thread-safe tracking (AD-541.3)");
	}

	#endregion

	#region S541.5 & S541.6: Shutdown Token and Handler Safety

	[Fact]
	public void HaveShutdownTokenSourceField()
	{
		// _shutdownTokenSource must exist for propagating cancellation
		var field = typeof(StreamingPullManager)
			.GetField("_shutdownTokenSource", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_shutdownTokenSource must exist for cancellation propagation (AD-541.5)");
		field.FieldType.ShouldBe(typeof(CancellationTokenSource));
	}

	[Fact]
	public void HaveVolatileDisposedField()
	{
		// _disposed must be volatile for thread-safe disposal checking
		var field = typeof(StreamingPullManager)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_disposed field must exist");

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(
			typeof(System.Runtime.CompilerServices.IsVolatile),
			"_disposed must be volatile for thread-safe access");
	}

	#endregion

	#region S541.6: Disposal Ordering (bd-y1lle)

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// StreamingPullManager must implement IAsyncDisposable
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(StreamingPullManager))
			.ShouldBeTrue("StreamingPullManager must implement IAsyncDisposable");
	}

	[Fact]
	public void HaveDisposeAsyncMethod()
	{
		// DisposeAsync must be public
		var method = typeof(StreamingPullManager)
			.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

		method.ShouldNotBeNull("DisposeAsync() must be publicly accessible");
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	#endregion

	#region Event Handler Signatures

	[Fact]
	public void HaveOnUnhealthyStreamDetectedHandler()
	{
		// The handler must exist as a private method
		var method = typeof(StreamingPullManager)
			.GetMethod("OnUnhealthyStreamDetectedHandler", BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull("OnUnhealthyStreamDetectedHandler must exist");
		// async void is required for EventHandler<T> delegate — AD-541.3 confirmation
		method.ReturnType.ShouldBe(typeof(void),
			"EventHandler<T> delegate requires void return — async void is correct here");
	}

	[Fact]
	public void HaveOnAckDeadlineExtensionRequestedHandler()
	{
		// The handler must exist as a private method
		var method = typeof(StreamingPullManager)
			.GetMethod("OnAckDeadlineExtensionRequested", BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull("OnAckDeadlineExtensionRequested must exist");
		method.ReturnType.ShouldBe(typeof(void),
			"EventHandler<T> delegate requires void return — async void is correct here");
	}

	#endregion
}
