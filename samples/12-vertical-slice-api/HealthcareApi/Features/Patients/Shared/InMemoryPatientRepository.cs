// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace HealthcareApi.Features.Patients.Shared;

/// <summary>
/// In-memory patient store. In a real app this would be backed by a database.
/// </summary>
public sealed class InMemoryPatientRepository : IPatientRepository
{
	private readonly ConcurrentDictionary<Guid, PatientDto> _patients = new();

	public void Add(PatientDto patient) => _patients[patient.Id] = patient;

	public PatientDto? GetById(Guid id) => _patients.GetValueOrDefault(id);

	public void Update(PatientDto patient) => _patients[patient.Id] = patient;
}
