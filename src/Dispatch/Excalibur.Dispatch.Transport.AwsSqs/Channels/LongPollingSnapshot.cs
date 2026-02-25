// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Snapshot of long polling metrics.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct LongPollingSnapshot : IEquatable<LongPollingSnapshot>
{
	public long MessagesReceived { get; init; }

	public long EmptyPolls { get; init; }

	public long Errors { get; init; }

	public double MessageRate { get; init; }

	public double EmptyPollRate { get; init; }

	public bool Equals(LongPollingSnapshot other) =>
			MessagesReceived == other.MessagesReceived &&
			EmptyPolls == other.EmptyPolls &&
			Errors == other.Errors &&
			MessageRate.Equals(other.MessageRate) &&
			EmptyPollRate.Equals(other.EmptyPollRate);

	public override bool Equals(object? obj) =>
			obj is LongPollingSnapshot other && Equals(other);

	public override int GetHashCode() =>
			HashCode.Combine(MessagesReceived, EmptyPolls, Errors, MessageRate, EmptyPollRate);

	public static bool operator ==(LongPollingSnapshot left, LongPollingSnapshot right) => left.Equals(right);

	public static bool operator !=(LongPollingSnapshot left, LongPollingSnapshot right) => !(left == right);
}
