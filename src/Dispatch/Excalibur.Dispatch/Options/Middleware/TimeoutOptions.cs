// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for timeout middleware.
/// </summary>
public sealed class TimeoutOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether timeout middleware is enabled. Default is true.
	/// </summary>
	/// <value> The current <see cref="Enabled" /> value. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the default timeout for all message processing. Default is 30 seconds.
	/// </summary>
	/// <value> The default timeout for all message processing. Default is 30 seconds. </value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout for Action messages. Default is 30 seconds.
	/// </summary>
	/// <value> The timeout for Action messages. Default is 30 seconds. </value>
	public TimeSpan ActionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout for Event messages. Default is 10 seconds.
	/// </summary>
	/// <value> The timeout for Event messages. Default is 10 seconds. </value>
	public TimeSpan EventTimeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the timeout for Document messages. Default is 60 seconds.
	/// </summary>
	/// <value> The timeout for Document messages. Default is 60 seconds. </value>
	public TimeSpan DocumentTimeout { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets message type-specific timeouts. Key is the message type name, value is the timeout duration.
	/// </summary>
	/// <value> The current <see cref="MessageTypeTimeouts" /> value. </value>
	public Dictionary<string, TimeSpan> MessageTypeTimeouts { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to throw an exception on timeout. If false, returns a timeout result instead. Default is true.
	/// </summary>
	/// <value> The current <see cref="ThrowOnTimeout" /> value. </value>
	public bool ThrowOnTimeout { get; set; } = true;

	/// <summary>
	/// Validates the timeout configuration.
	/// </summary>
	/// <exception cref="ArgumentException"> Thrown when configuration is invalid. </exception>
	public void Validate()
	{
		if (DefaultTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(
				Resources.TimeoutOptions_DefaultTimeoutMustBePositive,
				nameof(DefaultTimeout));
		}

		if (ActionTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(
				Resources.TimeoutOptions_ActionTimeoutMustBePositive,
				nameof(ActionTimeout));
		}

		if (EventTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(
				Resources.TimeoutOptions_EventTimeoutMustBePositive,
				nameof(EventTimeout));
		}

		if (DocumentTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(
				Resources.TimeoutOptions_DocumentTimeoutMustBePositive,
				nameof(DocumentTimeout));
		}

		foreach (var kvp in MessageTypeTimeouts)
		{
			if (kvp.Value <= TimeSpan.Zero)
			{
				throw new ArgumentException(
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.TimeoutOptions_MessageTypeTimeoutMustBePositive,
						kvp.Key),
					nameof(MessageTypeTimeouts));
			}
		}
	}
}
