// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Testing.Containers;

/// <summary>
/// Container lifecycle timeouts, scaled by the <c>TEST_TIMEOUT_MULTIPLIER</c> environment variable so a
/// slow or heavily-loaded CI host can be given proportionally more time without editing code.
/// </summary>
public static class ContainerTimeouts
{
	/// <summary>Gets the timeout for container startup operations (default 120s, before scaling).</summary>
	public static TimeSpan ContainerStart => TimeSpan.FromSeconds(120 * Multiplier);

	/// <summary>Gets the timeout for container disposal operations (default 30s, before scaling).</summary>
	public static TimeSpan ContainerDispose => TimeSpan.FromSeconds(30 * Multiplier);

	private static double Multiplier =>
		double.TryParse(Environment.GetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER"), out var m) ? m : 1.0;
}
