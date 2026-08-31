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
}
