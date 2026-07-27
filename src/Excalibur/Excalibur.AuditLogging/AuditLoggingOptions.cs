// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.AuditLogging;

/// <summary>
/// Options governing how audit logging treats the durability of its configured store.
/// </summary>
public sealed class AuditLoggingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a volatile (non-durable) audit store is permitted.
	/// </summary>
	/// <value>
	/// <see langword="false" /> — the default — states that this host expects a durable audit store, and
	/// the framework fails fast at startup if a volatile store is registered.
	/// <see langword="true" /> permits a volatile store, and the audit trail is then lost on process
	/// restart.
	/// </value>
	/// <remarks>
	/// <para>
	/// The default is the protective value, and it is the value you get by saying nothing. Accepting a
	/// volatile audit trail is a deliberate statement a host has to make out loud; it is never inferred from
	/// an omission, because the cost of guessing wrong is a compliance record that was never written and a
	/// caller that was told it was.
	/// </para>
	/// <para>
	/// Set this only for development and test hosts. It does not weaken any other audit guarantee — writes
	/// still succeed and are still readable within the process lifetime — it only withdraws the requirement
	/// that they outlive it.
	/// </para>
	/// </remarks>
	public bool AllowVolatileAuditStore { get; set; }
}
