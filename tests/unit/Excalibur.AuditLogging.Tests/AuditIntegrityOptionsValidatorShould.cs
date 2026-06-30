// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Unit tests for the <c>AuditIntegrityOptions</c> startup validator (<c>ValidateOnStart</c>) registered
/// by <c>AddAuditIntegrity()</c>. Exercised through the public DI surface — the validator type itself is
/// internal — so these tests also confirm the validator is actually wired into the container.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AuditIntegrityOptionsValidatorShould
{
	/// <summary>32 bytes (256 bits) — the HMAC-SHA256 minimum key strength enforced by the validator.</summary>
	private const int MinimumSigningKeyLengthBytes = 32;

	private static IValidateOptions<AuditIntegrityOptions> Resolve() =>
		new ServiceCollection()
			.AddAuditIntegrity()
			.BuildServiceProvider()
			.GetRequiredService<IValidateOptions<AuditIntegrityOptions>>();

	[Fact]
	public void Be_Registered_By_AddAuditIntegrity()
	{
		// Resolution itself is the assertion: the public DI path must wire the startup validator.
		Resolve().ShouldNotBeNull();
	}

	[Fact]
	public void Succeed_For_Default_Options_With_No_SigningKey()
	{
		// KeyId defaults to "default"; SigningKey null is a valid (fail-closed) state.
		var result = Resolve().Validate(name: null, new AuditIntegrityOptions());

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Fail_When_KeyId_Is_Empty_Or_Whitespace(string keyId)
	{
		var result = Resolve().Validate(name: null, new AuditIntegrityOptions { KeyId = keyId });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AuditIntegrityOptions.KeyId));
	}

	[Fact]
	public void Fail_When_KeyId_Contains_Colon()
	{
		var result = Resolve().Validate(name: null, new AuditIntegrityOptions { KeyId = "key:1" });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(":");
	}

	[Fact]
	public void Succeed_When_SigningKey_Is_Null()
	{
		var result = Resolve().Validate(name: null, new AuditIntegrityOptions { KeyId = "k", SigningKey = null });

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(16)]
	[InlineData(MinimumSigningKeyLengthBytes - 1)]
	public void Fail_When_SigningKey_Is_Shorter_Than_Minimum(int length)
	{
		var options = new AuditIntegrityOptions { KeyId = "k", SigningKey = new byte[length] };

		var result = Resolve().Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AuditIntegrityOptions.SigningKey));
	}

	[Theory]
	[InlineData(MinimumSigningKeyLengthBytes)]
	[InlineData(MinimumSigningKeyLengthBytes + 1)]
	[InlineData(64)]
	public void Succeed_When_SigningKey_Meets_Minimum_Length(int length)
	{
		var options = new AuditIntegrityOptions { KeyId = "k", SigningKey = new byte[length] };

		var result = Resolve().Validate(name: null, options);

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}
}
