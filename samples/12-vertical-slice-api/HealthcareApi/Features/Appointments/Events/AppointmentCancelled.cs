// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace HealthcareApi.Features.Appointments.Events;

public record AppointmentCancelled(Guid AppointmentId, Guid PatientId) : IDispatchEvent;
