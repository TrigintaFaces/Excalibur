// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using HealthcareApi.Features.Appointments.Shared;

namespace Microsoft.Extensions.DependencyInjection;

public static class AppointmentsServiceCollectionExtensions
{
	public static IServiceCollection AddAppointmentsFeature(this IServiceCollection services)
	{
		services.AddSingleton<IAppointmentRepository, InMemoryAppointmentRepository>();
		return services;
	}
}
