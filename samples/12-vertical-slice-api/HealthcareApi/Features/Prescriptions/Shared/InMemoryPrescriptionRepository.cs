// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace HealthcareApi.Features.Prescriptions.Shared;

public sealed class InMemoryPrescriptionRepository : IPrescriptionRepository
{
	private readonly ConcurrentDictionary<Guid, PrescriptionDto> _prescriptions = new();

	public void Add(PrescriptionDto prescription) => _prescriptions[prescription.Id] = prescription;

	public PrescriptionDto? GetById(Guid id) => _prescriptions.GetValueOrDefault(id);
}
