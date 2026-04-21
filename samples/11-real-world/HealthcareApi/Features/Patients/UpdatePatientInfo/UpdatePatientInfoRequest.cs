// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Patients.UpdatePatientInfo;

/// <summary>
/// API request DTO for updating patient information.
/// </summary>
public sealed class UpdatePatientInfoRequest
{
	public string? Email { get; init; }
	public string? Phone { get; init; }
}
