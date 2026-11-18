// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

/// <summary>Accepts one notification for delivery; completion persistence belongs to the outbox drainer.</summary>
public interface IEmailSender
{
	/// <summary>Sends the notification, observing caller cancellation before acceptance.</summary>
	Task SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken);
}
