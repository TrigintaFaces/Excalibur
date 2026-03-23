// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Integration;

/// <summary>
/// Tests for the DispatchIntegrationTestHarness infrastructure.
/// Verifies the harness correctly wires DI, dispatches through the pipeline,
/// and provides the handler execution tracker.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class DispatchIntegrationHarnessShould : IAsyncDisposable
{
	private DispatchIntegrationTestHarness? _harness;

	[Fact]
	public async Task CreateWithDefaultConfiguration()
	{
		// Act
		_harness = DispatchIntegrationTestHarness.Create();

		// Assert
		_harness.Dispatcher.ShouldNotBeNull();
		_harness.Services.ShouldNotBeNull();
		_harness.Tracker.ShouldNotBeNull();

		await Task.CompletedTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task ResolveDispatcherFromServiceProvider()
	{
		// Arrange
		_harness = DispatchIntegrationTestHarness.Create();

		// Act
		var dispatcher = _harness.Services.GetService<IDispatcher>();

		// Assert
		dispatcher.ShouldNotBeNull();
		dispatcher.ShouldBeSameAs(_harness.Dispatcher);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task DispatchCommandThroughPipeline()
	{
		// Arrange
		_harness = DispatchIntegrationTestHarness.Create(
			configure: services =>
			{
				services.AddTransient<IActionHandler<TestCommand, string>, TestCommandHandler>();
				services.AddDispatchHandlers(typeof(DispatchIntegrationHarnessShould).Assembly);
			});

		var command = new TestCommand { Name = "integration-test" };

		// Act
		var result = await _harness.DispatchAsync<TestCommand, string>(command);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task TrackHandlerExecutions()
	{
		// Arrange
		_harness = DispatchIntegrationTestHarness.Create(
			configure: services =>
			{
				services.AddTransient<IActionHandler<TrackedCommand, string>, TrackedCommandHandler>();
				services.AddDispatchHandlers(typeof(DispatchIntegrationHarnessShould).Assembly);
			});

		// Act
		var command = new TrackedCommand { Value = 42 };
		await _harness.DispatchAsync<TrackedCommand, string>(command);

		// Assert
		_harness.Tracker.CountFor<TrackedCommand>().ShouldBe(1);
		var executions = _harness.Tracker.GetFor<TrackedCommand>();
		executions.Count.ShouldBe(1);
		executions[0].MessageType.ShouldBe(typeof(TrackedCommand));
		executions[0].HandlerType.ShouldBe(typeof(TrackedCommandHandler));
	}

	[Fact]
	public async Task DisposeCleanly()
	{
		// Arrange
		_harness = DispatchIntegrationTestHarness.Create();

		// Act & Assert -- should not throw
		await _harness.DisposeAsync();
		_harness = null; // Prevent double dispose in cleanup
	}

	[Fact]
	public async Task SupportCustomServiceRegistration()
	{
		// Arrange
		var customValue = "custom-injected-value";
		_harness = DispatchIntegrationTestHarness.Create(
			configure: services =>
			{
				services.AddSingleton(new TestDependency(customValue));
			});

		// Act
		var dep = _harness.Services.GetService<TestDependency>();

		// Assert
		dep.ShouldNotBeNull();
		dep.Value.ShouldBe(customValue);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	[Fact]
	public void TrackerClearsExecutions()
	{
		// Arrange
		var tracker = new HandlerExecutionTracker();
		tracker.Record(typeof(string), typeof(int), 42, null);
		tracker.Record(typeof(string), typeof(int), 99, null);

		// Act
		tracker.Clear();

		// Assert
		tracker.Executions.ShouldBeEmpty();
	}

	[Fact]
	public void TrackerCountsCorrectlyByType()
	{
		// Arrange
		var tracker = new HandlerExecutionTracker();
		tracker.Record(typeof(string), typeof(int), 1, null);
		tracker.Record(typeof(string), typeof(int), 2, null);
		tracker.Record(typeof(string), typeof(string), "hello", null);

		// Assert
		tracker.CountFor<int>().ShouldBe(2);
		tracker.CountFor<string>().ShouldBe(1);
		tracker.CountFor<double>().ShouldBe(0);
	}

	public async ValueTask DisposeAsync()
	{
		if (_harness is not null)
		{
			await _harness.DisposeAsync().ConfigureAwait(false);
		}
	}

	#region Test Types

	internal sealed class TestCommand : IDispatchAction<string>
	{
		public string Name { get; set; } = string.Empty;
	}

	internal sealed class TestCommandHandler : IActionHandler<TestCommand, string>
	{
		public Task<string> HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			return Task.FromResult($"Handled: {action.Name}");
		}
	}

	internal sealed class TrackedCommand : IDispatchAction<string>
	{
		public int Value { get; set; }
	}

	internal sealed class TrackedCommandHandler : IActionHandler<TrackedCommand, string>
	{
		private readonly HandlerExecutionTracker _tracker;

		public TrackedCommandHandler(HandlerExecutionTracker tracker)
		{
			_tracker = tracker;
		}

		public Task<string> HandleAsync(TrackedCommand action, CancellationToken cancellationToken)
		{
			var result = $"Tracked: {action.Value}";
			_tracker.Record(typeof(TrackedCommandHandler), typeof(TrackedCommand), action, result);
			return Task.FromResult(result);
		}
	}

	internal sealed record TestDependency(string Value);

	#endregion
}
