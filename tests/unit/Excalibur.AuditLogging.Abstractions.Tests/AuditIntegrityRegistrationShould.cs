// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Locks for <c>AddAuditIntegrity()</c> and the startup validation of <see cref="AuditIntegrityOptions"/>.
/// </summary>
/// <remarks>
/// The validator is <c>internal</c> but is registered against the public <see cref="IValidateOptions{T}"/>
/// contract, so these arms resolve it from a real container and call it through that interface — the same
/// path the options system uses at startup, with no production visibility widened to reach it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditIntegrityRegistrationShould
{
	private static IValidateOptions<AuditIntegrityOptions> Validator()
		=> new ServiceCollection()
			.AddAuditIntegrity()
			.BuildServiceProvider()
			.GetServices<IValidateOptions<AuditIntegrityOptions>>()
			.Single();

	[Fact]
	public void ResolveTheStrategyAndKeyProvider_FromARealContainer()
	{
		using var provider = new ServiceCollection().AddAuditIntegrity().BuildServiceProvider();

		provider.GetRequiredService<IAuditIntegrityStrategy>().ShouldNotBeNull();
		provider.GetRequiredService<IAuditSigningKeyProvider>().ShouldNotBeNull();
	}

	/// <summary>
	/// A KMS-backed key provider is the documented production choice, and the registration promises a prior
	/// registration wins. If that broke, a deployment would silently fall back to the options-backed default
	/// and its keys would come from configuration instead of the key manager.
	/// </summary>
	[Fact]
	public void PreserveAConsumerSuppliedKeyProvider()
	{
		var services = new ServiceCollection();
		var consumerProvider = new AuditIntegrityHarness.StubKeyProvider("kms-1", AuditIntegrityHarness.KeyA);

		_ = services.AddSingleton<IAuditSigningKeyProvider>(consumerProvider);
		_ = services.AddAuditIntegrity();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IAuditSigningKeyProvider>().ShouldBeSameAs(consumerProvider);
	}

	[Fact]
	public void RegisterExactlyOneValidator_WhenCalledTwice()
	{
		using var provider = new ServiceCollection()
			.AddAuditIntegrity()
			.AddAuditIntegrity()
			.BuildServiceProvider();

		provider.GetServices<IValidateOptions<AuditIntegrityOptions>>().Count().ShouldBe(1);
	}

	[Fact]
	public void Reject_ANullServiceCollection()
		=> Should.Throw<ArgumentNullException>(
			() => AuditIntegrityServiceCollectionExtensions.AddAuditIntegrity(null!));

	/// <summary>
	/// The liveness arm for the validator: the shipped defaults, with no key supplied, are a valid state.
	/// A null key is deliberately allowed — the provider fails closed if integrity is actually used — so
	/// requiring one here would break every deployment that does not enable integrity.
	/// </summary>
	[Fact]
	public void AcceptTheDefaultOptions_WithNoSigningKey()
	{
		var result = Validator().Validate(name: null, new AuditIntegrityOptions());

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	[Fact]
	public void AcceptASigningKeyOfFullHmacStrength()
	{
		var options = new AuditIntegrityOptions { SigningKey = AuditIntegrityHarness.KeyA, KeyId = "k1" };

		Validator().Validate(name: null, options).Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_AnEmptyKeyIdentifier(string keyId)
	{
		var result = Validator().Validate(name: null, new AuditIntegrityOptions { KeyId = keyId });

		result.Failed.ShouldBeTrue();
	}

	/// <summary>
	/// The tag is colon-delimited, so a colon in the key id produces a token that parses into the wrong
	/// fields and can never be verified. Caught at startup, where it is a configuration error rather than an
	/// audit trail that silently cannot be checked.
	/// </summary>
	[Fact]
	public void Reject_AColonBearingKeyIdentifier()
	{
		var result = Validator().Validate(name: null, new AuditIntegrityOptions { KeyId = "tenant:1" });

		result.Failed.ShouldBeTrue();
	}

	/// <summary>
	/// A short key is worse than no key: no key fails closed and is obvious, whereas a weak key produces
	/// tags that look exactly like strong ones. 31 bytes is the boundary — one below HMAC-SHA256 strength.
	/// </summary>
	[Fact]
	public void Reject_ASigningKeyWeakerThanHmacSha256()
	{
		var options = new AuditIntegrityOptions { SigningKey = new byte[31], KeyId = "k1" };

		var result = Validator().Validate(name: null, options);

		result.Failed.ShouldBeTrue();
	}

	/// <summary>
	/// The other side of that boundary. Without this arm, a validator that rejected every supplied key
	/// would pass the arm above and make the signing key impossible to configure at all.
	/// </summary>
	[Fact]
	public void Accept_ASigningKeyOfExactlyTheMinimumLength()
	{
		var options = new AuditIntegrityOptions { SigningKey = new byte[32], KeyId = "k1" };

		Validator().Validate(name: null, options).Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Reject_ANullOptionsInstance()
		=> Should.Throw<ArgumentNullException>(() => Validator().Validate(name: null, options: null!));
}
