// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.EventSourcing.TieredStorage;

[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ColdArchiveMigrationReceipt))]
[JsonSerializable(typeof(ColdArchiveLayoutMarker))]
internal sealed partial class ColdArchiveMigrationJsonContext : JsonSerializerContext;
