// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marker interface for events that are intended for integration between bounded contexts or external systems.
/// </summary>
/// <remarks>
/// Integration events are published across service boundaries and are typically used for:
/// <list type="bullet">
/// <item> Cross-service communication </item>
/// <item> External system integration </item>
/// <item> Domain event publishing to message brokers </item>
/// <item> Event-driven architecture patterns </item>
/// </list>
/// </remarks>
public interface IIntegrationEvent : IDispatchEvent
{
}
