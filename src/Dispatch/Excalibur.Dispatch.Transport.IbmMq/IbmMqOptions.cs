// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Configuration options for the IBM MQ transport.
/// </summary>
/// <remarks>
/// Mirrors the native connection surface of the IBM MQ managed .NET client (queue manager, host/port,
/// server-connection channel, and queue names). IBM MQ is a queue-based broker with native request/reply,
/// so a reply-to queue can be configured for the request/reply pattern.
/// </remarks>
public sealed class IbmMqOptions
{
	/// <summary>Gets or sets the name of the target queue manager.</summary>
	/// <value>The queue manager name; required.</value>
	[Required]
	public string QueueManager { get; set; } = string.Empty;

	/// <summary>Gets or sets the host name of the queue manager listener.</summary>
	/// <value>The host name; required.</value>
	[Required]
	public string Host { get; set; } = string.Empty;

	/// <summary>Gets or sets the TCP port of the queue manager listener.</summary>
	/// <value>The listener port; must be in 1..65535. Defaults to 1414 (the IBM MQ default).</value>
	public int Port { get; set; } = 1414;

	/// <summary>Gets or sets the server-connection channel name.</summary>
	/// <value>The channel name; required. Commonly <c>DEV.APP.SVRCONN</c> on developer queue managers.</value>
	[Required]
	public string Channel { get; set; } = string.Empty;

	/// <summary>Gets or sets the queue that messages are sent to and received from.</summary>
	/// <value>The queue name; required.</value>
	[Required]
	public string QueueName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the reply-to queue used for the native request/reply pattern, or <see langword="null"/>
	/// when request/reply is not used.
	/// </summary>
	/// <value>The reply-to queue name, or <see langword="null"/>.</value>
	public string? ReplyToQueue { get; set; }

	/// <summary>Gets or sets the user id for client authentication, or <see langword="null"/> for none.</summary>
	/// <value>The user id, or <see langword="null"/>. Do not hard-code credentials; source them from configuration
	/// or a secret manager.</value>
	public string? UserId { get; set; }

	/// <summary>Gets or sets the password for client authentication, or <see langword="null"/> for none.</summary>
	/// <value>The password, or <see langword="null"/>. Source from a secret manager; never commit a value.</value>
	public string? Password { get; set; }

	/// <summary>Gets or sets the receive tuning options.</summary>
	/// <value>The receive tuning options.</value>
	public IbmMqReceiveTuningOptions Receive { get; set; } = new();
}

/// <summary>
/// Receive-side tuning options for the IBM MQ transport.
/// </summary>
public sealed class IbmMqReceiveTuningOptions
{
	/// <summary>
	/// The upper bound on <see cref="MaxBatchSize"/>. Because the receiver opens one queue-manager connection
	/// per in-flight message, this caps the concurrent connections a single receive can open.
	/// </summary>
	public const int MaxBatchSizeCeiling = 256;

	/// <summary>Gets or sets the maximum number of messages to drain per receive call.</summary>
	/// <value>The maximum receive batch size; must be in 1..<see cref="MaxBatchSizeCeiling"/>. Defaults to 10.</value>
	public int MaxBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of <em>cumulative</em> outstanding (received-but-not-yet
	/// acknowledged-or-rejected) units of work across all receive calls. Because each outstanding message
	/// holds its own queue-manager connection under syncpoint, this is the hard bound on the total
	/// connections the receiver can hold open — <see cref="MaxBatchSize"/> only caps a single call, so
	/// under slow acknowledgement the outstanding set (and its connections) would otherwise grow unbounded
	/// and exhaust the queue manager's connection pool. When the cap is reached, a receive call returns
	/// fewer (or zero) messages until the caller acknowledges or rejects enough to free capacity
	/// (back-pressure).
	/// </summary>
	/// <value>The maximum cumulative outstanding units of work; must be at least 1. Defaults to <see cref="MaxBatchSizeCeiling"/>.</value>
	[Range(1, int.MaxValue)]
	public int MaxOutstandingUnitsOfWork { get; set; } = MaxBatchSizeCeiling;

	/// <summary>Gets or sets the get-wait interval, in milliseconds, before a receive call returns empty.</summary>
	/// <value>The wait interval in milliseconds; must be non-negative. Defaults to 1000.</value>
	public int WaitIntervalMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum accepted payload size in bytes, or <see langword="null"/> to opt out of the limit.
	/// </summary>
	/// <value>
	/// The maximum payload size in bytes, or <see langword="null"/> for no limit. Defaults to
	/// <see cref="PayloadSizeGuard.DefaultMaxPayloadBytes"/> (4 MiB), matching the other transports so the
	/// ingress size guard is on by default rather than requiring opt-in.
	/// </value>
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;
}
