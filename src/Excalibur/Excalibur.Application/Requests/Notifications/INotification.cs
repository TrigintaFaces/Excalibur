// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Notifications;

/// <summary>
/// Represents a notification in the system, combining the properties of <see cref="IActivity" /> and IIntegrationEvent notifications.
/// </summary>
public interface INotification : IActivity, IIntegrationEvent
{
}
