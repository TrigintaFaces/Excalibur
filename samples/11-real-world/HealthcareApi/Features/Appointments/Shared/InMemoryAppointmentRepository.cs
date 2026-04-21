// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace HealthcareApi.Features.Appointments.Shared;

public sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
	private readonly ConcurrentDictionary<Guid, AppointmentDto> _appointments = new();

	public void Add(AppointmentDto appointment) => _appointments[appointment.Id] = appointment;

	public AppointmentDto? GetById(Guid id) => _appointments.GetValueOrDefault(id);

	public void Update(AppointmentDto appointment) => _appointments[appointment.Id] = appointment;
}
