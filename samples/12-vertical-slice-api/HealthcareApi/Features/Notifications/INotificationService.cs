// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Notifications;

/// <summary>
/// Notification service abstraction. In a real app this would send emails, SMS, push, etc.
/// </summary>
public interface INotificationService
{
	Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}
