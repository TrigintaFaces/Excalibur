// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Event arguments for acknowledgment deadline extension events.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AckDeadlineExtensionEventArgs" /> class. </remarks>
public sealed class AckDeadlineExtensionEventArgs(string ackId, int extensionSeconds) : EventArgs
{
	/// <summary>
	/// Gets the acknowledgment ID.
	/// </summary>
	/// <value>
	/// The acknowledgment ID.
	/// </value>
	public string AckId { get; } = ackId;

	/// <summary>
	/// Gets the extension seconds.
	/// </summary>
	/// <value>
	/// The extension seconds.
	/// </value>
	public int ExtensionSeconds { get; } = extensionSeconds;
}
