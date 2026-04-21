// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace HealthcareApi.Features.Patients.GetPatient;

/// <summary>
/// API request DTO for GET endpoints. [FromRoute] binds the ID from the URL path.
/// </summary>
public sealed class GetPatientRequest
{
	[FromRoute(Name = "id")]
	public Guid Id { get; init; }
}
