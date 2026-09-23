// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Handles a notification of type <typeparamref name="TNotification"/>. Provides the
/// <c>INotificationHandler&lt;TNotification&gt;</c> shape used by MediatR-based code; maps to the
/// canonical <c>IEventHandler</c>.
/// </summary>
/// <typeparam name="TNotification">The notification type handled.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when handling finishes.</returns>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
