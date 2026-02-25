// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Exception thrown when message processing times out.
/// </summary>
public sealed class MessageTimeoutException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessageTimeoutException" /> class.
	/// </summary>
	/// <param name="message"> The exception message. </param>
	public MessageTimeoutException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageTimeoutException" /> class.
	/// </summary>
	/// <param name="message"> The exception message. </param>
	/// <param name="innerException"> The inner exception. </param>
	public MessageTimeoutException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageTimeoutException" /> class.
	/// </summary>
	public MessageTimeoutException() : base()
	{
	}

	/// <summary>
	/// Gets or sets the ID of the message that timed out.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the type name of the message that timed out.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the configured timeout duration.
	/// </summary>
	/// <value>The current <see cref="TimeoutDuration"/> value.</value>
	public TimeSpan TimeoutDuration { get; set; }

	/// <summary>
	/// Gets or sets the actual elapsed time before timeout.
	/// </summary>
	/// <value>The current <see cref="ElapsedTime"/> value.</value>
	public TimeSpan ElapsedTime { get; set; }
}
