// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.AspNetCore.Mvc;

namespace HealthcareApi.Features.Appointments.GetAppointment;

public sealed class GetAppointmentRequest
{
	[FromRoute(Name = "id")]
	public Guid Id { get; init; }
}
