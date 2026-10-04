// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Binds one sealed legacy object to its verified typed copy within a storage namespace.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ColdArchiveMigrationReceipt(
	int FormatVersion,
	string DigestAlgorithm,
	string NamespaceId,
	string LegacyKey,
	string TypedKey,
	string TenantId,
	string AggregateId,
	string AggregateType,
	string SourceRevision,
	string SourceSha256);
