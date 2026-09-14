// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// A no-op implementation of <see cref="IMessageMetrics"/> that records nothing.
/// </summary>
/// <remarks>
/// <para>
/// This is the default registration, so a host that calls <c>AddDispatch()</c> and nothing else can
/// still construct the metrics-recording middleware. Recording is a cross-cutting concern: it must
/// never be the reason a message fails to dispatch, so the absence of a real metrics implementation
/// degrades to silence rather than to a container resolution failure.
/// </para>
/// <para>
/// Register a real <see cref="IMessageMetrics"/> to collect metrics; because the default is registered
/// with try-add semantics, any consumer registration replaces it without further configuration.
/// </para>
/// <para>
/// Deliberately <see langword="internal"/>: a consumer never needs to name this type. Zero-config
/// behaviour comes from calling <c>AddDispatch()</c>, and supplying metrics means registering an
/// <see cref="IMessageMetrics"/> of your own — neither path mentions the null object. Making it public
/// would add a type to the supported contract that exists only so the container has something to
/// resolve.
/// </para>
/// </remarks>
internal sealed class NullMessageMetrics : IMessageMetrics
{
	/// <summary>
	/// The shared singleton instance.
	/// </summary>
	public static readonly NullMessageMetrics Instance = new();

	private NullMessageMetrics()
	{
	}

	/// <inheritdoc />
	public void RecordMessageProcessed(string messageType, TimeSpan duration)
	{
		// Intentionally empty: no metrics sink is configured.
	}

	/// <inheritdoc />
	public void RecordMessageFailed(string messageType, string errorMessage)
	{
		// Intentionally empty: no metrics sink is configured.
	}

	/// <inheritdoc />
	public Task RecordMessageProcessedAsync(
		object context,
		TimeSpan duration,
		bool success,
		CancellationToken cancellationToken) => Task.CompletedTask;
}
