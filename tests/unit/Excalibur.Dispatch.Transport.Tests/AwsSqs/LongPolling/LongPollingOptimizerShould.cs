// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.SqsPollingStatus;
using IAwsLongPollingStrategy = Excalibur.Dispatch.Transport.ILongPollingStrategy;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class LongPollingOptimizerShould : IDisposable
{
	private readonly ILongPollingReceiver _receiver;
	private readonly IAwsLongPollingStrategy _strategy;
	private readonly IPollingMetricsCollector _metricsCollector;
	private readonly LongPollingOptions _config;
	private readonly LongPollingOptimizer _optimizer;

	public LongPollingOptimizerShould()
	{
		_receiver = A.Fake<ILongPollingReceiver>();
		_strategy = A.Fake<IAwsLongPollingStrategy>();
		_metricsCollector = A.Fake<IPollingMetricsCollector>();
		_config = new LongPollingOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};
		_config.Processing.EnableRequestCoalescing = false;

		_optimizer = new LongPollingOptimizer(
			_receiver,
			_strategy,
			_metricsCollector,
			_config,
			NullLogger<LongPollingOptimizer>.Instance);
	}

	[Fact]
	public void ThrowWhenReceiverIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new LongPollingOptimizer(
			null!, _strategy, _metricsCollector, _config, NullLogger<LongPollingOptimizer>.Instance));
	}

	[Fact]
	public void ThrowWhenStrategyIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new LongPollingOptimizer(
			_receiver, null!, _metricsCollector, _config, NullLogger<LongPollingOptimizer>.Instance));
	}

	[Fact]
	public void ThrowWhenMetricsCollectorIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new LongPollingOptimizer(
			_receiver, _strategy, null!, _config, NullLogger<LongPollingOptimizer>.Instance));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new LongPollingOptimizer(
			_receiver, _strategy, _metricsCollector, null!, NullLogger<LongPollingOptimizer>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new LongPollingOptimizer(
			_receiver, _strategy, _metricsCollector, _config, null!));
	}

	[Fact]
	public async Task ReceiveMessagesFromReceiverDirectly()
	{
		// Arrange
		var expectedMessages = new List<Message> { new() { MessageId = "msg-1" } };
		A.CallTo(() => _receiver.ReceiveMessagesAsync(
			A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<Message>>(expectedMessages));

		// Act
		var result = await _optimizer.ReceiveMessagesAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].MessageId.ShouldBe("msg-1");
	}

	[Fact]
	public async Task GetHealthStatusWhenHealthy()
	{
		// Arrange
		A.CallTo(() => _receiver.GetStatisticsAsync())
			.Returns(new ValueTask<ReceiverStatistics>(new ReceiverStatistics
			{
				TotalReceiveOperations = 100,
				TotalMessagesReceived = 500,
				TotalMessagesDeleted = 500,
				VisibilityTimeoutOptimizations = 0,
				LastReceiveTime = DateTimeOffset.UtcNow,
				PollingStatus = AwsPollingStatus.Active,
			}));

		// The optimizer now checks for ILongPollingStrategyAdmin; configure the fake
		// to implement the admin interface as well
		var adminStrategy = A.Fake<IAwsLongPollingStrategy>(
			o => o.Implements<ILongPollingStrategyAdmin>());
		A.CallTo(() => ((ILongPollingStrategyAdmin)adminStrategy).GetStatisticsAsync())
			.Returns(new ValueTask<Excalibur.Dispatch.Transport.LongPollingStatistics>(
				new Excalibur.Dispatch.Transport.LongPollingStatistics
				{
					TotalReceives = 100,
					EmptyReceives = 10,
					TotalMessages = 500,
					ApiCallsSaved = 50,
					CurrentLoadFactor = 0.5,
					CurrentWaitTime = TimeSpan.FromSeconds(10),
				}));

		using var optimizer = new LongPollingOptimizer(
			_receiver, adminStrategy, _metricsCollector, _config,
			NullLogger<LongPollingOptimizer>.Instance);

		// Act
		var health = await optimizer.GetHealthStatusAsync();

		// Assert
		health.IsHealthy.ShouldBeTrue();
		health.TotalMessagesProcessed.ShouldBe(500);
	}

	[Fact]
	public async Task GetHealthStatusWhenUnhealthy()
	{
		// Arrange
		A.CallTo(() => _receiver.GetStatisticsAsync())
			.Returns(new ValueTask<ReceiverStatistics>(new ReceiverStatistics
			{
				TotalReceiveOperations = 100,
				TotalMessagesReceived = 5,
				TotalMessagesDeleted = 5,
				VisibilityTimeoutOptimizations = 0,
				LastReceiveTime = DateTimeOffset.UtcNow,
				PollingStatus = AwsPollingStatus.Error,
			}));

		// EmptyReceiveRate = 95/100 = 0.95
		var adminStrategy = A.Fake<IAwsLongPollingStrategy>(
			o => o.Implements<ILongPollingStrategyAdmin>());
		A.CallTo(() => ((ILongPollingStrategyAdmin)adminStrategy).GetStatisticsAsync())
			.Returns(new ValueTask<Excalibur.Dispatch.Transport.LongPollingStatistics>(
				new Excalibur.Dispatch.Transport.LongPollingStatistics
				{
					TotalReceives = 100,
					EmptyReceives = 95,
				}));

		using var optimizer = new LongPollingOptimizer(
			_receiver, adminStrategy, _metricsCollector, _config,
			NullLogger<LongPollingOptimizer>.Instance);

		// Act
		var health = await optimizer.GetHealthStatusAsync();

		// Assert
		health.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task GetHealthStatusWhenExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _receiver.GetStatisticsAsync())
			.ThrowsAsync(new InvalidOperationException("Connection failed"));

		// Act
		var health = await _optimizer.GetHealthStatusAsync();

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.Status.ShouldBe("Error");
		health.Details.ShouldContainKey("Error");
	}

	[Fact]
	public async Task GetEmptyOptimizationStatisticsWhenNoQueues()
	{
		// Act
		var stats = await _optimizer.GetOptimizationStatisticsAsync();

		// Assert
		stats.ShouldBeEmpty();
	}

	[Fact]
	public void ThrowObjectDisposedExceptionAfterDispose()
	{
		// Arrange
		_optimizer.Dispose();

		// Act & Assert
		Should.ThrowAsync<ObjectDisposedException>(
			() => _optimizer.ReceiveMessagesAsync("https://example.com/queue", CancellationToken.None));
	}

	[Fact]
	public void DisposeWithoutThrowing()
	{
		// Act & Assert
		_optimizer.Dispose();
		_optimizer.Dispose();
	}

	[Fact]
	public async Task GetHealthStatus_UsesAdminInterface_WhenStrategyImplementsIt()
	{
		// Arrange -- strategy that implements ILongPollingStrategyAdmin
		var adminStrategy = A.Fake<IAwsLongPollingStrategy>(
			o => o.Implements<ILongPollingStrategyAdmin>());
		A.CallTo(() => ((ILongPollingStrategyAdmin)adminStrategy).GetStatisticsAsync())
			.Returns(new ValueTask<Excalibur.Dispatch.Transport.LongPollingStatistics>(
				new Excalibur.Dispatch.Transport.LongPollingStatistics
				{
					TotalReceives = 100,
					EmptyReceives = 20,
					TotalMessages = 400,
					CurrentLoadFactor = 0.6,
					CurrentWaitTime = TimeSpan.FromSeconds(10),
				}));
		A.CallTo(() => _receiver.GetStatisticsAsync())
			.Returns(new ValueTask<ReceiverStatistics>(new ReceiverStatistics
			{
				TotalReceiveOperations = 100,
				TotalMessagesReceived = 400,
				TotalMessagesDeleted = 400,
				VisibilityTimeoutOptimizations = 0,
				PollingStatus = AwsPollingStatus.Active,
				LastReceiveTime = DateTimeOffset.UtcNow,
			}));

		using var optimizer = new LongPollingOptimizer(
			_receiver, adminStrategy, _metricsCollector, _config,
			NullLogger<LongPollingOptimizer>.Instance);

		// Act
		var health = await optimizer.GetHealthStatusAsync();

		// Assert -- admin interface was used, so EfficiencyScore reflects actual EmptyReceiveRate
		health.IsHealthy.ShouldBeTrue();
		health.EfficiencyScore.ShouldBe(0.8, 0.01); // 1 - (20/100) = 0.8
	}

	[Fact]
	public async Task GetHealthStatus_FallsBackToDefaultStats_WhenStrategyLacksAdminInterface()
	{
		// Arrange -- plain strategy without ILongPollingStrategyAdmin
		var plainStrategy = A.Fake<IAwsLongPollingStrategy>();
		A.CallTo(() => _receiver.GetStatisticsAsync())
			.Returns(new ValueTask<ReceiverStatistics>(new ReceiverStatistics
			{
				TotalReceiveOperations = 50,
				TotalMessagesReceived = 200,
				TotalMessagesDeleted = 200,
				VisibilityTimeoutOptimizations = 0,
				PollingStatus = AwsPollingStatus.Active,
				LastReceiveTime = DateTimeOffset.UtcNow,
			}));

		using var optimizer = new LongPollingOptimizer(
			_receiver, plainStrategy, _metricsCollector, _config,
			NullLogger<LongPollingOptimizer>.Instance);

		// Act
		var health = await optimizer.GetHealthStatusAsync();

		// Assert -- fallback: default stats have EmptyReceiveRate=0, so EfficiencyScore=1.0
		health.IsHealthy.ShouldBeTrue();
		health.EfficiencyScore.ShouldBe(1.0); // 1 - 0.0 (default EmptyReceiveRate)
	}

	public void Dispose()
	{
		_optimizer.Dispose();
		_receiver.Dispose();
	}
}

#pragma warning restore CA2012
