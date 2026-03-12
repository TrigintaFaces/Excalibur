// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Smoke tests that verify all 115 shipping packages compile and link together.
/// This project's existence as a build-verification gate is the primary value --
/// if this project builds, all packages compose without conflicts.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Component", "Platform")]
public class SmokeTests
{
	[Fact]
	public void AllPackagesComposeWithoutConflicts()
	{
		// This test's primary purpose is build verification.
		// If all 115 ProjectReferences resolve without conflicts,
		// the test passes implicitly.
		Assert.True(true, "All 115 shipping packages compose without build conflicts.");
	}
}
