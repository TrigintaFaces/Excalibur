// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.A3.Authorization;

internal sealed record GrantCacheDocument(string UserId, long Revision, Grant[] Grants);
internal sealed record ActivityGroupCacheDocument(string TenantId, DateTimeOffset ExpiresOn, Dictionary<string, string[]> Groups);

[JsonSerializable(typeof(GrantCacheDocument))]
[JsonSerializable(typeof(ActivityGroupCacheDocument))]
internal sealed partial class GrantCacheJsonContext : JsonSerializerContext;
