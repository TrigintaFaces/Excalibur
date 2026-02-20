// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ message publishers.
/// </summary>
public sealed class RabbitMqPublisherOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable publisher confirms.
	/// </summary>
	/// <remarks>
	/// When enabled, the publisher will wait for broker confirmation before
	/// returning from publish operations. This provides delivery guarantees
	/// but may impact throughput.
	/// </remarks>
	/// <value><see langword="true"/> to enable publisher confirms; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool EnableConfirms { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for waiting for publish confirmations.
	/// </summary>
	/// <remarks>
	/// If a confirmation is not received within this timeout, a
	/// <see cref="TimeoutException"/> will be thrown.
	/// </remarks>
	/// <value>The confirmation timeout. Default is 5 seconds.</value>
	public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable mandatory publishing.
	/// </summary>
	/// <remarks>
	/// When enabled, unroutable messages will be returned to the publisher
	/// instead of being silently discarded.
	/// </remarks>
	/// <value><see langword="true"/> to enable mandatory publishing; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool MandatoryPublishing { get; set; } = true;

	/// <summary>
	/// Gets or sets the message persistence level.
	/// </summary>
	/// <value>The persistence level. Default is <see cref="RabbitMqPersistence.Persistent"/>.</value>
	public RabbitMqPersistence Persistence { get; set; } = RabbitMqPersistence.Persistent;

	/// <summary>
	/// Gets or sets the default message time-to-live.
	/// </summary>
	/// <remarks>
	/// Messages will expire and be removed from queues after this duration.
	/// Set to <c>null</c> for no expiration.
	/// </remarks>
	/// <value>The message TTL. Default is 7 days.</value>
	public TimeSpan? MessageTtl { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the maximum message size in bytes.
	/// </summary>
	/// <remarks>
	/// Messages exceeding this size will be rejected before publishing.
	/// </remarks>
	/// <value>The maximum message size. Default is 128 MB.</value>
	public long MaxMessageSizeBytes { get; set; } = 128 * 1024 * 1024;

	/// <summary>
	/// Gets or sets a value indicating whether to batch multiple messages
	/// into a single channel operation for improved throughput.
	/// </summary>
	/// <value><see langword="true"/> to enable batch publishing; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool EnableBatchPublishing { get; set; }

	/// <summary>
	/// Gets or sets the maximum batch size when batch publishing is enabled.
	/// </summary>
	/// <value>The maximum batch size. Default is 100.</value>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum time to wait for a batch to fill before sending.
	/// </summary>
	/// <value>The batch flush interval. Default is 50 milliseconds.</value>
	public TimeSpan BatchFlushInterval { get; set; } = TimeSpan.FromMilliseconds(50);
}
