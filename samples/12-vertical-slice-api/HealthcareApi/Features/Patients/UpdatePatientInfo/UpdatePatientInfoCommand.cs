// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace HealthcareApi.Features.Patients.UpdatePatientInfo;

/// <summary>
/// Command to update a patient's contact information. No return value (202 Accepted).
/// </summary>
public record UpdatePatientInfoCommand(
	Guid PatientId,
	string? Email,
	string? Phone) : IDispatchAction;
