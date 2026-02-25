// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Source-generated JSON serializer context for security event types.
/// Enables AOT-safe serialization for security event stores.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false)]
[JsonSerializable(typeof(SecurityEvent))]
[JsonSerializable(typeof(SecurityEventQuery))]
internal sealed partial class SecurityEventSerializerContext : JsonSerializerContext;
