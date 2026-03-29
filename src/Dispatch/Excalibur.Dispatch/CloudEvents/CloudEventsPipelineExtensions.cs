// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Extension methods for adding CloudEvents middleware to the dispatch pipeline.
/// </summary>
public static class CloudEventsPipelineExtensions
{
	/// <summary>
	/// Adds CloudEvents middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The CloudEvents middleware enriches messages with CloudEvents metadata
	/// (source, type, subject, etc.) following the CloudEvents specification.
	/// </para>
	/// <para>
	/// CloudEvents options must be configured separately via <c>IOptions&lt;CloudEventOptions&gt;</c>.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseCloudEvents()         // Enrich early so downstream middleware sees CE metadata
	///        .UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseOutbox();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseCloudEvents(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<CloudEventMiddleware>();
	}

	/// <summary>
	/// Adds custom CloudEvent validation to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="validator">
	/// A function that validates a <see cref="CloudEvent"/> and returns <see langword="true"/>
	/// if the event is valid, or <see langword="false"/> to reject it.
	/// </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This method registers a custom CloudEvent validator via
	/// <see cref="CloudEventsServiceCollectionExtensions.UseCloudEventValidation"/>.
	/// The validator is invoked for each incoming CloudEvent before handler execution.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseCloudEventValidation(
		this IDispatchBuilder builder,
		Func<CloudEvent, CancellationToken, Task<bool>> validator)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(validator);

		// Call the implementation directly
		_ = builder.Services.Configure<CloudEventOptions>(options => options.Schema.CustomValidator = validator);

		return builder;
	}

	/// <summary>
	/// Adds CloudEvent batching support to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureBatch"> Optional action to configure batch options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This method registers CloudEvent batching services via
	/// <see cref="CloudEventsServiceCollectionExtensions.UseCloudEventBatching"/>.
	/// Batching groups multiple CloudEvents together for efficient processing.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseCloudEventBatching(
		this IDispatchBuilder builder,
		Action<CloudEventBatchOptions>? configureBatch = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Inline batch registration directly rather than delegating to service collection extension
		var batchOptions = new CloudEventBatchOptions();
		configureBatch?.Invoke(batchOptions);
		builder.Services.TryAddSingleton(batchOptions);
		builder.Services.TryAddTransient<ICloudEventBatchProcessor, DefaultCloudEventBatchProcessor>();

		return builder;
	}

	/// <summary>
	/// Adds custom CloudEvent transformation for outgoing events to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="transformer">
	/// A function that transforms a <see cref="CloudEvent"/> before it is sent.
	/// Multiple transformers are chained in registration order.
	/// </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This method registers a custom CloudEvent transformer via
	/// <see cref="CloudEventsServiceCollectionExtensions.UseCloudEventTransformation"/>.
	/// Transformers are applied to outgoing CloudEvents in the order they were registered.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseCloudEventTransformation(
		this IDispatchBuilder builder,
		Func<CloudEvent, IDispatchEvent, IMessageContext, CancellationToken, Task> transformer)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(transformer);

		// Inline transformation registration directly rather than delegating to service collection extension
		_ = builder.Services.Configure<CloudEventOptions>(options =>
		{
			var existingTransformer = options.Schema.OutgoingTransformer;
			options.Schema.OutgoingTransformer = async (ce, evt, ctx, ct) =>
			{
				if (existingTransformer != null)
				{
					await existingTransformer(ce, evt, ctx, ct).ConfigureAwait(false);
				}

				await transformer(ce, evt, ctx, ct).ConfigureAwait(false);
			};
		});

		return builder;
	}
}
