// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Source-generated JSON serializer context for <see cref="SecurityAuditEvent"/>.
/// Enables AOT-safe serialization for integrity hashing and archival.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false)]
[JsonSerializable(typeof(SecurityAuditEvent))]
internal sealed partial class SecurityAuditEventSerializerContext : JsonSerializerContext;
