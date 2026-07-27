// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Options governing whether encryption may run on a key provider that does not survive a restart.
/// </summary>
public sealed class KeyDurabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a volatile (in-memory) key management provider is permitted.
	/// </summary>
	/// <value>
	/// <see langword="false" /> — the default — states that this host expects a durable key provider.
	/// <see langword="true" /> permits volatile keys, and anything encrypted under them becomes
	/// unrecoverable when the process exits. When compliance encryption is composed, a startup gate verifies
	/// the registered provider against this value and fails fast if a volatile provider is present while this
	/// is <see langword="false" />.
	/// </value>
	/// <remarks>
	/// <para>
	/// The default is the protective value, and it is what a host gets by saying nothing. This asymmetry is
	/// deliberate: the cost of wrongly requiring durability is a startup error a developer fixes in a
	/// minute, and the cost of wrongly allowing volatility is ciphertext nobody can ever read again. Those
	/// are not comparable, so the silent path is the recoverable one.
	/// </para>
	/// <para>
	/// Set this only for development and test hosts, where losing the keys on exit is the intended
	/// behaviour rather than an accident.
	/// </para>
	/// </remarks>
	public bool AllowVolatileKeyProvider { get; set; }
}
