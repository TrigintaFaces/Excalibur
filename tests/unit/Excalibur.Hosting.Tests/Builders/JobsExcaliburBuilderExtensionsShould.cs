// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Hosting.Builders;

using Quartz;

namespace Excalibur.Hosting.Tests.Builders;

/// <summary>
/// Paired behavioral tests for <see cref="JobsExcaliburBuilderExtensions"/> (S804 §804-C / bd-sdhocq A13).
/// </summary>
/// <remarks>
/// <para>
/// The <c>IExcaliburBuilder.AddJobs(...)</c> bridge forwards to the now-internal
/// <c>IServiceCollection.AddExcaliburJobHost(...)</c> aggregator one-to-one across all four
/// overloads. These tests exercise the bridge from the consumer-facing surface and pin the
/// underlying registration shape so a future regression analogous to <c>bd-zqkbnq</c>
/// (S803 γ-seam missed call-site) is caught at test time rather than through downstream
/// shard fan-out.
/// </para>
/// <para>
/// See also <see cref="AddJobsMinimalWiringConformanceTests"/> for the
/// <see cref="MinimalWiringConformanceTestKit{T}"/> bucket-level pin applied via the
/// <c>services.AddExcalibur(x =&gt; x.AddJobs(...))</c> composition-root path.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Jobs")]
[Trait("Feature", "Builders")]
public sealed class JobsExcaliburBuilderExtensionsShould : UnitTestBase
{
	#region Null-guards (4 overloads)

	[Fact]
	public void AddJobs_Assemblies_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IExcaliburBuilder)null!).AddJobs(Assembly.GetExecutingAssembly()));
	}

	[Fact]
	public void AddJobs_QuartzConfig_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IExcaliburBuilder)null!).AddJobs(configureQuartz: null));
	}

	[Fact]
	public void AddJobs_JobsConfig_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IExcaliburBuilder)null!).AddJobs(configureJobs: _ => { }));
	}

	[Fact]
	public void AddJobs_JobsConfig_ThrowWhenConfigureJobsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert — the inner AddExcaliburJobHost validates configureJobs is non-null
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(x => x.AddJobs(configureJobs: null!)));
	}

	[Fact]
	public void AddJobs_QuartzAndJobsConfig_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IExcaliburBuilder)null!).AddJobs(configureQuartz: null, configureJobs: null));
	}

	#endregion

	#region Fluent chain — all 4 overloads return builder

	[Fact]
	public void AddJobs_Assemblies_ReturnBuilderForFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? captured = null;

		// Act
		_ = services.AddExcalibur(x => captured = x.AddJobs(Assembly.GetExecutingAssembly()));

		// Assert — chain returns same builder the callback received
		captured.ShouldNotBeNull();
	}

	[Fact]
	public void AddJobs_QuartzConfig_ReturnBuilderForFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();
		var quartzConfigured = false;
		IExcaliburBuilder? captured = null;

		// Act
		_ = services.AddExcalibur(x => captured = x.AddJobs(
			configureQuartz: _ => quartzConfigured = true));

		// Assert
		captured.ShouldNotBeNull();
		quartzConfigured.ShouldBeTrue(
			"configureQuartz callback must be invoked by the bridge forwarder.");
	}

	[Fact]
	public void AddJobs_JobsConfig_ReturnBuilderForFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();
		var jobsConfigured = false;
		IExcaliburBuilder? captured = null;

		// Act
		_ = services.AddExcalibur(x => captured = x.AddJobs(
			configureJobs: _ => jobsConfigured = true));

		// Assert
		captured.ShouldNotBeNull();
		jobsConfigured.ShouldBeTrue(
			"configureJobs callback must be invoked by the bridge forwarder.");
	}

	[Fact]
	public void AddJobs_QuartzAndJobsConfig_ReturnBuilderForFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();
		var quartzConfigured = false;
		var jobsConfigured = false;
		IExcaliburBuilder? captured = null;

		// Act
		_ = services.AddExcalibur(x => captured = x.AddJobs(
			configureQuartz: _ => quartzConfigured = true,
			configureJobs: _ => jobsConfigured = true));

		// Assert
		captured.ShouldNotBeNull();
		quartzConfigured.ShouldBeTrue("configureQuartz callback must be invoked by the bridge.");
		jobsConfigured.ShouldBeTrue("configureJobs callback must be invoked by the bridge.");
	}

	#endregion

	#region Registration-shape forwarding (paired 1:1 with AddExcaliburJobHost)

	[Fact]
	public void AddJobs_RegisterQuartzSchedulerFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcalibur(x => x.AddJobs());

		// Assert — Quartz primitives registered by the underlying AddExcaliburJobHost
		services.ShouldContain(
			sd => sd.ServiceType == typeof(ISchedulerFactory),
			customMessage: "ISchedulerFactory must be registered after AddJobs — the bridge must forward to the internal AddExcaliburJobHost 1:1.");
	}

	[Fact]
	public void AddJobs_RegisterDispatchPrimitives()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcalibur(x => x.AddJobs());

		// Assert — Excalibur builder-level Dispatch primitives must still be present,
		// confirming the bridge does NOT replace the root AddExcalibur wiring.
		services.Any(sd => sd.ServiceType.FullName?.Contains("Dispatch", StringComparison.Ordinal) == true)
			.ShouldBeTrue("Dispatch primitives must remain registered after AddJobs.");
	}

	[Fact]
	public void AddJobs_IncreaseServiceCountVsBareAddExcalibur()
	{
		// Arrange — baseline: AddExcalibur only
		var baselineServices = new ServiceCollection();
		_ = baselineServices.AddExcalibur(_ => { });
		var baselineCount = baselineServices.Count;

		// Act — same root + AddJobs
		var withJobsServices = new ServiceCollection();
		_ = withJobsServices.AddExcalibur(x => x.AddJobs());

		// Assert — AddJobs must register additional services on top of the foundation.
		withJobsServices.Count.ShouldBeGreaterThan(
			baselineCount,
			"AddJobs must add Quartz + job host services on top of the base AddExcalibur foundation.");
	}

	[Fact]
	public void AddJobs_Idempotent_SecondCallDoesNotDuplicateSchedulerFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — invoke bridge twice
		_ = services.AddExcalibur(x => x.AddJobs().AddJobs());

		// Assert — Quartz registers its own TryAdd semantics on ISchedulerFactory
		services.Count(sd => sd.ServiceType == typeof(ISchedulerFactory))
			.ShouldBe(1, "ISchedulerFactory must not duplicate under repeated AddJobs calls (TryAdd semantics).");
	}

	#endregion

	#region Bridge delegation invariant — matches AddExcaliburJobHost(IHostApplicationBuilder)

	[Fact]
	public void AddJobs_MatchesHostBuilderCarveOut_ForSchedulerRegistration()
	{
		// Arrange — two paths that MUST register the same Quartz scheduler primitive:
		//   (1) composition-root bridge: services.AddExcalibur(x => x.AddJobs())
		//   (2) host carve-out:          hostBuilder.AddExcaliburJobHost()
		var bridgeServices = new ServiceCollection();
		_ = bridgeServices.AddExcalibur(x => x.AddJobs());

		var host = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
		_ = host.AddExcaliburJobHost();

		// Act — count scheduler-factory registrations each path produced
		var bridgeSchedulerFactories = bridgeServices.Count(sd => sd.ServiceType == typeof(ISchedulerFactory));
		var hostSchedulerFactories = host.Services.Count(sd => sd.ServiceType == typeof(ISchedulerFactory));

		// Assert — both paths wire a single ISchedulerFactory registration
		bridgeSchedulerFactories.ShouldBe(
			hostSchedulerFactories,
			"AddJobs bridge and AddExcaliburJobHost host carve-out must produce equivalent scheduler wiring " +
			"(paired-test invariant per ADR-325 §Secondary / S803 bd-zqkbnq lesson).");
	}

	#endregion
}
