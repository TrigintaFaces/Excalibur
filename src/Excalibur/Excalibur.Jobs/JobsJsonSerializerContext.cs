// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Jobs.Coordination;
using Excalibur.Jobs.Core;

namespace Excalibur.Jobs;

/// <summary>
/// JSON serializer context for Excalibur.Jobs types, enabling AOT-compatible serialization.
/// </summary>
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JobConfig))]
[JsonSerializable(typeof(JobInstanceInfo))]
[JsonSerializable(typeof(JobInstanceCapabilities))]
[JsonSerializable(typeof(JobInstanceStatus))]
[JsonSerializable(typeof(HashSet<string>))]
[JsonSerializable(typeof(IReadOnlySet<string>))]
public sealed partial class JobsJsonSerializerContext : JsonSerializerContext
{
}
