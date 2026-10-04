// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Durable namespace activation; not a writer fence or proof that migration is complete.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ColdArchiveLayoutMarker(int FormatVersion, string NamespaceId, string Layout);
