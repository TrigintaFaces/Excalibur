// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Fluent builder interface for configuring Azure Service Bus processor (consumer) settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sb.ConfigureProcessor(processor =>
/// {
///     processor.DefaultEntity("orders-queue")
///              .MaxConcurrentCalls(20)
///              .PrefetchCount(100)
///              .AutoCompleteMessages(false)
///              .MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(5));
/// });
/// </code>
/// </example>
public interface IAzureServiceBusProcessorBuilder
{
	/// <summary>
	/// Sets the default queue or subscription name for receiving messages.
	/// </summary>
	/// <param name="entityName">The default entity name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="entityName"/> is null, empty, or whitespace.
	/// </exception>
	IAzureServiceBusProcessorBuilder DefaultEntity(string entityName);

	/// <summary>
	/// Sets the maximum number of concurrent calls to the message handler.
	/// </summary>
	/// <param name="maxConcurrent">The maximum concurrent calls. Default is 10.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxConcurrent"/> is less than or equal to zero.
	/// </exception>
	IAzureServiceBusProcessorBuilder MaxConcurrentCalls(int maxConcurrent);

	/// <summary>
	/// Enables or disables automatic message completion.
	/// </summary>
	/// <param name="autoComplete">Whether to auto-complete messages. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IAzureServiceBusProcessorBuilder AutoCompleteMessages(bool autoComplete = true);

	/// <summary>
	/// Sets the prefetch count for improved performance.
	/// </summary>
	/// <param name="prefetchCount">The number of messages to prefetch. Default is 50.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="prefetchCount"/> is negative.
	/// </exception>
	IAzureServiceBusProcessorBuilder PrefetchCount(int prefetchCount);

	/// <summary>
	/// Sets the maximum duration for automatic lock renewal.
	/// </summary>
	/// <param name="duration">The maximum lock renewal duration.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="duration"/> is negative.
	/// </exception>
	IAzureServiceBusProcessorBuilder MaxAutoLockRenewalDuration(TimeSpan duration);

	/// <summary>
	/// Sets the receive mode.
	/// </summary>
	/// <param name="receiveMode">The receive mode. Default is PeekLock.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IAzureServiceBusProcessorBuilder ReceiveMode(ServiceBusReceiveMode receiveMode);

	/// <summary>
	/// Adds a custom configuration setting.
	/// </summary>
	/// <param name="key">The configuration key.</param>
	/// <param name="value">The configuration value.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="key"/> is null, empty, or whitespace.
	/// </exception>
	IAzureServiceBusProcessorBuilder WithConfig(string key, string value);
}

/// <summary>
/// Internal implementation of the Azure Service Bus processor builder.
/// </summary>
internal sealed class AzureServiceBusProcessorBuilder : IAzureServiceBusProcessorBuilder
{
	private readonly AzureServiceBusProcessorOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusProcessorBuilder"/> class.
	/// </summary>
	/// <param name="options">The processor options to configure.</param>
	public AzureServiceBusProcessorBuilder(AzureServiceBusProcessorOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder DefaultEntity(string entityName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
		_options.DefaultEntityName = entityName;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder MaxConcurrentCalls(int maxConcurrent)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrent);
		_options.MaxConcurrentCalls = maxConcurrent;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder AutoCompleteMessages(bool autoComplete = true)
	{
		_options.AutoCompleteMessages = autoComplete;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder PrefetchCount(int prefetchCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(prefetchCount);
		_options.PrefetchCount = prefetchCount;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder MaxAutoLockRenewalDuration(TimeSpan duration)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(duration.TotalMilliseconds, nameof(duration));
		_options.MaxAutoLockRenewalDuration = duration;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder ReceiveMode(ServiceBusReceiveMode receiveMode)
	{
		_options.ReceiveMode = receiveMode;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusProcessorBuilder WithConfig(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.AdditionalConfig[key] = value;
		return this;
	}
}
