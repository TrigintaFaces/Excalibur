// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configures the in-flight visibility-timeout heartbeat for the wired SQS subscriber.
/// </summary>
/// <remarks>
/// <para>
/// SQS makes a received message visible again (redelivered) once its visibility timeout
/// elapses while the message is still being processed. A long-running handler can therefore
/// be redelivered the same message before it finishes. When the heartbeat is enabled, the
/// subscriber periodically calls <c>ChangeMessageVisibility</c> for the in-flight message,
/// extending its visibility window for as long as the handler runs (up to <see cref="MaxExtension"/>).
/// </para>
/// <para>
/// The heartbeat is <b>opt-in</b> (<see cref="Enabled"/> defaults to <see langword="false"/>) so
/// that the default consumer behaviour is unchanged. Handlers should still be idempotent because
/// AWS delivery remains at-least-once.
/// </para>
/// </remarks>
public sealed class AwsSqsVisibilityHeartbeatOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the visibility-timeout heartbeat is enabled.
	/// </summary>
	/// <value><see langword="true"/> to extend visibility for long-running handlers; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the interval between visibility extensions.
	/// </summary>
	/// <value>The heartbeat interval. Default is 30 seconds.</value>
	/// <remarks>
	/// Choose an interval shorter than <see cref="VisibilityTimeout"/> so the extension is applied
	/// before the current window expires.
	/// </remarks>
	public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the visibility timeout applied on each extension.
	/// </summary>
	/// <value>The visibility window requested per heartbeat. Default is 60 seconds.</value>
	/// <remarks>
	/// AWS SQS allows a visibility timeout between 0 seconds and 12 hours.
	/// </remarks>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the maximum total time the heartbeat keeps a single message in-flight.
	/// </summary>
	/// <value>The maximum cumulative extension per message. Default is 10 minutes.</value>
	/// <remarks>
	/// Once this budget is exhausted the subscriber stops extending visibility and the message
	/// is allowed to become visible again, providing a safety bound against a stuck handler.
	/// </remarks>
	public TimeSpan MaxExtension { get; set; } = TimeSpan.FromMinutes(10);
}
