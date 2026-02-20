// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messages;

/// <summary>
/// Represents timer trigger information.
/// </summary>
public sealed class TimerInfo : IDispatchMessage
{
	private readonly Dictionary<string, object> _headers = [];
	private readonly DefaultMessageFeatures _features = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="TimerInfo" /> class.
	/// </summary>
	public TimerInfo()
	{
		MessageId = Guid.NewGuid().ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new ReadOnlyDictionary<string, object>(_headers);
		Features = _features;
		MessageType = GetType().Name;
	}

	/// <inheritdoc />
	public string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	/// <value>
	/// The correlation ID.
	/// </value>
	public string? CorrelationId
	{
		get => _headers.TryGetValue("CorrelationId", out var value) ? value?.ToString() : null;
		set
		{
			if (value != null)
			{
				_headers["CorrelationId"] = value;
			}
			else
			{
				_ = _headers.Remove("CorrelationId");
			}
		}
	}

	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the timer name.
	/// </summary>
	/// <value>The current <see cref="TimerName"/> value.</value>
	public string TimerName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cron expression.
	/// </summary>
	/// <value>The current <see cref="CronExpression"/> value.</value>
	public string CronExpression { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the scheduled time for this trigger.
	/// </summary>
	/// <value>The current <see cref="ScheduledTime"/> value.</value>
	public DateTimeOffset ScheduledTime { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this trigger is past due.
	/// </summary>
	/// <value>The current <see cref="IsPastDue"/> value.</value>
	public bool IsPastDue { get; set; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; }

	/// <inheritdoc />
	public object Body => this;

	/// <inheritdoc />
	public string MessageType { get; }

	/// <inheritdoc />
	public IMessageFeatures Features { get; }

	/// <inheritdoc />
	public Guid Id => Guid.TryParse(MessageId, out var guid) ? guid : Guid.Empty;

	/// <inheritdoc />
	public MessageKinds Kind => MessageKinds.Event;
}
