// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Serializes EventStore telemetry integration tests to reduce emulator contention
/// under CI load (notably Cosmos emulator timeout pressure).
/// </summary>
[CollectionDefinition("EventStore Telemetry Tests", DisableParallelization = true)]
public sealed class EventStoreTelemetryCollection
{
}
