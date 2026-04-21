// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using HealthcareApi.Features.Prescriptions.Shared;

using Microsoft.AspNetCore.Authorization;

namespace HealthcareApi.Features.Prescriptions.CreatePrescription;

/// <summary>
/// Command to create a prescription. The [Authorize] attribute is read by the
/// ASP.NET Core authorization bridge middleware and evaluated against the user's claims.
/// Only physicians can create prescriptions.
/// </summary>
[Authorize(Roles = "Physician")]
public record CreatePrescriptionCommand(
	Guid PatientId,
	string Medication,
	string Dosage,
	int DaysSupply) : IDispatchAction<CreatePrescriptionResult>;
