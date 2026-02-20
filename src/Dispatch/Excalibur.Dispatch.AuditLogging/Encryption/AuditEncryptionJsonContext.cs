// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Encryption;

/// <summary>
/// AOT-safe JSON serialization context for audit encryption types.
/// </summary>
[JsonSerializable(typeof(EncryptedData))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AuditEncryptionJsonContext : JsonSerializerContext;
