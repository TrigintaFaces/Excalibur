// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using HealthcareApi.Features.Patients.Shared;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the Patients feature slice.
/// Following Microsoft conventions: extension on IServiceCollection in the
/// Microsoft.Extensions.DependencyInjection namespace for discoverability.
/// </summary>
public static class PatientsServiceCollectionExtensions
{
	public static IServiceCollection AddPatientsFeature(this IServiceCollection services)
	{
		services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
		return services;
	}
}
