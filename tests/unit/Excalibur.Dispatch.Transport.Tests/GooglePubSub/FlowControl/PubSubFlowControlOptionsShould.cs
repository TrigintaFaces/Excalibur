// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.FlowControl;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class PubSubFlowControlOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var options = new PubSubFlowControlOptions();

		options.MaxOutstandingElementCount.ShouldBe(1000);
		options.MaxOutstandingByteCount.ShouldBe(100_000_000);
	}

	[Fact]
	public void AcceptZero_BecauseItDefersToTheClientLibrary()
	{
		// The subscriber registration treats 0 as "leave the limit to the client library" and skips
		// applying FlowControlSettings entirely. A validator that rejected 0 would refuse a supported
		// configuration.
		var options = new PubSubFlowControlOptions { MaxOutstandingElementCount = 0, MaxOutstandingByteCount = 0 };

		Should.NotThrow(options.Validate);
	}

	[Theory]
	[InlineData(-1, 100)]
	[InlineData(100, -1)]
	public void RejectANegativeLimit(int elements, long bytes)
	{
		// Without this, a negative failed the registration's `> 0` test and silently became
		// "use the client library default" -- indistinguishable from 0, but not what was asked for.
		var options = new PubSubFlowControlOptions
		{
			MaxOutstandingElementCount = elements,
			MaxOutstandingByteCount = bytes,
		};

		_ = Should.Throw<ArgumentException>(options.Validate);
	}

	[Fact]
	public void BeValidatedByTheRegisteredParentValidator()
	{
		// The flow-control limits are nested inside GooglePubSubOptions, so nothing resolves an
		// IValidateOptions<PubSubFlowControlOptions> for them. This arm fails if the parent validator
		// stops delegating, which is the only thing that makes Validate() reachable at startup.
		var options = new global::Excalibur.Dispatch.Transport.Google.GooglePubSubOptions();
		options.Connection.ProjectId = "project";
		options.Connection.SubscriptionId = "subscription";
		options.Subscriber.FlowControl.MaxOutstandingElementCount = -1;

		var result = new GooglePubSubOptionsValidator().Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxOutstandingElementCount");
	}

	[Fact]
	public void PassTheParentValidator_WhenLimitsAreValid()
	{
		var options = new global::Excalibur.Dispatch.Transport.Google.GooglePubSubOptions();
		options.Connection.ProjectId = "project";
		options.Connection.SubscriptionId = "subscription";

		new GooglePubSubOptionsValidator().Validate(name: null, options).Succeeded.ShouldBeTrue();
	}
}
