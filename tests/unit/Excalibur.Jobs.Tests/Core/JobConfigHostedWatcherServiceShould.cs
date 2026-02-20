// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JobConfigHostedWatcherService{TJob, TConfig}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobConfigHostedWatcherServiceShould : IDisposable
{
	private readonly IScheduler _scheduler = A.Fake<IScheduler>();
	private readonly WatcherTestOptionsMonitor<WatcherTestJobConfig> _monitor;
	private readonly JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig> _service;

	public JobConfigHostedWatcherServiceShould()
	{
		var config = new WatcherTestJobConfig { Disabled = false };
		_monitor = new WatcherTestOptionsMonitor<WatcherTestJobConfig>(config);

		_service = new JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>(
			_scheduler,
			_monitor,
			NullLogger<JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>>.Instance);
	}

	[Fact]
	public async Task StartAsync_ResumeJob_WhenNotDisabled()
	{
		// Act
		await _service.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _scheduler.ResumeJob(
			A<JobKey>.That.Matches(k => k.Name == "TestJob"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StartAsync_PauseJob_WhenDisabled()
	{
		// Arrange
		var config = new WatcherTestJobConfig { Disabled = true };
		var monitor = new WatcherTestOptionsMonitor<WatcherTestJobConfig>(config);
		var service = new JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>(
			_scheduler, monitor,
			NullLogger<JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>>.Instance);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _scheduler.PauseJob(
			A<JobKey>.That.Matches(k => k.Name == "TestJob"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		service.Dispose();
	}

	[Fact]
	public async Task StartAsync_HandlesNullScheduler()
	{
		// Arrange
		var config = new WatcherTestJobConfig();
		var monitor = new WatcherTestOptionsMonitor<WatcherTestJobConfig>(config);
		var service = new JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>(
			null, monitor,
			NullLogger<JobConfigHostedWatcherService<WatcherTestJob, WatcherTestJobConfig>>.Instance);

		// Act — should not throw when scheduler is null
		await service.StartAsync(CancellationToken.None);

		service.Dispose();
	}

	[Fact]
	public async Task StopAsync_DisposesChangeListener()
	{
		// Arrange
		await _service.StartAsync(CancellationToken.None);

		// Act
		await _service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_DoesNothing_WhenDisposed()
	{
		// Arrange
		_service.Dispose();

		// Act — should return early
		await _service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Act & Assert — should not throw
		_service.Dispose();
		_service.Dispose();
	}

	public void Dispose()
	{
		_service.Dispose();
	}
}

internal sealed class WatcherTestJobConfig : JobConfig
{
	public WatcherTestJobConfig()
	{
		JobName = "TestJob";
		JobGroup = "TestGroup";
	}
}

internal sealed class WatcherTestJob : IConfigurableJob<WatcherTestJobConfig>
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Test implementation of IOptionsMonitor with OnChange support.
/// </summary>
internal sealed class WatcherTestOptionsMonitor<T>(T initialValue) : IOptionsMonitor<T>
{
	private readonly List<Action<T, string?>> _listeners = [];

	public T CurrentValue { get; } = initialValue;

	public T Get(string? name) => CurrentValue;

	public IDisposable? OnChange(Action<T, string?> listener)
	{
		_listeners.Add(listener);
		return new ChangeListenerDisposable(this, listener);
	}

	private sealed class ChangeListenerDisposable(WatcherTestOptionsMonitor<T> monitor, Action<T, string?> listener) : IDisposable
	{
		public void Dispose() => monitor._listeners.Remove(listener);
	}
}
