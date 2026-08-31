// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Data.Persistence;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the provider-name arms in <see cref="PersistenceProviderConformanceTestKit"/> actually
/// BIND -- that each goes RED against a provider carrying the defect it names, and GREEN against one that
/// does not.
/// </summary>
/// <remarks>
/// <para>
/// These arms previously could not fail. The kit declared the expected name as an abstract member, so each
/// deriving suite supplied both the implementation AND the answer it would be checked against, and the arm
/// compared an implementation to a claim made about it in the same file. Any value passed. A suite that
/// every provider passes demonstrates substitutability only if the thing they all pass is the same thing,
/// and it was not: some suites named an engine, some named a test constant, and the kit had been bent
/// until it agreed with all of them.
/// </para>
/// <para>
/// The kit now owns the name and hands it to the deriver's factory, so the arms are a real substitution
/// check. That change cannot be proven non-vacuous against the real providers, because they were corrected
/// to honour their configured name in the same window -- a GREEN there is now consistent with the arms
/// binding and with their being vacuous. So the proof is here, against fakes whose single defect is fixed
/// by construction.
/// </para>
/// <para>
/// The fakes implement <see cref="IPersistenceProvider"/> DIRECTLY, inheriting no first-party base, so the
/// arms bind the interface's own requirement rather than re-testing an inherited convenience.
/// </para>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                       ExpectedName   RoundTrip   Stability
/// Honest                GREEN          GREEN       GREEN
/// IgnoresConfiguredName RED            RED         green
/// ReportsItsEngine      RED            RED         green
/// ForgetsAfterDisposal  GREEN          GREEN       RED
/// ThrowsAfterDisposal   GREEN          GREEN       RED
/// </code>
/// <para>
/// The lower-case greens are load-bearing in both directions. A provider returning a constant is perfectly
/// STABLE -- the stability arm cannot see it. A provider that round-trips every configured name can still
/// lose that name on disposal -- the round-trip arm cannot see that. Neither arm detects the other's
/// defect, which is why both are mandatory.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceProviderNameArmsBindShould
{
	#region Configured-name equality

	/// <summary>DETECTION: the equality arm must FAIL against a provider that ignores its configured name.</summary>
	[Fact]
	public void Red_ExpectedName_WhenTheProviderIgnoresItsConfiguredName()
	{
		var probe = new ArmProbe(FakeMode.IgnoresConfiguredName);

		var thrown = Should.Throw<TestFixtureAssertionException>(probe.RunExpectedNameArm);

		thrown.Message.ShouldContain(
			"the instance name this kit configured",
			Case.Sensitive,
			"the arm must fail on the configured-name contract, not some incidental error");
	}

	/// <summary>
	/// DETECTION: the same arm must FAIL against a provider reporting its ENGINE.
	/// </summary>
	/// <remarks>
	/// This is the confusion the sentinel exists to separate. Name is the identity of a configured
	/// instance -- the key it was registered under -- and never the engine behind it; the engine is what
	/// ProviderType reports. A sentinel that could coincide with an engine name would let this pass.
	/// </remarks>
	[Fact]
	public void Red_ExpectedName_WhenTheProviderReportsItsEngineInstead()
	{
		var probe = new ArmProbe(FakeMode.ReportsItsEngine);

		var thrown = Should.Throw<TestFixtureAssertionException>(probe.RunExpectedNameArm);

		thrown.Message.ShouldContain("not the database engine", Case.Sensitive);
	}

	/// <summary>LIVENESS: the same arm must PASS against a provider that honours its configured name.</summary>
	[Fact]
	public void Green_ExpectedName_WhenTheProviderHonoursItsConfiguredName() =>
		new ArmProbe(FakeMode.Honest).RunExpectedNameArm();

	#endregion

	#region Round-trip

	/// <summary>
	/// DETECTION: the round-trip arm must FAIL against a provider returning a constant.
	/// </summary>
	/// <remarks>
	/// The constant here is the kit's own sentinel, so this provider PASSES the equality arm above. That
	/// is the entire reason the round-trip arm exists: an implementation that hardcodes whatever value the
	/// kit happens to check for is indistinguishable, to the equality arm, from one that honours
	/// configuration.
	/// </remarks>
	[Fact]
	public void Red_RoundTrip_WhenTheProviderReturnsAConstant()
	{
		var probe = new ArmProbe(FakeMode.ConstantEqualToTheSentinel);

		// The equality arm is satisfied -- the constant happens to be what it checks for.
		probe.RunExpectedNameArm();

		var thrown = Should.Throw<TestFixtureAssertionException>(probe.RunRoundTripArm);

		thrown.Message.ShouldContain("Name must round-trip", Case.Sensitive);
	}

	/// <summary>
	/// DETECTION: two instances configured differently must not report the same name.
	/// </summary>
	[Fact]
	public void Red_RoundTrip_ReportsTwoInstancesCollapsingIntoOne()
	{
		var probe = new ArmProbe(FakeMode.ReportsItsEngine);

		var thrown = Should.Throw<TestFixtureAssertionException>(probe.RunRoundTripArm);

		thrown.Message.ShouldContain("Name must round-trip", Case.Sensitive);
	}

	/// <summary>LIVENESS: the round-trip arm must PASS against an honest provider.</summary>
	[Fact]
	public void Green_RoundTrip_WhenTheProviderHonoursEveryConfiguredName() =>
		new ArmProbe(FakeMode.Honest).RunRoundTripArm();

	/// <summary>
	/// Pins the round-trip arm's blind spot: a provider that round-trips every name can still lose it on
	/// disposal, and this arm cannot see that. The stability arm below is what does.
	/// </summary>
	[Fact]
	public void Green_RoundTrip_EvenWhenTheProviderForgetsItsNameAfterDisposal() =>
		new ArmProbe(FakeMode.ForgetsAfterDisposal).RunRoundTripArm();

	#endregion

	#region Lifecycle stability

	/// <summary>DETECTION: the stability arm must FAIL when the name changes after disposal.</summary>
	[Fact]
	public async Task Red_Stability_WhenTheProviderForgetsItsNameAfterDisposal()
	{
		var probe = new ArmProbe(FakeMode.ForgetsAfterDisposal);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunStabilityArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("changed across the provider's lifetime", Case.Sensitive);
	}

	/// <summary>
	/// DETECTION: the stability arm must FAIL when reading the name after disposal throws.
	/// </summary>
	/// <remarks>
	/// Name is total. Teardown diagnostics read it precisely when the provider is no longer usable, so a
	/// provider that throws there cannot be identified in the failure that needed identifying.
	/// </remarks>
	[Fact]
	public async Task Red_Stability_WhenReadingTheNameAfterDisposalThrows()
	{
		var probe = new ArmProbe(FakeMode.ThrowsAfterDisposal);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunStabilityArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("never throws", Case.Sensitive);
	}

	/// <summary>LIVENESS: the stability arm must PASS against an honest provider.</summary>
	[Fact]
	public Task Green_Stability_WhenTheNameSurvivesDisposal() =>
		new ArmProbe(FakeMode.Honest).RunStabilityArmAsync();

	/// <summary>
	/// Pins the stability arm's blind spot: a constant name is perfectly stable, so this arm passes a
	/// provider that ignores configuration entirely. The round-trip arm above is what catches that.
	/// </summary>
	[Fact]
	public Task Green_Stability_EvenWhenTheProviderIgnoresItsConfiguredName() =>
		new ArmProbe(FakeMode.ConstantEqualToTheSentinel).RunStabilityArmAsync();

	#endregion

	#region Harness

	/// <summary>The single decision each fake varies.</summary>
	private enum FakeMode
	{
		/// <summary>Reports the name it was configured with, before and after disposal. Conformant.</summary>
		Honest,

		/// <summary>Ignores the configured name and reports a fixed value of its own.</summary>
		IgnoresConfiguredName,

		/// <summary>Reports its database engine rather than the instance identity.</summary>
		ReportsItsEngine,

		/// <summary>Ignores configuration and returns exactly what the equality arm checks for.</summary>
		ConstantEqualToTheSentinel,

		/// <summary>Honours configuration, then loses the name once disposed.</summary>
		ForgetsAfterDisposal,

		/// <summary>Honours configuration, then throws when the name is read after disposal.</summary>
		ThrowsAfterDisposal,
	}

	/// <summary>
	/// Drives the real kit arms against a supplied fake. Subclassing is the only way in: calling the arms
	/// THROUGH the kit is the point -- a reimplemented copy would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(FakeMode mode) : PersistenceProviderConformanceTestKit
	{
		protected override string ExpectedProviderType => "test";

		protected override IPersistenceProvider CreateProvider(string providerName) =>
			new FakeProvider(mode, providerName);

		public void RunExpectedNameArm() => Provider_ShouldHaveExpectedName();

		public void RunRoundTripArm() => Provider_NameShouldRoundTripEveryConfiguredName();

		public Task RunStabilityArmAsync() => Provider_NameShouldBeStableAcrossLifecycle();
	}

	/// <summary>A minimal provider whose naming behaviour is fixed by construction.</summary>
	private sealed class FakeProvider(FakeMode mode, string configuredName) : IPersistenceProvider
	{
		// The sentinel the kit configures for non-naming arms. Duplicated deliberately: the kit keeps its
		// sentinel private, and a fake that could read it would not be modelling an outside implementation.
		private const string KitSentinel = "conformance-instance";

		private bool _disposed;

		/// <summary>THE ONE EXPRESSION UNDER EXPERIMENT.</summary>
		public string Name => mode switch
		{
			FakeMode.IgnoresConfiguredName => "a-name-of-my-own",
			FakeMode.ReportsItsEngine => "postgres",
			FakeMode.ConstantEqualToTheSentinel => KitSentinel,
			FakeMode.ForgetsAfterDisposal => _disposed ? "disposed" : configuredName,
			FakeMode.ThrowsAfterDisposal => _disposed
				? throw new ObjectDisposedException(nameof(FakeProvider))
				: configuredName,
			_ => configuredName,
		};

		public string ProviderType => "test";

		public Task<TResult> ExecuteAsync<TConnection, TResult>(
			IDataRequest<TConnection, TResult> request,
			CancellationToken cancellationToken)
			where TConnection : IDisposable => throw new NotSupportedException("not exercised here");

		public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public object? GetService(Type serviceType) => null;

		public void Dispose() => _disposed = true;

		public ValueTask DisposeAsync()
		{
			_disposed = true;

			return ValueTask.CompletedTask;
		}
	}

	#endregion
}
