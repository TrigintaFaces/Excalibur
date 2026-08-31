// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Spanner.Tests;

/// <summary>
/// Locks for the startup validation of <see cref="SpannerOptions"/>. Spanner is addressed by a three-part
/// resource path, so a missing segment does not fail loudly at configuration time on its own — it produces a
/// syntactically well-formed path pointing at nothing, and the failure surfaces later as a connection error
/// far from its cause. These arms are what keep that failure at startup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SpannerOptionsValidatorShould
{
	private static SpannerOptions Valid() => new()
	{
		ProjectId = "excalibur-project",
		InstanceId = "excalibur-instance",
		DatabaseId = "excalibur-database",
	};

	private static ValidateOptionsResult Validate(SpannerOptions options)
		=> new SpannerOptionsValidator().Validate(name: null, options);

	/// <summary>
	/// The liveness arm, and it carries the weight for the whole class: a validator that rejected every
	/// configuration would satisfy every rejection arm below and be indistinguishable from a correct one.
	/// </summary>
	[Fact]
	public void Accept_AFullyPopulatedConfiguration()
	{
		var result = Validate(Valid());

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_AMissingProjectId(string projectId)
	{
		var options = Valid();
		options.ProjectId = projectId;

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.ProjectId), StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_AMissingInstanceId(string instanceId)
	{
		var options = Valid();
		options.InstanceId = instanceId;

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.InstanceId), StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_AMissingDatabaseId(string databaseId)
	{
		var options = Valid();
		options.DatabaseId = databaseId;

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.DatabaseId), StringComparison.Ordinal));
	}

	[Fact]
	public void Reject_ANegativeAbortRetryCount()
	{
		var options = Valid();
		options.MaxAbortRetries = -1;

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.MaxAbortRetries), StringComparison.Ordinal));
	}

	[Fact]
	public void Reject_ANegativeAbortRetryBaseDelay()
	{
		var options = Valid();
		options.AbortRetryBaseDelayMilliseconds = -1;

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.AbortRetryBaseDelayMilliseconds), StringComparison.Ordinal));
	}

	/// <summary>
	/// Zero is the boundary of "non-negative" and it is a legitimate choice: it means do not retry an
	/// <c>ABORTED</c> transaction, and surface the conflict to the caller immediately. An off-by-one that
	/// turned the bound into "positive" would silently forbid that policy.
	/// </summary>
	[Fact]
	public void Accept_ZeroRetriesAndZeroBackoff()
	{
		var options = Valid();
		options.MaxAbortRetries = 0;
		options.AbortRetryBaseDelayMilliseconds = 0;

		var result = Validate(options);

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	/// <summary>
	/// The validator accumulates rather than short-circuits, so a wholly-unconfigured provider tells the
	/// operator everything that is wrong in one startup failure instead of one field per restart.
	/// </summary>
	[Fact]
	public void Report_EveryFailure_NotMerelyTheFirst()
	{
		var options = new SpannerOptions
		{
			MaxAbortRetries = -1,
			AbortRetryBaseDelayMilliseconds = -1,
		};

		var result = Validate(options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldNotBeNull();
		result.Failures!.Count().ShouldBe(5);
	}

	[Fact]
	public void Reject_ANullOptionsInstance()
	{
		var validator = new SpannerOptionsValidator();

		_ = Should.Throw<ArgumentNullException>(() => validator.Validate(name: null, options: null!));
	}
}
