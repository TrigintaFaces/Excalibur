// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport.Kafka;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Kafka transport via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderKafkaExtensions
{
	/// <summary>
	/// Configures the Kafka transport with the default name via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseKafka(kafka =>
	///     {
	///         kafka.BootstrapServers("localhost:9092")
	///              .ConfigureConsumer(consumer => consumer.GroupId("my-group"));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseKafka(
		this IDispatchBuilder builder,
		Action<IKafkaTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddKafkaTransport(configure);
		return builder;
	}

	/// <summary>
	/// Configures a named Kafka transport via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="name">The transport name for multi-transport scenarios.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseKafka("analytics", kafka =>
	///     {
	///         kafka.BootstrapServers("analytics-cluster:9092")
	///              .MapTopic&lt;MetricEvent&gt;("metrics-topic");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseKafka(
		this IDispatchBuilder builder,
		string name,
		Action<IKafkaTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddKafkaTransport(name, configure);
		return builder;
	}
}
