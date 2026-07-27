// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Options governing whether scheduled delivery may run on a schedule store that does not survive a
/// restart.
/// </summary>
public sealed class ScheduleDurabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a volatile (in-memory) schedule store is permitted.
	/// </summary>
	/// <value>
	/// <see langword="false" /> — the default — states that this host expects a durable schedule store, and
	/// the framework fails fast at startup if a volatile store is registered.
	/// <see langword="true" /> permits a volatile store, and everything scheduled but not yet due is lost
	/// when the process exits.
	/// </value>
	/// <remarks>
	/// <para>
	/// The protective value is the one a host gets by saying nothing. A scheduled delivery is a promise
	/// accepted now and kept later; a store that forgets it on restart breaks that promise at the moment
	/// nobody is watching, having already reported the schedule as accepted.
	/// </para>
	/// <para>
	/// Set this only for development and test hosts, where losing pending schedules on exit is intended.
	/// </para>
	/// </remarks>
	public bool AllowVolatileScheduleStore { get; set; }
}
