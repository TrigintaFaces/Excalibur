// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Source-generated JSON metadata for the closed payload shapes this package reads and writes
/// itself, enabling serialization without runtime reflection so those paths are safe under trimming
/// and ahead-of-time compilation.
/// </summary>
/// <remarks>
/// No generation options are declared, so the emitted contract matches the serializer defaults used
/// by the reflection-based calls this replaces: property names are taken verbatim (or from an
/// explicit name attribute) and dictionary keys are written unchanged. Persisted schema history and
/// migration results therefore remain readable without migration.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(SchemaMigrationResult))]
[JsonSerializable(typeof(SecureElasticsearchAuthenticationProvider.OAuth2TokenResponse))]
internal sealed partial class ElasticSearchInternalJsonContext : JsonSerializerContext;
