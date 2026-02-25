// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Firestore;

/// <summary>
/// Thread-safe helper for Firestore emulator host configuration.
/// </summary>
/// <remarks>
/// <para>
/// Firestore SDK uses the <c>FIRESTORE_EMULATOR_HOST</c> environment variable to detect
/// emulator usage. Setting this variable is process-wide, so we guard it with a lock
/// and log a warning if a different host is already configured.
/// </para>
/// </remarks>
public static class FirestoreEmulatorHelper
{
	private const string EmulatorHostVariable = "FIRESTORE_EMULATOR_HOST";
#if NET9_0_OR_GREATER
	private static readonly Lock Lock = new();
#else
	private static readonly object Lock = new();
#endif

	/// <summary>
	/// Configures the emulator host environment variable thread-safely.
	/// </summary>
	/// <param name="emulatorHost">The emulator host to set (e.g., "localhost:8080").</param>
	/// <returns>
	/// <see langword="true"/> if the environment variable was set or already matched;
	/// <see langword="false"/> if a different value was already configured.
	/// </returns>
	public static bool TryConfigureEmulatorHost(string emulatorHost)
	{
		if (string.IsNullOrWhiteSpace(emulatorHost))
		{
			return false;
		}

		lock (Lock)
		{
			var existing = Environment.GetEnvironmentVariable(EmulatorHostVariable);

			if (string.IsNullOrEmpty(existing))
			{
				Environment.SetEnvironmentVariable(EmulatorHostVariable, emulatorHost);
				return true;
			}

			// Already set to the same value -- no conflict
			if (string.Equals(existing, emulatorHost, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// Different host already configured -- cannot override (process-wide)
			return false;
		}
	}
}
