// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Decides whether an emulator-backed integration suite may skip when its emulator is unavailable,
/// or must fail instead.
/// </summary>
/// <remarks>
/// The decision is a pure function of values passed in by the caller. Environment lookups are
/// deliberately performed by the caller and handed in as parameters: a function that reads ambient
/// state cannot be exercised without mutating that state, and ambient state is caller-mutable, so a
/// suite could silently arrange its own exemption. Passing the values in makes that inexpressible and
/// makes the decision directly testable.
/// </remarks>
internal static class EmulatorRequirement
{
	/// <summary>
	/// Determines whether the emulator is required rather than optional.
	/// </summary>
	/// <param name="ci">
	/// The raw value of the <c>CI</c> environment variable, or <see langword="null"/> when it is unset.
	/// </param>
	/// <param name="githubActions">
	/// The raw value of the <c>GITHUB_ACTIONS</c> environment variable, or <see langword="null"/> when
	/// it is unset.
	/// </param>
	/// <returns>
	/// <see langword="true"/> when the emulator is required, so an unavailable emulator must fail the
	/// suite; <see langword="false"/> when it is optional, so an unavailable emulator may skip.
	/// </returns>
	internal static bool IsRequired(string? ci, string? githubActions) =>
		IsAffirmative(ci) || IsAffirmative(githubActions);

	private static bool IsAffirmative(string? value) =>
		string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
