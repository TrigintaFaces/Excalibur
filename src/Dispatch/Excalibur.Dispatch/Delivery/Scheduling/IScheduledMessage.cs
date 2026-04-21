// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a persistable scheduled message with comprehensive metadata for timezone-aware execution and tracking.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes three focused sub-interfaces for consumers that need only a subset of properties:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IScheduledMessageIdentity"/> — Message identity: ID, type name, serialized body, and correlation.</description></item>
/// <item><description><see cref="IScheduleSpec"/> — Schedule definition: cron, timezone, interval, next execution, and enabled state.</description></item>
/// <item><description><see cref="IScheduledMessageMetadata"/> — Execution tracking and operational metadata: last execution, missed behavior, tenant, tracing, and audit.</description></item>
/// </list>
/// </remarks>
public interface IScheduledMessage
	: IScheduledMessageIdentity,
	  IScheduleSpec,
	  IScheduledMessageMetadata
{
}
