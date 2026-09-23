// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace HealthcareApi.Features.Prescriptions.Events;

public record PrescriptionCreated(
	Guid PrescriptionId,
	Guid PatientId,
	string Medication) : IDispatchEvent;
