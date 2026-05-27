// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Compliance;

namespace GdprCompliance.Domain.Events;

/// <summary>
/// Published after <see cref="Commands.EraseCustomerHandler"/> clears every
/// <see cref="PersonalDataAttribute"/> field on a customer.
/// </summary>
public sealed record CustomerErasedEvent(
	Guid CustomerId,
	Guid RequestId,
	ErasureRequestStatus Status,
	DateTimeOffset? ScheduledExecutionTime,
	string Pattern) : DomainEvent;

/// <summary>
/// Published after <see cref="Commands.TombstoneCustomerHandler"/> replaces a
/// customer row with a tombstone marker record.
/// </summary>
public sealed record CustomerTombstonedEvent(
	Guid CustomerId,
	Guid RequestId,
	ErasureRequestStatus Status,
	DateTimeOffset? ScheduledExecutionTime) : DomainEvent;
