// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Batch;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options;

/// <summary>
/// Binds the batching option invariants, and binds them at the seam a consumer actually configures.
/// </summary>
/// <remarks>
/// <para>
/// The wiring arms below are the load-bearing ones. A validator that is never registered is inert, and
/// inert is indistinguishable from correct when only the validator's own arms are asserted — the class
/// under test would pass every unit arm while a misconfigured host started happily. So the arms that
/// matter resolve the validator through the real container built by <c>UseBatching</c> and let
/// <c>ValidateOnStart</c> run, rather than asserting that a registration is present.
/// </para>
/// <para>
/// Note which type is under test. <c>MicroBatchOptions</c> is the value handed to the batch processor's
/// constructor, not a configuration surface — nothing binds it and no public member accepts one — so its
/// invariants are constructor preconditions and are asserted as such elsewhere. The type a consumer
/// configures is <see cref="UnifiedBatchingOptions"/>, and that is what fails the host at start.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class UnifiedBatchingOptionsValidatorShould
{
	private static ValidateOptionsResult Validate(UnifiedBatchingOptions options) =>
		new UnifiedBatchingOptionsValidator().Validate(name: null, options);

	/// <summary>
	/// Builds a container the way a consumer does, through the public batching entry point.
	/// </summary>
	private static ServiceProvider BuildBatchingContainer(Action<UnifiedBatchingOptions>? configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		_ = new DispatchBuilder(services).UseBatching();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// PRECISION, and it comes first deliberately: the shipped defaults must pass.
	/// </summary>
	/// <remarks>
	/// Without this, every rejection arm below is satisfied by a validator that fails everything — which
	/// would refuse to start for every consumer who never configures batching, a worse outcome than the
	/// missing validation this replaces.
	/// </remarks>
	[Fact]
	public void Accept_the_shipped_defaults()
	{
		Validate(new UnifiedBatchingOptions()).Succeeded.ShouldBeTrue();
	}

	/// <summary>
	/// SAFETY. A batch size of zero is never full, so the batcher degrades to timer-only flushing.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Reject_a_non_positive_batch_size(int size)
	{
		var result = Validate(new UnifiedBatchingOptions { MaxBatchSize = size });

		result.Failed.ShouldBeTrue();
		// The message must name the OPTION, because the runtime symptom names nothing at all.
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchSize));
	}

	/// <summary>
	/// SAFETY. A non-positive delay turns the flush timer into a busy loop.
	/// </summary>
	/// <remarks>
	/// This is the rule the range attributes on the options type cannot express, because those ranges do
	/// not apply to a <see cref="TimeSpan"/>. It is the reason the validator is written out by hand
	/// rather than delegated to annotation validation.
	/// </remarks>
	[Fact]
	public void Reject_a_non_positive_batch_delay()
	{
		var result = Validate(new UnifiedBatchingOptions { MaxBatchDelay = TimeSpan.Zero });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchDelay));
	}

	/// <summary>
	/// SAFETY. A non-positive degree of parallelism leaves no worker permitted to process a batch.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Reject_a_non_positive_parallelism(int parallelism)
	{
		var result = Validate(new UnifiedBatchingOptions { MaxParallelism = parallelism });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxParallelism));
	}

	/// <summary>
	/// SAFETY. Every violation is reported at once, not just the first.
	/// </summary>
	/// <remarks>
	/// A validator that stops at the first failure makes a consumer fix one value, restart, and discover
	/// the next — which is the slow shape of a fast-fail check.
	/// </remarks>
	[Fact]
	public void Report_every_violation_rather_than_only_the_first()
	{
		var result = Validate(new UnifiedBatchingOptions
		{
			MaxBatchSize = 0,
			MaxBatchDelay = TimeSpan.Zero,
			MaxParallelism = 0,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchSize));
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchDelay));
		result.FailureMessage.ShouldContain(nameof(UnifiedBatchingOptions.MaxParallelism));
	}

	/// <summary>
	/// WIRING, SAFETY — the arm that fails if the validator is never registered.
	/// </summary>
	/// <remarks>
	/// Resolved through the container <c>UseBatching</c> builds, so this goes red both when the validator
	/// is wrong AND when it is correct but unregistered. The arms above cannot tell those apart.
	/// </remarks>
	[Fact]
	public void Fail_host_start_when_batching_is_misconfigured()
	{
		using var provider = BuildBatchingContainer(o => o.MaxBatchSize = 0);

		var startupValidator = provider.GetService<IStartupValidator>();
		startupValidator.ShouldNotBeNull(
			"UseBatching must chain ValidateOnStart, or a misconfigured batcher starts silently.");

		var ex = Should.Throw<OptionsValidationException>(startupValidator.Validate);
		ex.Message.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchSize));
	}

	/// <summary>
	/// WIRING, LIVENESS — the permitted configuration still starts.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a registration that rejects everything, which
	/// would stop every batching host from starting at all.
	/// </remarks>
	[Fact]
	public void Start_normally_on_a_valid_batching_configuration()
	{
		using var provider = BuildBatchingContainer(o =>
		{
			o.MaxBatchSize = 64;
			o.MaxBatchDelay = TimeSpan.FromMilliseconds(50);
			o.MaxParallelism = 4;
		});

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>
	/// WIRING, LIVENESS — a host that never configures batching still starts on the defaults.
	/// </summary>
	/// <remarks>
	/// The common case, and the one a too-eager validator breaks.
	/// </remarks>
	[Fact]
	public void Start_normally_when_batching_is_never_configured()
	{
		using var provider = BuildBatchingContainer(configure: null);

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>
	/// BYPASS, SAFETY — constructing the middleware directly must not skip the check.
	/// </summary>
	/// <remarks>
	/// The middleware's constructor is reachable without the pipeline, so startup validation alone leaves
	/// one route honest and the other silent. This arm binds the route the container does not cover.
	/// </remarks>
	[Fact]
	public void Refuse_construction_of_a_middleware_holding_unusable_options()
	{
		var ex = Should.Throw<ArgumentException>(() => new UnifiedBatchingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions { MaxBatchSize = 0 }),
			NullLogger<UnifiedBatchingMiddleware>.Instance,
			NullLoggerFactory.Instance));

		ex.Message.ShouldContain(nameof(UnifiedBatchingOptions.MaxBatchSize));
	}

	/// <summary>
	/// BYPASS, LIVENESS — a valid direct construction still succeeds.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by a constructor that refuses every option set.
	/// </remarks>
	[Fact]
	public void Construct_the_middleware_normally_on_valid_options()
	{
		using var middleware = new UnifiedBatchingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions()),
			NullLogger<UnifiedBatchingMiddleware>.Instance,
			NullLoggerFactory.Instance);

		middleware.ShouldNotBeNull();
	}

	/// <summary>
	/// WIRING. Repeated registration must not stack duplicate validators.
	/// </summary>
	/// <remarks>
	/// <c>UseBatching</c> registers through <c>TryAddEnumerable</c>; without it, a consumer composing two
	/// profiles that both enable batching would report every failure twice.
	/// </remarks>
	[Fact]
	public void Register_exactly_one_validator_however_often_batching_is_added()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var builder = new DispatchBuilder(services);
		_ = builder.UseBatching();
		_ = builder.UseBatching();

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IValidateOptions<UnifiedBatchingOptions>>()
			.OfType<UnifiedBatchingOptionsValidator>()
			.Count()
			.ShouldBe(1);
	}
}
