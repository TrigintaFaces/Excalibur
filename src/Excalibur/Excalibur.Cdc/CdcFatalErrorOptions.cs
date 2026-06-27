// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc;

/// <summary>
/// Options for configuring fatal-error handling during CDC processing for a provider whose change
/// events are of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change-event type.</typeparam>
public sealed class CdcFatalErrorOptions<TEvent>
	where TEvent : class
{
	/// <summary>
	/// Gets or sets the delegate invoked when a fatal error occurs during CDC processing.
	/// When <see langword="null"/>, the processor rethrows the exception and stops processing
	/// (fail-loud — never a silent infinite retry).
	/// </summary>
	public CdcFatalErrorHandler<TEvent>? OnFatalError { get; set; }
}
