// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>A detached scheduled message paired with its opaque administrative generation.</summary>
public sealed class ScheduleExecutionSnapshot
{
	/// <summary>Initializes a snapshot with an owned copy of the message.</summary>
	/// <param name="generation">Nonempty administrative generation.</param>
	/// <param name="message">Message to copy.</param>
	public ScheduleExecutionSnapshot(Guid generation, IScheduledMessage message)
	{
		if (generation == Guid.Empty)
		{
			throw new ArgumentException("A generation is required.", nameof(generation));
		}
		ArgumentNullException.ThrowIfNull(message);
		Generation = generation;
		Message = Copy(message);
	}
	/// <summary>Gets the opaque administrative generation.</summary>
	public Guid Generation { get; }
	/// <summary>Gets the detached mutable message; its mutation cannot change stored state.</summary>
	public IScheduledMessage Message { get; }

	internal static ScheduledMessage Copy(IScheduledMessage message) => new()
	{
		Id = message.Id,
		MessageName = message.MessageName,
		MessageBody = message.MessageBody,
		CorrelationId = message.CorrelationId,
		CronExpression = message.CronExpression,
		TimeZoneId = message.TimeZoneId,
		Interval = message.Interval,
		NextExecutionUtc = message.NextExecutionUtc,
		Enabled = message.Enabled,
		LastExecutionUtc = message.LastExecutionUtc,
		MissedExecutionBehavior = message.MissedExecutionBehavior,
		TenantId = message.TenantId,
		TraceParent = message.TraceParent,
		UserId = message.UserId,
	};
}
