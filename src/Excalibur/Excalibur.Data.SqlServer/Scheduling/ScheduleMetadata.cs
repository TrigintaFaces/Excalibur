// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Data.SqlServer.Scheduling;

internal sealed record ScheduleMetadata(string? CorrelationId, string? TraceParent, string? TenantId, string? UserId);

[JsonSerializable(typeof(ScheduleMetadata))]
internal sealed partial class ScheduleMetadataJsonContext : JsonSerializerContext;
