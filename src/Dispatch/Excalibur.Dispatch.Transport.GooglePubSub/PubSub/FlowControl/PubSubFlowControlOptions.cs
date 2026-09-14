// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Bounds how many unacknowledged messages the streaming subscriber holds in memory.
/// </summary>
/// <remarks>
/// These are applied when the subscriber client is built and cannot change afterwards, so they describe
/// a ceiling for the life of the subscription rather than a value tuned while it runs. They govern the
/// streaming subscriber only; the raw-pull receiver path does not use flow control.
/// </remarks>
public sealed class PubSubFlowControlOptions
{
	/// <summary>
	/// Gets or sets the maximum number of unacknowledged messages held in memory before the subscriber
	/// stops pulling more.
	/// </summary>
	/// <value> Defaults to 1000. Set to 0 to leave the limit to the client library. </value>
	public int MaxOutstandingElementCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum total size, in bytes, of unacknowledged messages held in memory before
	/// the subscriber stops pulling more.
	/// </summary>
	/// <value> Defaults to 100,000,000 (100 MB). Set to 0 to leave the limit to the client library. </value>
	public long MaxOutstandingByteCount { get; set; } = 100_000_000;

	/// <summary>
	/// Throws when a limit is negative.
	/// </summary>
	/// <remarks>
	/// Zero is meaningful — it defers to the client library's own default — so only a negative value is
	/// rejected. Without this, a negative was indistinguishable from zero at the point the subscriber was
	/// built and silently became "use the library default".
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when either limit is negative. </exception>
	public void Validate()
	{
		if (MaxOutstandingElementCount < 0)
		{
			throw new ArgumentException(
				"MaxOutstandingElementCount cannot be negative. Use 0 to defer to the client library's default.",
				nameof(MaxOutstandingElementCount));
		}

		if (MaxOutstandingByteCount < 0)
		{
			throw new ArgumentException(
				"MaxOutstandingByteCount cannot be negative. Use 0 to defer to the client library's default.",
				nameof(MaxOutstandingByteCount));
		}
	}
}
