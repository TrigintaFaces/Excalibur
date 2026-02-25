// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Indicates that a test requires SQL Server to be available.
/// </summary>
public class RequiresSqlServerAttribute : FactAttribute
{
	/// <summary>
	///     Initializes a new instance of the RequiresSqlServerAttribute class.
	/// </summary>
	public RequiresSqlServerAttribute()
	{
		if (!IsSqlServerAvailable())
		{
			Skip = "SQL Server is not available";
		}
	}

	private static bool IsSqlServerAvailable()
	{
		// Check if we're in a CI environment or if SQL Server is configured
		var ciEnv = Environment.GetEnvironmentVariable("CI");
		if (!string.IsNullOrEmpty(ciEnv))
		{
			return true; // Assume SQL Server is available in CI
		}

		// Could add more sophisticated checks here
		return true; // For now, assume it's available
	}
}
