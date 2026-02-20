// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Patients.Shared;

/// <summary>
/// Repository scoped to the Patients slice. Each slice owns its own data abstraction.
/// </summary>
public interface IPatientRepository
{
	void Add(PatientDto patient);
	PatientDto? GetById(Guid id);
	void Update(PatientDto patient);
}
