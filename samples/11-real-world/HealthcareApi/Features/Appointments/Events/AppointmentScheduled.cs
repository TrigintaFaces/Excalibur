// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace HealthcareApi.Features.Appointments.Events;

/// <summary>
/// Published when an appointment is scheduled. The Notifications slice reacts to this.
/// </summary>
public record AppointmentScheduled(
	Guid AppointmentId,
	Guid PatientId,
	string PhysicianName,
	DateTimeOffset ScheduledAt) : IDispatchEvent;
