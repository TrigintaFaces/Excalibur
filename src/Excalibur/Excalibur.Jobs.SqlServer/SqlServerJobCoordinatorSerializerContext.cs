// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Jobs.Coordination;

namespace Excalibur.Jobs.SqlServer;

/// <summary>
/// Source-generated JSON serializer context for SQL Server job coordinator data types.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false)]
[JsonSerializable(typeof(JobInstanceInfo))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class SqlServerJobCoordinatorSerializerContext : JsonSerializerContext;
