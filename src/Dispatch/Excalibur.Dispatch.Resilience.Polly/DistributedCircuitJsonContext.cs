// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// JSON serialization context for distributed circuit breaker types.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(DistributedCircuitState))]
[JsonSerializable(typeof(DistributedCircuitMetrics))]
internal sealed partial class DistributedCircuitJsonContext : JsonSerializerContext;
