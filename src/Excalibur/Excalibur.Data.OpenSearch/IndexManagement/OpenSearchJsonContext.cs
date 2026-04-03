// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Request payload for ISM retry/change_policy API calls.
/// </summary>
internal sealed record IsmRetryRequest
{
    /// <summary>
    /// Gets the target ISM state name.
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }
}

/// <summary>
/// Source-generated JSON serializer context for OpenSearch index management types.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(IsmRetryRequest))]
internal sealed partial class OpenSearchJsonContext : JsonSerializerContext;
