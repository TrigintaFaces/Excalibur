// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using HealthcareApi.Features.Appointments.Shared;

namespace HealthcareApi.Features.Appointments.GetAppointment;

public record GetAppointmentQuery(Guid AppointmentId) : IDispatchAction<AppointmentDto>;
