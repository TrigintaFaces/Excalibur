// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Prescriptions.Shared;

public interface IPrescriptionRepository
{
	void Add(PrescriptionDto prescription);
	PrescriptionDto? GetById(Guid id);
}
