// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for the Azure Service Bus request/reply pattern.
/// </summary>
/// <remarks>
/// <para>
/// Configures the request/reply client behavior including the reply queue,
/// timeout settings, and session management options.
/// </para>
/// </remarks>
public sealed class RequestReplyOptions
{
	/// <summary>
	/// Gets or sets the reply queue name where responses are received.
	/// </summary>
	/// <remarks>
	/// This queue must be session-enabled in Azure Service Bus to support
	/// correlation via session IDs.
	/// </remarks>
	/// <value>The reply queue name.</value>
	[Required]
	public string ReplyQueueName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the default timeout for waiting for a reply.
	/// </summary>
	/// <remarks>
	/// If no reply is received within this duration, a <see cref="TimeoutException"/> is thrown.
	/// </remarks>
	/// <value>The reply timeout. Default is 30 seconds.</value>
	public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the default time-to-live for request messages.
	/// </summary>
	/// <remarks>
	/// Request messages will expire if not processed within this duration.
	/// Set to <c>null</c> to use the queue/topic default TTL.
	/// </remarks>
	/// <value>The request message TTL. Default is 60 seconds.</value>
	public TimeSpan? RequestTimeToLive { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the maximum number of concurrent outstanding requests.
	/// </summary>
	/// <remarks>
	/// Limits the number of request/reply operations that can be in flight simultaneously.
	/// This helps prevent resource exhaustion when sending many concurrent requests.
	/// </remarks>
	/// <value>The maximum concurrent requests. Default is 100.</value>
	[Range(1, 10000)]
	public int MaxConcurrentRequests { get; set; } = 100;
}
