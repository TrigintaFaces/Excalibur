// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Provides utilities for accessing the OpenAPI YAML schema for problem details.
/// </summary>
public static class ProblemDetailsOpenApi
{
	private const string ResourceName = "Excalibur.Hosting.Web.problem-details.openapi.yaml";

	/// <summary>
	/// Reads the problem details OpenAPI YAML schema from the embedded resource.
	/// </summary>
	/// <returns>The YAML content as a string.</returns>
	/// <exception cref="InvalidOperationException">The embedded resource was not found.</exception>
	public static string GetYaml()
	{
		var assembly = typeof(ProblemDetailsOpenApi).Assembly;
		using var stream = assembly.GetManifestResourceStream(ResourceName)
			?? throw new InvalidOperationException(
				$"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	/// <summary>
	/// Opens a read-only stream to the problem details OpenAPI YAML schema.
	/// </summary>
	/// <returns>A <see cref="Stream"/> positioned at the start of the YAML content.</returns>
	/// <exception cref="InvalidOperationException">The embedded resource was not found.</exception>
	public static Stream GetYamlStream()
	{
		var assembly = typeof(ProblemDetailsOpenApi).Assembly;
		return assembly.GetManifestResourceStream(ResourceName)
			?? throw new InvalidOperationException(
				$"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
	}
}
