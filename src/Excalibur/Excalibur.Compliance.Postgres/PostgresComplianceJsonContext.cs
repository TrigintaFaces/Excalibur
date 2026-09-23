// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json.Serialization;


namespace Excalibur.Compliance.Postgres;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(ErasureSummary))]
[JsonSerializable(typeof(VerificationSummary))]
[JsonSerializable(typeof(IReadOnlyList<ErasureException>))]
internal sealed partial class PostgresComplianceJsonContext : JsonSerializerContext
{
}
