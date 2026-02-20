// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Provides utilities for accessing OpenAPI YAML files related to problem details schema.
/// </summary>
public static class ProblemDetailsOpenApi
{
	/// <summary>
	/// Gets the full path to the problem details OpenAPI YAML schema file.
	/// </summary>
	/// <returns> The absolute path to the problem-details.openapi.yaml file in the application's base directory. </returns>
	public static string GetYamlPath() => Path.Combine(AppContext.BaseDirectory, "openapi/problem-details.openapi.yaml");
}
