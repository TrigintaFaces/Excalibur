// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using HealthcareApi.Features.Patients.Shared;

namespace HealthcareApi.Features.Patients.GetPatient;

/// <summary>
/// Query to retrieve a patient by ID. Returns <see cref="PatientDto"/>.
/// </summary>
public record GetPatientQuery(Guid PatientId) : IDispatchAction<PatientDto>;
