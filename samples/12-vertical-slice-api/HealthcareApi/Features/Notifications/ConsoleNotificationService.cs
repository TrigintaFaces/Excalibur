// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Notifications;

/// <summary>
/// Console-based notification for the sample. Replace with email/SMS in production.
/// </summary>
public sealed class ConsoleNotificationService : INotificationService
{
	public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[NOTIFICATION] To: {recipient} | Subject: {subject} | {body}");
		return Task.CompletedTask;
	}
}
