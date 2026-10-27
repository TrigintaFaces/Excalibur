// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using HealthcareApi.Features.Notifications;

namespace Microsoft.Extensions.DependencyInjection;

public static class NotificationsServiceCollectionExtensions
{
	public static IServiceCollection AddNotificationsFeature(this IServiceCollection services)
	{
		services.AddSingleton<INotificationService, ConsoleNotificationService>();
		return services;
	}
}
