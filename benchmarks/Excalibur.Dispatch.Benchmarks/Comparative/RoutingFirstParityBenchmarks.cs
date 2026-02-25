// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention
#pragma warning disable SA1402 // File may only contain a single type - benchmarks with supporting types

/// <summary>
/// Routing-first parity scenarios used for MediatR + transport replacement validation.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class RoutingFirstParityBenchmarks
{
	private static readonly RoutingDecision LocalRoutingDecision = RoutingDecision.Success("local", []);
	private static readonly RoutingDecision AwsSqsRoutingDecision = RoutingDecision.Success("aws-sqs", ["aws-sqs"]);
	private static readonly RoutingDecision AzureServiceBusRoutingDecision =
		RoutingDecision.Success("azure-servicebus", ["azure-servicebus"]);
	private static readonly RoutingDecision KafkaRoutingDecision = RoutingDecision.Success("kafka", ["kafka"]);
	private static readonly RoutingDecision RabbitMqRoutingDecision = RoutingDecision.Success("rabbitmq", ["rabbitmq"]);

	private IServiceProvider? _serviceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _contextFactory;
	private ProviderParityMessageBus? _awsSqsBus;
	private ProviderParityMessageBus? _azureServiceBus;
	private ProviderParityMessageBus? _kafkaBus;
	private ProviderParityMessageBus? _rabbitMqBus;

	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBenchmarkDispatch();
		_ = services.AddTransient<IActionHandler<RoutingFirstCommand>, RoutingFirstCommandHandler>();
		_ = services.AddTransient<IActionHandler<RoutingFirstQuery, int>, RoutingFirstQueryHandler>();

		_awsSqsBus = new ProviderParityMessageBus("aws-sqs");
		_azureServiceBus = new ProviderParityMessageBus("azure-servicebus");
		_kafkaBus = new ProviderParityMessageBus("kafka");
		_rabbitMqBus = new ProviderParityMessageBus("rabbitmq");

		_ = services.AddRemoteMessageBus("aws-sqs", _ => _awsSqsBus);
		_ = services.AddRemoteMessageBus("azure-servicebus", _ => _azureServiceBus);
		_ = services.AddRemoteMessageBus("kafka", _ => _kafkaBus);
		_ = services.AddRemoteMessageBus("rabbitmq", _ => _rabbitMqBus);

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	[Benchmark(Baseline = true, Description = "Dispatch: pre-routed local command")]
	public async Task<IMessageResult> Dispatch_PreRoutedLocalCommand()
	{
		var command = new RoutingFirstCommand { Value = 42 };
		return await DispatchWithRouteAsync(command, LocalRoutingDecision).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed local query")]
	public async Task<IMessageResult<int>> Dispatch_PreRoutedLocalQuery()
	{
		var query = new RoutingFirstQuery { Value = 42 };
		return await DispatchWithRouteAsync<RoutingFirstQuery, int>(query, LocalRoutingDecision).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (AWS SQS)")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AwsSqs()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(evt, AwsSqsRoutingDecision).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Azure Service Bus)")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AzureServiceBus()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(evt, AzureServiceBusRoutingDecision)
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (AWS SQS) throughput profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AwsSqs_Throughput()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AwsSqsRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Throughput;
					context.Items[ProviderParityMessageBus.BenchmarkBatchSizeKey] = 32;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (AWS SQS) retry profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AwsSqs_Retry()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AwsSqsRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Retry;
					context.Items[ProviderParityMessageBus.BenchmarkRetryAttemptsKey] = 3;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (AWS SQS) poison profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AwsSqs_Poison()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AwsSqsRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Poison;
					context.Items[ProviderParityMessageBus.BenchmarkPoisonReasonKey] = "visibility_timeout";
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (AWS SQS) observability profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AwsSqs_Observability()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AwsSqsRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Observability;
					context.Items[ProviderParityMessageBus.BenchmarkObservabilityDepthKey] = 8;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Azure Service Bus) throughput profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AzureServiceBus_Throughput()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AzureServiceBusRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Throughput;
					context.Items[ProviderParityMessageBus.BenchmarkBatchSizeKey] = 32;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Azure Service Bus) retry profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AzureServiceBus_Retry()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AzureServiceBusRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Retry;
					context.Items[ProviderParityMessageBus.BenchmarkRetryAttemptsKey] = 3;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Azure Service Bus) poison profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AzureServiceBus_Poison()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AzureServiceBusRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Poison;
					context.Items[ProviderParityMessageBus.BenchmarkPoisonReasonKey] = "max_delivery_count";
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Azure Service Bus) observability profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_AzureServiceBus_Observability()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				AzureServiceBusRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Observability;
					context.Items[ProviderParityMessageBus.BenchmarkObservabilityDepthKey] = 8;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (Kafka)")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_Kafka()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(evt, KafkaRoutingDecision).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed remote event (RabbitMQ)")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_RabbitMq()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(evt, RabbitMqRoutingDecision).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed Kafka throughput profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_Kafka_Throughput()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				KafkaRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Throughput;
					context.Items[ProviderParityMessageBus.BenchmarkBatchSizeKey] = 32;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed Kafka retry profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_Kafka_Retry()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				KafkaRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Retry;
					context.Items[ProviderParityMessageBus.BenchmarkRetryAttemptsKey] = 3;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed Kafka poison profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_Kafka_Poison()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				KafkaRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Poison;
					context.Items[ProviderParityMessageBus.BenchmarkPoisonReasonKey] = "schema_mismatch";
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed Kafka observability profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_Kafka_Observability()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				KafkaRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Observability;
					context.Items[ProviderParityMessageBus.BenchmarkObservabilityDepthKey] = 8;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed RabbitMQ throughput profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_RabbitMq_Throughput()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				RabbitMqRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Throughput;
					context.Items[ProviderParityMessageBus.BenchmarkBatchSizeKey] = 32;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed RabbitMQ retry profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_RabbitMq_Retry()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				RabbitMqRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Retry;
					context.Items[ProviderParityMessageBus.BenchmarkRetryAttemptsKey] = 3;
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed RabbitMQ poison profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_RabbitMq_Poison()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				RabbitMqRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Poison;
					context.Items[ProviderParityMessageBus.BenchmarkPoisonReasonKey] = "dead_letter_threshold";
				})
			.ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch: pre-routed RabbitMQ observability profile")]
	public async Task<IMessageResult> Dispatch_PreRoutedRemoteEvent_RabbitMq_Observability()
	{
		var evt = new RoutingFirstEvent { Value = 42 };
		return await DispatchWithRouteAsync(
				evt,
				RabbitMqRoutingDecision,
				static context =>
				{
					context.Items[ProviderParityMessageBus.BenchmarkScenarioKey] = ProviderParityScenario.Observability;
					context.Items[ProviderParityMessageBus.BenchmarkObservabilityDepthKey] = 8;
				})
			.ConfigureAwait(false);
	}

	private async Task<IMessageResult> DispatchWithRouteAsync<TMessage>(
		TMessage message,
		RoutingDecision decision,
		Action<IMessageContext>? configureContext = null)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_contextFactory);

		var context = _contextFactory.CreateContext();
		context.RoutingDecision = decision;
		configureContext?.Invoke(context);
		var dispatchTask = _dispatcher.DispatchAsync(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				_contextFactory.Return(context);
			}
		}

		return await AwaitAndReturnContextAsync(dispatchTask, _contextFactory, context).ConfigureAwait(false);
	}

	private async Task<IMessageResult<TResponse>> DispatchWithRouteAsync<TMessage, TResponse>(
		TMessage message,
		RoutingDecision decision)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_contextFactory);

		var context = _contextFactory.CreateContext();
		context.RoutingDecision = decision;
		var dispatchTask = _dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				_contextFactory.Return(context);
			}
		}

		return await AwaitAndReturnTypedContextAsync(dispatchTask, _contextFactory, context).ConfigureAwait(false);
	}

	private static async Task<IMessageResult> AwaitAndReturnContextAsync(
		Task<IMessageResult> dispatchTask,
		IMessageContextFactory contextFactory,
		IMessageContext context)
	{
		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
	}

	private static async Task<IMessageResult<TResponse>> AwaitAndReturnTypedContextAsync<TResponse>(
		Task<IMessageResult<TResponse>> dispatchTask,
		IMessageContextFactory contextFactory,
		IMessageContext context)
	{
		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
	}
}

internal enum ProviderParityScenario
{
	Baseline = 0,
	Throughput = 1,
	Retry = 2,
	Poison = 3,
	Observability = 4,
}

public sealed class RoutingFirstCommand : IDispatchAction
{
	public int Value { get; init; }
}

public sealed class RoutingFirstQuery : IDispatchAction<int>
{
	public int Value { get; init; }
}

public sealed class RoutingFirstEvent : IDispatchEvent
{
	public int Value { get; init; }
}

public sealed class RoutingFirstCommandHandler : IActionHandler<RoutingFirstCommand>
{
	public Task HandleAsync(RoutingFirstCommand message, CancellationToken cancellationToken)
	{
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

public sealed class RoutingFirstQueryHandler : IActionHandler<RoutingFirstQuery, int>
{
	public Task<int> HandleAsync(RoutingFirstQuery message, CancellationToken cancellationToken)
	{
		return Task.FromResult(message.Value * 2);
	}
}

internal sealed class ProviderParityMessageBus(string providerName) : IMessageBus
{
	internal const string BenchmarkScenarioKey = "benchmark.scenario";
	internal const string BenchmarkBatchSizeKey = "benchmark.batchSize";
	internal const string BenchmarkRetryAttemptsKey = "benchmark.retryAttempts";
	internal const string BenchmarkPoisonReasonKey = "benchmark.poisonReason";
	internal const string BenchmarkObservabilityDepthKey = "benchmark.observabilityDepth";

	private static readonly string[] ObservabilityTagKeys =
	[
		"benchmark.obs.traceId",
		"benchmark.obs.spanId",
		"benchmark.obs.route",
		"benchmark.obs.tenant",
		"benchmark.obs.messageType",
		"benchmark.obs.retryCount",
		"benchmark.obs.partition",
		"benchmark.obs.sequence",
	];

	private int _publishedCount;
	private long _syntheticWorkCounter;
	private readonly string _providerMetadataKey = providerName switch
	{
		"aws-sqs" => "benchmark.aws.attributes",
		"azure-servicebus" => "benchmark.asb.applicationProperties",
		"kafka" => "benchmark.kafka.headers",
		_ => "benchmark.rabbitmq.headers",
	};

	public int PublishedCount => _publishedCount;

	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);
		StampProviderMetadata(context);
		ApplyScenarioWorkload(context);
		_ = Interlocked.Increment(ref _publishedCount);
		return Task.CompletedTask;
	}

	public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);
		StampProviderMetadata(context);
		ApplyScenarioWorkload(context);
		_ = Interlocked.Increment(ref _publishedCount);
		return Task.CompletedTask;
	}

	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);
		StampProviderMetadata(context);
		ApplyScenarioWorkload(context);
		_ = Interlocked.Increment(ref _publishedCount);
		return Task.CompletedTask;
	}

	private void ApplyScenarioWorkload(IMessageContext context)
	{
		var scenario = ResolveScenario(context);
		switch (scenario)
		{
			case ProviderParityScenario.Throughput:
			{
				var batchSize = GetInt(context, BenchmarkBatchSizeKey, 16);
				var hashSeed = providerName.GetHashCode(StringComparison.Ordinal);
				var accumulator = _syntheticWorkCounter;
				for (var i = 0; i < batchSize; i++)
				{
					accumulator = unchecked((accumulator * 16777619) ^ (hashSeed + i));
				}

				_syntheticWorkCounter = accumulator;
				break;
			}
			case ProviderParityScenario.Retry:
			{
				var attempts = Math.Max(1, GetInt(context, BenchmarkRetryAttemptsKey, 2));
				var retryAccumulator = 0;
				for (var attempt = 0; attempt < attempts; attempt++)
				{
					retryAccumulator = attempt + 1;
				}

				context.Items["benchmark.retry.currentAttempt"] = retryAccumulator;

				break;
			}
			case ProviderParityScenario.Poison:
			{
				if (!context.Items.ContainsKey(BenchmarkPoisonReasonKey))
				{
					context.Items[BenchmarkPoisonReasonKey] = "unknown";
				}

				break;
			}
			case ProviderParityScenario.Observability:
			{
				var depth = Math.Clamp(GetInt(context, BenchmarkObservabilityDepthKey, 4), 1, ObservabilityTagKeys.Length);
				var checksum = _syntheticWorkCounter;
				for (var i = 0; i < depth; i++)
				{
					checksum = unchecked((checksum * 31) + ObservabilityTagKeys[i].GetHashCode(StringComparison.Ordinal) + i);
				}

				_syntheticWorkCounter = checksum;
				context.Items["benchmark.obs.depth"] = depth;
				break;
			}
			default:
				break;
		}
	}

	private static ProviderParityScenario ResolveScenario(IMessageContext context)
	{
		if (!context.Items.TryGetValue(BenchmarkScenarioKey, out var rawScenario) || rawScenario is null)
		{
			return ProviderParityScenario.Baseline;
		}

		if (rawScenario is ProviderParityScenario scenario)
		{
			return scenario;
		}

		if (rawScenario is string scenarioText &&
			Enum.TryParse<ProviderParityScenario>(scenarioText, ignoreCase: true, out var parsed))
		{
			return parsed;
		}

		return ProviderParityScenario.Baseline;
	}

	private static int GetInt(IMessageContext context, string key, int defaultValue)
	{
		if (!context.Items.TryGetValue(key, out var value) || value is null)
		{
			return defaultValue;
		}

		return value switch
		{
			int intValue => intValue,
			long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
			_ => defaultValue,
		};
	}

	private void StampProviderMetadata(IMessageContext context)
	{
		context.Items[_providerMetadataKey] = 3;
	}
}
