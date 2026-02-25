// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents an event that has occurred within the system and can be dispatched to multiple handlers.
/// </summary>
/// <remarks>
/// Events are immutable facts that describe something that has happened in the past. They are typically handled by multiple subscribers and
/// should not contain behavior. Events support eventual consistency patterns and are commonly used for:
/// <list type="bullet">
/// <item> Domain event sourcing </item>
/// <item> Integration between bounded contexts </item>
/// <item> Audit logging and activity tracking </item>
/// <item> Cache invalidation and projections </item>
/// </list>
/// </remarks>
public interface IDispatchEvent : IDispatchMessage;
