// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker timer type the <see cref="AddCronTimerTransportMinimalWiringConformanceTests"/>
/// uses as the generic argument to <c>AddCronTimerTransport&lt;T&gt;</c>.
/// </summary>
public sealed class ConformancePinTimer : ICronTimerMarker { }

/// <summary>
/// Regression pin for S790 FIX 6 (commit <c>133aa1415</c>):
/// <see cref="Microsoft.Extensions.DependencyInjection.CronTimerTransportServiceCollectionExtensions.AddCronTimerTransport{TTimer}(IServiceCollection, string, Action{CronTimerOptions}?)"/>
/// must accept a 6-field (second-level) cron expression by auto-detecting the field count
/// via <see cref="TimeZoneAwareCronExpression"/>'s constructor whitespace-token scan.
/// </summary>
/// <remarks>
/// <para>
/// Bucket A — framework-level parser auto-detect. The extension itself is self-sufficient:
/// <c>TryAddSingleton&lt;ICronScheduler, CronScheduler&gt;()</c> supplies the default scheduler;
/// the AddKeyedSingleton per timer name is keyed-unique; options are registered via the
/// MS-standard <c>AddOptions&lt;T&gt;(name).ValidateOnStart()</c> pattern covered by the
/// harness benign-drift whitelist.
/// </para>
/// <para>
/// The 6-field-parse assertion is additionally covered by <see cref="Fact_SixFieldExpression_IsAccepted"/>
/// which calls into the parser directly — per COMPASS msg 1360 recommendation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddCronTimerTransportMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<ConformancePinTimer>
{
	/// <summary>Canonical S790 sample expression — every 10 seconds (6-field).</summary>
	private const string SixFieldExpression = "*/10 * * * * *";

	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddCronTimerTransport<ConformancePinTimer>(SixFieldExpression);

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(ICronScheduler),
	};

	/// <inheritdoc />
	/// <remarks>
	/// AddTransportAdapterLifecycle (hosted service + transport registry plumbing) resolves
	/// services the conformance kit does not supply. Disable <c>ValidateOnBuild</c> — the
	/// pin asserts registration-surface hygiene for FIX 6's cron auto-detect, not end-to-end
	/// transport activation.
	/// </remarks>
	protected override bool ValidateOnBuild => false;

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton<ICronScheduler>(A.Fake<ICronScheduler>());

	/// <inheritdoc />
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		var scheduler = provider.GetRequiredService<ICronScheduler>();
		scheduler.GetType().Name.ShouldNotBe(nameof(CronScheduler),
			"Consumer-supplied ICronScheduler must survive TryAdd in FIX 6.");
	}

	/// <summary>Bucket A isolation gate — 6-field cron expression is accepted.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>
	/// Bucket A idempotence gate — <b>inversion-assertion expected-failure</b> per the
	/// four-way convergence ruling (COMPASS msg 1378 + SENTINEL msg 1379): the
	/// <c>TransportRegistry</c> throws <see cref="InvalidOperationException"/> on the
	/// second call by design to guard against duplicate factory names.
	/// </summary>
	/// <remarks>
	/// The registry-guard is intentional and not a framework defect — see fourth harness
	/// finding in msg 1371. The pin asserts the specific guard throw so a future change to
	/// the registry semantics (e.g., silent no-op on duplicate name) is caught by CI.
	/// </remarks>
	[Fact]
	public void Gate_Idempotence_ExpectedFailure_RegistryGuard()
	{
		var ex = Assert.Throws<InvalidOperationException>(ExecuteIdempotenceGate);
		ex.Message.ShouldContain("already registered",
			Case.Insensitive,
			"Registry-guard throw must retain its advisory message so consumers can diagnose double-registration.");
	}

	/// <summary>Bucket A override gate — consumer-registered scheduler survives.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();

	/// <summary>
	/// Directly exercises <see cref="TimeZoneAwareCronExpression"/>'s 6-field auto-detect
	/// (constructor whitespace-token scan). Added per COMPASS msg 1360 for parser-level
	/// regression coverage independent of the transport pipeline.
	/// </summary>
	[Fact]
	public void Fact_SixFieldExpression_IsAccepted()
	{
		var expr = new TimeZoneAwareCronExpression(SixFieldExpression, TimeZoneInfo.Utc);
		var next = expr.GetNextOccurrence(DateTimeOffset.UtcNow);
		next.ShouldNotBeNull("FIX 6 requires the 6-field cron expression to produce next-occurrence calculations.");
	}

	/// <summary>
	/// Regression pin for S791 C2 (<c>cb2a403ee</c>, <c>bd-61s6mw</c>):
	/// <see cref="TimeZoneAwareCronExpression.GetNextOccurrence(DateTimeOffset)"/> must
	/// handle a <see cref="DateTimeOffset"/> whose underlying <see cref="DateTime.Kind"/> is
	/// not <see cref="DateTimeKind.Utc"/> without throwing <see cref="ArgumentException"/>.
	/// Pre-fix, the impl passed <c>DateTimeOffset.DateTime</c> (Kind=Unspecified) into
	/// Cronos's <c>(DateTime, TimeZoneInfo)</c> overload — which requires Kind=Utc —
	/// producing noisy log errors on the TransportBindings smoke and a retry recovery.
	/// </summary>
	[Fact]
	public void Fact_GetNextOccurrence_AcceptsNonUtcDateTimeOffsetWithoutThrowing()
	{
		var expr = new TimeZoneAwareCronExpression(SixFieldExpression, TimeZoneInfo.Utc);

		// Deliberately construct a DateTimeOffset backed by a DateTime whose Kind is
		// NOT Utc — matches the S790 smoke condition that triggered the defect.
		var localDt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Local);
		var dtoFromLocal = new DateTimeOffset(localDt);

		Should.NotThrow(() => expr.GetNextOccurrence(dtoFromLocal),
			"C2 fix: GetNextOccurrence must accept any DateTimeOffset without throwing ArgumentException.");

		// Also confirm the overload for between-occurrences stays safe.
		var end = dtoFromLocal.AddHours(1);
		Should.NotThrow(() => expr.GetOccurrencesBetween(dtoFromLocal, end).ToList(),
			"C2 fix: GetOccurrencesBetween must accept any DateTimeOffset without throwing ArgumentException.");
	}
}
