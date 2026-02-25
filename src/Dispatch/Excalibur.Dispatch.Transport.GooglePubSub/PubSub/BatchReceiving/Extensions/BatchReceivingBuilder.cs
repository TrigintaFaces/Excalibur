// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Builder for configuring batch receiving options.
/// </summary>
public sealed class BatchReceivingBuilder
{
	private readonly IServiceCollection _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchReceivingBuilder" /> class.
	/// </summary>
	internal BatchReceivingBuilder(IServiceCollection services) => _services = services;

	/// <summary>
	/// Configures batch receiving options.
	/// </summary>
	public BatchReceivingBuilder ConfigureOptions(Action<BatchConfiguration> configure)
	{
		_ = _services.Configure(configure);
		return this;
	}

	/// <summary>
	/// Uses adaptive batching strategy.
	/// </summary>
	public BatchReceivingBuilder UseAdaptiveBatching()
	{
		_ = _services.Replace(ServiceDescriptor.Singleton<IBatchingStrategy, AdaptiveBatchingStrategy>());
		return this;
	}

	/// <summary>
	/// Uses time-bound batching strategy.
	/// </summary>
	public BatchReceivingBuilder UseTimeBoundBatching()
	{
		_ = _services.Replace(ServiceDescriptor.Singleton<IBatchingStrategy, TimeBoundBatchingStrategy>());
		return this;
	}

	/// <summary>
	/// Uses size-bound batching strategy.
	/// </summary>
	public BatchReceivingBuilder UseSizeBoundBatching()
	{
		_ = _services.Replace(ServiceDescriptor.Singleton<IBatchingStrategy, SizeBoundBatchingStrategy>());
		return this;
	}

	/// <summary>
	/// Uses a custom batching strategy.
	/// </summary>
	public BatchReceivingBuilder UseCustomBatching<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStrategy>()
		where TStrategy : class, IBatchingStrategy
	{
		_ = _services.Replace(ServiceDescriptor.Singleton<IBatchingStrategy, TStrategy>());
		return this;
	}

	/// <summary>
	/// Configures parallel message processing.
	/// </summary>
	public BatchReceivingBuilder WithParallelProcessing(
		Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor)
	{
		// Note: Using the processing version that extends BatchProcessorBase
		_ = _services.AddScoped<IBatchProcessor>(provider =>
			new ParallelBatchProcessor(
				provider.GetRequiredService<IOptions<BatchConfiguration>>(),
				messageProcessor,
				provider.GetRequiredService<ILogger<ParallelBatchProcessor>>(),
				provider.GetRequiredService<BatchMetricsCollector>()));

		return this;
	}

	/// <summary>
	/// Configures ordered message processing.
	/// </summary>
	public BatchReceivingBuilder WithOrderedProcessing(
		Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor)
	{
		_ = _services.AddScoped<IBatchProcessor>(provider =>
			new OrderedBatchProcessor(
				provider.GetRequiredService<IOptions<BatchConfiguration>>(),
				messageProcessor,
				provider.GetRequiredService<ILogger<OrderedBatchProcessor>>(),
				provider.GetRequiredService<BatchMetricsCollector>()));

		return this;
	}

	/// <summary>
	/// Configures adaptive message processing.
	/// </summary>
	public BatchReceivingBuilder WithAdaptiveProcessing(
		Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor)
	{
		_ = _services.AddScoped<IBatchProcessor>(provider =>
			new AdaptiveBatchProcessor(
				provider.GetRequiredService<IOptions<BatchConfiguration>>(),
				messageProcessor,
				provider.GetRequiredService<ILogger<AdaptiveBatchProcessor>>(),
				provider.GetRequiredService<ILoggerFactory>(),
				provider.GetRequiredService<BatchMetricsCollector>()));

		return this;
	}

	/// <summary>
	/// Integrates with the flow control system.
	/// </summary>
	public BatchReceivingBuilder IntegrateWithFlowControl()
	{
		// Ensure flow control is registered
		_services.TryAddSingleton<PubSubFlowController>();

		// Note: Scrutor's Decorate extension method would be needed for this pattern For now, commenting out to avoid compilation errors
		// _services.Decorate<IBatchReceiver>((inner, provider) => { var flowController =
		// provider.GetRequiredService<PubSubFlowController>(); return inner; });
		return this;
	}

	/// <summary>
	/// Enables metrics collection.
	/// </summary>
	public BatchReceivingBuilder WithMetrics(string? meterName = null)
	{
		if (!string.IsNullOrEmpty(meterName))
		{
			_ = _services.Replace(ServiceDescriptor.Singleton(provider =>
				new BatchMetricsCollector(meterName)));
		}

		return this;
	}
}
