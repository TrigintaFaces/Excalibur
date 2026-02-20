// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of IScheduledMessage with timezone support.
/// </summary>
public sealed class ScheduledMessage : IScheduledMessage
{
	/// <inheritdoc />
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <inheritdoc />
	public string CronExpression { get; set; } = null!;

	/// <inheritdoc />
	public string? TimeZoneId { get; set; }

	/// <inheritdoc />
	public TimeSpan? Interval { get; set; }

	/// <inheritdoc />
	public string MessageName { get; set; } = null!;

	/// <inheritdoc />
	public string MessageBody { get; set; } = null!;

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? TraceParent { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; set; }

	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? NextExecutionUtc { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? LastExecutionUtc { get; set; }

	/// <inheritdoc />
	public bool Enabled { get; set; } = true;

	/// <inheritdoc />
	public MissedExecutionBehavior? MissedExecutionBehavior { get; set; }
}
