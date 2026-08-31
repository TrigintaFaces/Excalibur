// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Implemented by an erasure store whose backing schema can be verified — or provisioned — before the
/// store serves its first request.
/// </summary>
/// <remarks>
/// <para>
/// Schema provisioning is a <b>configuration</b> concern, so it is settled once at startup rather than on
/// the path of every write. A store that verified its schema inside <c>SaveRequestAsync</c> reports a
/// deployment fault as a failure of that one erasure request, at the moment a data subject's request is
/// being filed and to a caller with no way to tell the two apart.
/// </para>
/// <para>
/// The hosted service registered by <c>AddErasureSchemaValidation()</c> calls every registered validator
/// during host startup, so a mis-provisioned deployment fails to start instead of failing one erasure
/// request at a time. Stores keep a first-use check as the fail-closed floor for consumers that never run
/// that hosted service.
/// </para>
/// </remarks>
public interface IErasureSchemaValidator
{
	/// <summary>
	/// Verifies the store's backing schema, provisioning it first if the store is configured to do so.
	/// </summary>
	/// <param name="cancellationToken">A token to observe while awaiting the verification.</param>
	/// <returns>A task that completes when the schema has been verified.</returns>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// A required table is absent, or is present but missing columns this store's statements bind, and
	/// automatic provisioning is disabled.
	/// </exception>
	ValueTask ValidateSchemaAsync(CancellationToken cancellationToken);
}
