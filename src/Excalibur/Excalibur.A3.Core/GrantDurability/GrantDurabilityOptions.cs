// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Options governing whether authorization may run on a grant store that does not survive a restart.
/// </summary>
public sealed class GrantDurabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a volatile (in-memory) grant store is permitted.
	/// </summary>
	/// <value>
	/// <see langword="false" /> — the default — states that this host expects a durable grant store, and
	/// the framework fails fast at startup if a volatile store is registered.
	/// <see langword="true" /> permits a volatile store, and every grant is lost when the process exits.
	/// </value>
	/// <remarks>
	/// <para>
	/// The protective value is what a host gets by saying nothing. Losing the grant set does not fail
	/// loudly: authorization keeps answering, and it answers <em>deny</em> for everyone, because a user
	/// whose grants vanished is indistinguishable from a user who never had any. The system stays up and
	/// stops letting anyone in — and the grants were recorded as saved.
	/// </para>
	/// <para>
	/// Set this only for development and test hosts.
	/// </para>
	/// </remarks>
	public bool AllowVolatileGrantStore { get; set; }
}
