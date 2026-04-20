// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace HealthcareApi.Features.Patients.Events;

/// <summary>
/// Domain event published when a new patient is registered.
/// The Notifications slice subscribes to this event to send a welcome message.
/// </summary>
public record PatientRegistered(Guid PatientId, string Email) : IDispatchEvent;
