// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc;

/// <summary>
/// Handles a fatal error that occurs during CDC processing for a provider whose change events are of
/// type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change-event type.</typeparam>
/// <param name="exception">The exception that occurred during processing.</param>
/// <param name="failedEvent">
/// The change event that failed to process, or <see langword="null"/> when the fatal error occurred at
/// the connection / poll-loop level and no single change event is implicated.
/// </param>
/// <returns>A task representing the asynchronous operation.</returns>
/// <remarks>
/// When no handler is configured (<see cref="CdcFatalErrorOptions{TEvent}.OnFatalError"/> is
/// <see langword="null"/>), a fatal error is rethrown and processing stops — fail-loud, never a silent
/// infinite retry (ADR-338).
/// </remarks>
public delegate Task CdcFatalErrorHandler<TEvent>(Exception exception, TEvent? failedEvent)
	where TEvent : class;
