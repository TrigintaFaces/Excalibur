// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="EmulatorRequirement"/>, the decision that governs whether an
/// emulator-backed integration suite may skip when its emulator is unavailable.
/// </summary>
/// <remarks>
/// This binds the same source file the integration suite compiles, so the assertions below constrain
/// the decision that actually runs rather than a restatement of it. The suite it governs cannot report
/// its own correctness: when it skips it exits green with zero failures, so its own result summary looks
/// the same whether the choice was right or wrong. The only place the skip-versus-fail choice can be
/// checked is here, where the choice is a pure function.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class EmulatorRequirementShould : UnitTestBase
{
	#region Required In CI

	[Fact]
	public void RequireTheEmulatorWhenCiIsTrue()
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci: "true", githubActions: null);

		// Assert
		required.ShouldBeTrue();
	}

	[Fact]
	public void RequireTheEmulatorWhenGithubActionsIsTrue()
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci: null, githubActions: "true");

		// Assert
		required.ShouldBeTrue();
	}

	[Fact]
	public void RequireTheEmulatorWhenBothCiSignalsAreTrue()
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci: "true", githubActions: "true");

		// Assert
		required.ShouldBeTrue();
	}

	[Theory]
	[InlineData("TRUE")]
	[InlineData("True")]
	[InlineData("tRuE")]
	public void RequireTheEmulatorRegardlessOfCasing(string ci)
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci, githubActions: null);

		// Assert
		required.ShouldBeTrue();
	}

	#endregion Required In CI

	#region Optional Outside CI

	// The liveness arm. Without it the decision could satisfy every assertion above by requiring the
	// emulator unconditionally, which would turn a developer's missing container runtime into a red
	// build on a machine that was never expected to have one.

	[Fact]
	public void NotRequireTheEmulatorWhenNeitherSignalIsSet()
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci: null, githubActions: null);

		// Assert
		required.ShouldBeFalse();
	}

	[Fact]
	public void NotRequireTheEmulatorWhenBothSignalsAreFalse()
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci: "false", githubActions: "false");

		// Assert
		required.ShouldBeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData("0")]
	[InlineData("yes")]
	public void NotRequireTheEmulatorForValuesThatAreNotAffirmative(string ci)
	{
		// Arrange / Act
		var required = EmulatorRequirement.IsRequired(ci, githubActions: null);

		// Assert
		required.ShouldBeFalse();
	}

	#endregion Optional Outside CI
}
