// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Jobs.GoogleCloud;

/// <summary>
/// Payload sent to Google Cloud Scheduler for job invocations.
/// </summary>
internal sealed record JobSchedulePayload
{
    /// <summary>
    /// Gets the assembly-qualified type name of the job to execute.
    /// </summary>
    [JsonPropertyName("JobType")]
    public required string JobType { get; init; }

    /// <summary>
    /// Gets the logical name of the scheduled job.
    /// </summary>
    [JsonPropertyName("JobName")]
    public required string JobName { get; init; }
}

/// <summary>
/// Source-generated JSON serializer context for Google Cloud Jobs package types.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(JobSchedulePayload))]
internal sealed partial class JobsGcfJsonContext : JsonSerializerContext;
