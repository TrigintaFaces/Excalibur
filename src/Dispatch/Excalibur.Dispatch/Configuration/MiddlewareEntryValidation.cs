// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// The single place a profile's declared middleware entries are admitted into pipeline construction.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IPipelineProfile" /> is public, so a consumer may implement it directly and hand back entries this assembly never
/// constructed. Such an entry can carry a <see langword="null" /> middleware type and an unstated criticality without any compiler
/// complaint, because both are the zero value of their type. Admitting one would resolve it to whatever the zero value happens to mean,
/// which is exactly the silent outcome the criticality declaration exists to prevent.
/// </para>
/// <para>
/// Every path that turns profile entries into pipeline registrations routes through <see cref="ValidateEntry" />, so the invariant cannot
/// be re-opened by a future caller that forgets it. A rejection names the profile, the position, and which half is missing, because the
/// author of an invalid entry cannot see it in a debugger any more easily than in a log.
/// </para>
/// </remarks>
internal static class MiddlewareEntryValidation
{
	/// <summary>
	/// Validates a single declared entry, throwing when it cannot be built into a pipeline registration.
	/// </summary>
	/// <param name="entry"> The entry as the profile declared it. </param>
	/// <param name="profileName"> The declaring profile's name, used to name the offending profile in the failure. </param>
	/// <param name="index"> The entry's position within the profile's declared entries. </param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the entry carries no middleware type, or when its criticality is <see cref="MiddlewareCriticality.Unspecified" />.
	/// </exception>
	public static void ValidateEntry(in MiddlewareEntry entry, string profileName, int index)
	{
		if (entry.MiddlewareType is null)
		{
			throw new InvalidOperationException(string.Format(
				CultureInfo.InvariantCulture,
				Resources.MiddlewareEntry_MissingTypeFormat,
				profileName,
				index));
		}

		if (entry.Criticality == MiddlewareCriticality.Unspecified)
		{
			throw new InvalidOperationException(string.Format(
				CultureInfo.InvariantCulture,
				Resources.MiddlewareEntry_UnspecifiedCriticalityFormat,
				entry.MiddlewareType.Name,
				profileName,
				index));
		}
	}
}
