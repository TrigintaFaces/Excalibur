// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Compliance.SqlServer;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(ErasureSummary))]
[JsonSerializable(typeof(VerificationSummary))]
internal sealed partial class SqlServerComplianceJsonContext : JsonSerializerContext
{
}
