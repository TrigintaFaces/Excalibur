// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using HealthcareApi.Features.Prescriptions.Shared;

namespace Microsoft.Extensions.DependencyInjection;

public static class PrescriptionsServiceCollectionExtensions
{
	public static IServiceCollection AddPrescriptionsFeature(this IServiceCollection services)
	{
		services.AddSingleton<IPrescriptionRepository, InMemoryPrescriptionRepository>();
		return services;
	}
}
