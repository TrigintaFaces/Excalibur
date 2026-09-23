// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// A change-feed subscription and a claim-based drain are alternatives, not layers. A host that
/// registers both publishes some messages twice and raises nothing, so the composition is refused at
/// host start.
/// </summary>
/// <remarks>
/// <para>
/// The duplicate needs no crash, no pause and no clock skew. The feed delivers a message and its
/// handler begins publishing; the drain claims the same message successfully, because the handler
/// never claimed it and the claim's exclusion set therefore says nothing about it; both publish. The
/// atomic claim is working correctly throughout — this is not a claim defect, it is a defect in
/// composing two mechanisms that were each designed to be the only one.
/// </para>
/// <para>
/// <b>Safety and liveness are both here deliberately, and neither alone is sufficient.</b> A guard
/// that refuses every host would satisfy the safety arm perfectly while breaking every consumer, and
/// nothing in a green safety arm would reveal it. The two liveness arms are what make the refusal
/// mean <em>this specific pair</em> rather than <em>something was registered</em>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxRefusesTwoDeliveryMechanismsShould
{
	[Fact]
	public void Refuse_WhenAChangeFeedSubscriptionAndAClaimBasedDrainAreBothRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseInMemory().EnableBackgroundProcessing()));
		services.AddSingleton(A.Fake<IChangeFeedSubscription<CloudOutboxMessage>>());

		using var provider = services.BuildServiceProvider(validateScopes: false);

		var thrown = Should.Throw<InvalidOperationException>(() => Validator(provider).Validate());

		thrown.Message.ShouldContain(
			"IChangeFeedSubscription",
			Case.Sensitive,
			"the refusal must name the change-feed registration — a consumer who registered two things "
			+ "cannot act on a message that does not say which two");

		thrown.Message.ShouldContain(
			"OutboxBackgroundService",
			Case.Sensitive,
			"and it must name the drain registration for the same reason");
	}

	[Fact]
	public void Start_WhenOnlyTheClaimBasedDrainIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseInMemory().EnableBackgroundProcessing()));

		using var provider = services.BuildServiceProvider(validateScopes: false);

		Should.NotThrow(
			() => Validator(provider).Validate(),
			"the drain alone is a supported and complete configuration. If this goes RED the guard is "
			+ "refusing on the presence of the drain rather than on the presence of BOTH mechanisms");
	}

	[Fact]
	public void Start_WhenOnlyTheChangeFeedSubscriptionIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseInMemory()));
		services.AddSingleton(A.Fake<IChangeFeedSubscription<CloudOutboxMessage>>());

		using var provider = services.BuildServiceProvider(validateScopes: false);

		Should.NotThrow(
			() => Validator(provider).Validate(),
			"the subscription alone is a supported and complete configuration. If this goes RED the "
			+ "guard is refusing on the presence of the subscription rather than on the presence of BOTH");
	}

	[Fact]
	public void Start_WhenNeitherMechanismIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseInMemory()));

		using var provider = services.BuildServiceProvider(validateScopes: false);

		Should.NotThrow(
			() => Validator(provider).Validate(),
			"a host that drains manually through IOutboxProcessor registers neither mechanism, and this "
			+ "guard has nothing to say about it");
	}

	/// <remarks>
	/// Constructed directly rather than resolved out of <c>GetServices&lt;IHostedService&gt;()</c>, and the
	/// reason is the same one that shaped the guard itself: resolving that sequence <em>constructs every
	/// hosted service in the host</em>. Here that means <c>OutboxBackgroundService</c>, which demands an
	/// <c>IOutboxPublisher</c> these minimal hosts do not register — so the harness threw a
	/// dependency-resolution error before the validator ever ran, and the arms failed for a reason that had
	/// nothing to do with what they assert. That these hosts have no publisher is correct: the question
	/// under test is which delivery mechanisms were <em>registered</em>, which is settled before anything is
	/// ever published.
	/// <para>
	/// That the validator is wired as an <see cref="IHostedService"/> is asserted by
	/// <c>OutboxPrerequisiteValidatorShould</c>; it is not re-proved here.
	/// </para>
	/// </remarks>
	private static OutboxPrerequisiteValidator Validator(IServiceProvider provider) => new(provider);
}
