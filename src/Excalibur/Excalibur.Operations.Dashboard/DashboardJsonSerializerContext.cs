// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// System.Text.Json source-generation context for the dashboard's response DTOs, keeping the
/// consumer-facing serialization path AOT- and trim-safe (no reflection-based metadata, no
/// <c>[RequiresUnreferencedCode]</c> on the consumer surface).
/// </summary>
/// <remarks>
/// Each subsystem declares an additional <c>partial</c> file for this class carrying the
/// <see cref="JsonSerializableAttribute"/> entries for its own DTOs, so a subsystem adds its
/// serializable types in its own file rather than editing a shared central one. The generator
/// aggregates the attributes across all partial declarations.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DashboardCapabilities))]
internal sealed partial class DashboardJsonSerializerContext : JsonSerializerContext
{
}
