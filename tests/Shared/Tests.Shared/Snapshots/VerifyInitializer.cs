// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

namespace Tests.Shared.Snapshots;

/// <summary>
/// Configures Verify with shared scrubbers and conventions for all test projects.
/// Uses <see cref="ModuleInitializerAttribute"/> to run automatically when the assembly loads.
/// </summary>
public static partial class VerifyInitializer
{
	[ModuleInitializer]
	public static void Initialize()
	{
		// Scrub dynamic values that change between runs
		VerifierSettings.ScrubInlineGuids();
		VerifierSettings.ScrubInlineDateTimeOffsets("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz");

		// Use project-relative directory for snapshot files
		DerivePathInfo(
			(sourceFile, projectDirectory, type, method) =>
				new PathInfo(
					directory: Path.Combine(projectDirectory, "Snapshots"),
					typeName: type.Name,
					methodName: method.Name));
	}
}
