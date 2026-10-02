// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Compliance;

/// <summary>
/// The source-generated metadata <see cref="EncryptedData.TryParse"/> reads a stored envelope with.
/// </summary>
/// <remarks>
/// Source-generated rather than reflection-based because this package declares itself AOT-compatible, and a
/// reflection-based read would be the one member of the parse path a trimmer could remove. Declared with no
/// <see cref="JsonSourceGenerationOptionsAttribute"/>, so property names are the declared ones: the writer
/// that produced the stored bytes uses the same default, and a naming policy added on either side alone would
/// make every previously stored envelope unparseable.
/// </remarks>
[JsonSerializable(typeof(EncryptedData))]
internal sealed partial class EncryptedDataJsonContext : JsonSerializerContext;
