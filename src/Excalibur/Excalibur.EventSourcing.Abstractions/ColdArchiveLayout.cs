// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Selects the persisted object layout of a cold event store.</summary>
/// <remarks>
/// Providers capture this selection when the store is constructed. Changing layout requires an explicit
/// namespace cutover through <see cref="IColdEventStoreMigration"/>; configuration alone never migrates data.
/// Unknown values must be rejected. Existing namespaces default to <see cref="Legacy"/> for compatibility.
/// </remarks>
public enum ColdArchiveLayout
{
	/// <summary>
	/// Uses tenant/aggregate-ID keys. Any namespace activation marker or per-stream migration receipt
	/// prevents legacy operations; an occupied slot belonging to another aggregate type must fail.
	/// </summary>
	Legacy = 0,

	/// <summary>
	/// Uses tenant/aggregate-type/aggregate-ID keys. Requires durable namespace activation and validates
	/// retained migration history before reading or updating an already migrated stream.
	/// Ordinary reads, writes and existence checks fail for an occupied legacy slot without a verified
	/// migration receipt. After activation, a slot with no legacy source can begin fresh typed history.
	/// Another aggregate type at the same tenant/ID can begin after the occupied slot's migration receipt
	/// and retained baseline validate.
	/// </summary>
	TypedV2 = 1,
}
