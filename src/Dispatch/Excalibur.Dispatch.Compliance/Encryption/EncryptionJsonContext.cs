// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Compliance;

[JsonSerializable(typeof(EncryptedData))]
internal sealed partial class EncryptionJsonContext : JsonSerializerContext
{
}
