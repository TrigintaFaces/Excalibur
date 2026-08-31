// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Data.SqlServer.ErrorHandling;

/// <summary>
/// Source-generated metadata for the dead-letter message property bag, so the store serializes it without
/// reflection and needs no trimming or ahead-of-time annotation for that path.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class DeadLetterJsonContext : JsonSerializerContext
{
}
