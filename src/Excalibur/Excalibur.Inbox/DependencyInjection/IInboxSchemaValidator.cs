// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Inbox;

/// <summary>
/// An inbox store that can verify its physical schema matches its registered deployment mode. The inbox
/// schema-validation hosted service resolves every registered validator and runs it at host startup, so a
/// mode/schema mismatch fails fast before the first message is processed rather than on first use.
/// </summary>
public interface IInboxSchemaValidator
{
	/// <summary>
	/// Verifies the store's physical schema against its deployment mode, throwing when they do not match
	/// (for example a multi-tenant store pointed at a single-tenant schema, or vice versa).
	/// </summary>
	/// <param name="cancellationToken">A token to observe while awaiting the verification.</param>
	/// <returns>A task that completes when the schema has been verified.</returns>
	ValueTask ValidateSchemaAsync(CancellationToken cancellationToken);
}
