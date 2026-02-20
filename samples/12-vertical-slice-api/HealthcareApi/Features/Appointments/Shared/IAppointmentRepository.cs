// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Appointments.Shared;

public interface IAppointmentRepository
{
	void Add(AppointmentDto appointment);
	AppointmentDto? GetById(Guid id);
	void Update(AppointmentDto appointment);
}
