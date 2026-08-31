// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Postgres.Persistence;

namespace Excalibur.Data.Tests.Postgres.Persistence;

/// <summary>
/// Pins the startup rejection of Postgres resilience configurations that cannot be honoured, and that the
/// ranges declared on those options are actually enforced.
/// </summary>
/// <remarks>
/// The ceiling on the backoff schedule is only half the fix: it is worth nothing if the configuration
/// feeding it is never checked. The ranges on the resilience options sit one level below the object the
/// parent hands to the annotation validator, and that validator inspects only the properties of the
/// object it is given - so until the parent validated the nested object explicitly, the ranges bound
/// nothing and an out-of-range value was accepted in silence.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresResilienceOptionsValidationShould : UnitTestBase
{
	[Fact]
	public void RejectACeilingBelowTheBaseDelayRatherThanReinterpretIt()
	{
		var options = CreateOptions();
		options.Resilience.RetryDelayMilliseconds = 20_000;
		options.Resilience.MaxRetryDelayMilliseconds = 5_000;

		var ex = Should.Throw<ValidationException>(() => options.Validate());

		ex.Message.ShouldContain(nameof(PostgresPersistenceResilienceOptions.MaxRetryDelayMilliseconds));
	}

	[Fact]
	public void EnforceTheDeclaredAttemptRangeRatherThanMerelyDeclareIt()
	{
		var options = CreateOptions();
		options.Resilience.MaxRetryAttempts = 5_000;

		_ = Should.Throw<ValidationException>(() => options.Validate());
	}

	[Fact]
	public void EnforceTheDeclaredCeilingRangeRatherThanMerelyDeclareIt()
	{
		var options = CreateOptions();
		options.Resilience.MaxRetryDelayMilliseconds = 60 * 60 * 1000;

		_ = Should.Throw<ValidationException>(() => options.Validate());
	}

	[Fact]
	public void AcceptTheDefaultsUnchanged()
	{
		// Liveness: the arms above must fail because the configuration is bad, not because this validator
		// rejects everything handed to it.
		var options = CreateOptions();
		Should.NotThrow(() => options.Validate());
	}

	private static PostgresPersistenceOptions CreateOptions() =>
		new() { ConnectionString = "Host=localhost;Database=excalibur" };
}
