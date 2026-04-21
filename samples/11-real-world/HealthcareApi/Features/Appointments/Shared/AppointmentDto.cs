// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Appointments.Shared;

public sealed class AppointmentDto
{
	public required Guid Id { get; init; }
	public required Guid PatientId { get; init; }
	public required string PhysicianName { get; init; }
	public required DateTimeOffset ScheduledAt { get; init; }
	public required string Reason { get; init; }
	public bool IsCancelled { get; init; }
}

public sealed class ScheduleAppointmentResult
{
	public required Guid AppointmentId { get; init; }
}
