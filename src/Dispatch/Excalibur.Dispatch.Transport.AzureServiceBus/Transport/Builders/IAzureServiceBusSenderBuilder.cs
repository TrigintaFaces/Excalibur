// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Fluent builder interface for configuring Azure Service Bus sender (producer) settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sb.ConfigureSender(sender =>
/// {
///     sender.DefaultEntity("orders-queue")
///           .EnableBatching(true)
///           .MaxBatchSizeBytes(256 * 1024)
///           .MaxBatchCount(100)
///           .BatchWindow(TimeSpan.FromMilliseconds(100));
/// });
/// </code>
/// </example>
public interface IAzureServiceBusSenderBuilder
{
	/// <summary>
	/// Sets the default queue or topic name for sending messages.
	/// </summary>
	/// <param name="entityName">The default entity name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="entityName"/> is null, empty, or whitespace.
	/// </exception>
	IAzureServiceBusSenderBuilder DefaultEntity(string entityName);

	/// <summary>
	/// Enables or disables message batching.
	/// </summary>
	/// <param name="enable">Whether to enable batching. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IAzureServiceBusSenderBuilder EnableBatching(bool enable = true);

	/// <summary>
	/// Sets the maximum batch size in bytes.
	/// </summary>
	/// <param name="maxSizeBytes">The maximum size in bytes. Default is 256KB.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxSizeBytes"/> is less than or equal to zero.
	/// </exception>
	IAzureServiceBusSenderBuilder MaxBatchSizeBytes(long maxSizeBytes);

	/// <summary>
	/// Sets the maximum number of messages in a batch.
	/// </summary>
	/// <param name="maxCount">The maximum message count per batch. Default is 100.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxCount"/> is less than or equal to zero.
	/// </exception>
	IAzureServiceBusSenderBuilder MaxBatchCount(int maxCount);

	/// <summary>
	/// Sets the batch window duration.
	/// </summary>
	/// <param name="window">The time to wait before sending a partial batch.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="window"/> is negative.
	/// </exception>
	IAzureServiceBusSenderBuilder BatchWindow(TimeSpan window);

	/// <summary>
	/// Adds a custom configuration setting.
	/// </summary>
	/// <param name="key">The configuration key.</param>
	/// <param name="value">The configuration value.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="key"/> is null, empty, or whitespace.
	/// </exception>
	IAzureServiceBusSenderBuilder WithConfig(string key, string value);
}

/// <summary>
/// Internal implementation of the Azure Service Bus sender builder.
/// </summary>
internal sealed class AzureServiceBusSenderBuilder : IAzureServiceBusSenderBuilder
{
	private readonly AzureServiceBusSenderOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusSenderBuilder"/> class.
	/// </summary>
	/// <param name="options">The sender options to configure.</param>
	public AzureServiceBusSenderBuilder(AzureServiceBusSenderOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder DefaultEntity(string entityName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
		_options.DefaultEntityName = entityName;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder EnableBatching(bool enable = true)
	{
		_options.EnableBatching = enable;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder MaxBatchSizeBytes(long maxSizeBytes)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSizeBytes);
		_options.MaxBatchSizeBytes = maxSizeBytes;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder MaxBatchCount(int maxCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
		_options.MaxBatchCount = maxCount;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder BatchWindow(TimeSpan window)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(window.TotalMilliseconds, nameof(window));
		_options.BatchWindow = window;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusSenderBuilder WithConfig(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.AdditionalConfig[key] = value;
		return this;
	}
}
