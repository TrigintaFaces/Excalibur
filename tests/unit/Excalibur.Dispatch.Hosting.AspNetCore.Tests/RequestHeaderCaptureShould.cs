// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Covers the configuration contract for request-header capture: that a credential-bearing header is
/// refused at startup, that a cap which could never admit an entry is refused too, and that an ordinary
/// allowlist still starts.
/// </summary>
/// <remarks>
/// The refusal arms are paired with a liveness arm on purpose. A validator that rejected every
/// configuration would satisfy every safety assertion here and make the feature unusable, and nothing in
/// the safety arms alone could tell the difference.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class RequestHeaderCaptureShould
{
	[Theory]
	[InlineData("Authorization")]
	[InlineData("Cookie")]
	[InlineData("Proxy-Authorization")]
	public void RefuseACredentialHeaderAtStartup(string credentialHeader)
	{
		// Startup is the last point at which this is actionable. Skipping the header at runtime instead
		// would leave a configuration that reads as though it were in effect, and nothing would say
		// otherwise on any message.
		var exception = Should.Throw<OptionsValidationException>(
			() => Validate(o => o.AllowedHeaders.Add(credentialHeader)));

		exception.Message.ShouldContain(credentialHeader);
	}

	[Theory]
	[InlineData("authorization")]
	[InlineData("COOKIE")]
	public void RefuseACredentialHeaderWhateverItsCasing(string credentialHeader) =>
		// HTTP field names are case-insensitive by specification, so a case-sensitive refusal would be
		// bypassed by the same header spelled differently -- and the allowlist would still match it.
		_ = Should.Throw<OptionsValidationException>(
			() => Validate(o => o.AllowedHeaders.Add(credentialHeader)));

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void RefuseAnEntryCapThatCouldNeverAdmitAHeader(int maxEntries) =>
		// Quiet-direction misconfiguration: it drops everything while the allowlist still reads as
		// though names were permitted, which at runtime is indistinguishable from an empty allowlist.
		_ = Should.Throw<OptionsValidationException>(() => Validate(o =>
		{
			_ = o.AllowedHeaders.Add("X-Request-Id");
			o.MaxEntries = maxEntries;
		}));

	[Fact]
	public void RefuseABlankHeaderName() =>
		_ = Should.Throw<OptionsValidationException>(
			() => Validate(o => o.AllowedHeaders.Add("   ")));

	[Fact]
	public void StartWithAnOrdinaryAllowlist()
	{
		// The liveness half. Without it every arm above is satisfied by a validator that refuses
		// everything, and the feature would be unusable behind a passing suite.
		var options = Validate(o => _ = o.AllowedHeaders.Add("X-Request-Id"));

		options.AllowedHeaders.ShouldContain("X-Request-Id");
		options.MaxEntries.ShouldBe(8);
		options.MaxValueLength.ShouldBe(256);
		options.MaxTotalLength.ShouldBe(1024);
	}

	[Fact]
	public void StartWithNoConfigurationAtAll()
	{
		// Registering without configuring must remain legal and must capture nothing, because that is the
		// state every consumer who has not thought about headers is in.
		var options = Validate(configure: null);

		options.AllowedHeaders.ShouldBeEmpty();
	}

	/// <summary>
	/// Builds the real options pipeline and forces validation, the way <c>ValidateOnStart</c> does.
	/// </summary>
	private static RequestHeaderCaptureOptions Validate(
		Action<RequestHeaderCaptureOptions>? configure)
	{
		var services = new ServiceCollection();
		_ = services.AddDispatchRequestHeaderCapture(configure);

		using var provider = services.BuildServiceProvider();

		// Resolving the start-up validators and running them is what ValidateOnStart does at host start;
		// going through the registered IValidateOptions rather than calling the validator directly is
		// what makes this arm bind the REGISTRATION as well as the validation logic.
		foreach (var validator in provider.GetServices<IStartupValidator>())
		{
			validator.Validate();
		}

		return provider.GetRequiredService<IOptions<RequestHeaderCaptureOptions>>().Value;
	}
}
