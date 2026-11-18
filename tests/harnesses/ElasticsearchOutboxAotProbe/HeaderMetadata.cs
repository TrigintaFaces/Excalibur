// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

internal sealed class GeneratedHeader
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

// Deliberately not registered in ProbeHeaderJson or any provider context.
internal sealed class MissingHeader
{
    public string Value { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GeneratedHeader))]
internal sealed partial class ProbeHeaderJson : JsonSerializerContext;
