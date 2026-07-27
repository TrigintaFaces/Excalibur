// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Workflows;

/// <summary>
/// The source-generated <see cref="JsonSerializerContext"/> for the framework-owned types the durable
/// workflow engine serializes, so those types round-trip without reflection under trimming and native AOT.
/// </summary>
/// <remarks>
/// The framework owns and source-generates these envelope and projection types unconditionally. Arbitrary
/// consumer activity/workflow payload types are not owned by the framework and cannot be source-generated
/// here — for a fully trim/AOT-safe payload path a consumer supplies their own source-generated resolver via
/// <see cref="WorkflowOptions.PayloadTypeInfoResolver"/>, which is composed ahead of this context.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorkflowState))]
[JsonSerializable(typeof(WorkflowInstanceSummary))]
[JsonSerializable(typeof(WorkflowStoreStatistics))]
[JsonSerializable(typeof(WorkflowQueryFilter))]
[JsonSerializable(typeof(WorkflowStatus))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(System.Guid))]
[JsonSerializable(typeof(System.DateTimeOffset))]
internal sealed partial class WorkflowJsonSerializerContext : JsonSerializerContext
{
}
