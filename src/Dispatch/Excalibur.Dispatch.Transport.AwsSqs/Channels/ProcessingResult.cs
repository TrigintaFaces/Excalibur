// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Result of message processing.
/// </summary>
public readonly struct ProcessingResult : IEquatable<ProcessingResult>
{
	public bool Success { get; init; }

	public string? Error { get; init; }

	public static ProcessingResult Ok() => new() { Success = true };

	public static ProcessingResult Failed(string error) => new() { Success = false, Error = error };

	public static bool operator ==(ProcessingResult left, ProcessingResult right) => left.Equals(right);

	public static bool operator !=(ProcessingResult left, ProcessingResult right) => !(left == right);

	public bool Equals(ProcessingResult other) =>
			Success == other.Success &&
			string.Equals(Error, other.Error, StringComparison.Ordinal);

	public override bool Equals(object? obj) => obj is ProcessingResult other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Success, Error);
}
