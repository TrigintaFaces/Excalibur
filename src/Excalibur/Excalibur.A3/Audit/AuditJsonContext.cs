// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Excalibur.A3.Audit.Events;

namespace Excalibur.A3.Audit;

/// <summary>
/// Source-generated JSON serializer context for A3 audit types.
/// Enables AOT-safe serialization for audit middleware outbox fallback.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(RaisedBy))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ActivityAudited))]
[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
	Justification = "ActivityAudited.Request is a string property; source-gen serialization does not use reflection")]
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
	Justification = "ActivityAudited.Request is a string property; source-gen serialization does not use dynamic code")]
internal sealed partial class AuditJsonContext : JsonSerializerContext;
