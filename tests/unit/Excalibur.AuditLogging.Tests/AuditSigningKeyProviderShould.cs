// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// bd-qa71t5 (S856, SECURITY — audit-trail integrity) restored unit-level fail-closed lock for the shared
/// default <see cref="IAuditSigningKeyProvider"/> (<c>OptionsAuditSigningKeyProvider</c>, keyed from
/// <see cref="AuditIntegrityOptions"/>). PM-assigned single-actor (Platform-authored, Tests-reviewed,
/// 17647); SA ruled it required (17644).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> the qa71t5 one-key-config fold deleted the ES-local
/// <c>AuditSigningKeyProviderShould</c> (which tested the now-removed ES-local provider keyed from
/// <c>AuditOptions</c>). That deletion dropped the only <i>deterministic, non-Docker</i> unit coverage of
/// fail-closed-on-missing-key — a pure in-process security failure-path. The Docker-gated real-ES
/// <c>AuditIntegrityShould</c> lock is valuable additive end-to-end coverage but is skipped in any
/// no-Docker CI run, so it does not substitute for this unit lock (<c>testing-patterns</c>: cover failure
/// paths deterministically; <c>issue-accountability</c>: a dropped test is restored, not reclassified).
/// </para>
/// <para>
/// <b>Scope (non-duplicative):</b> this binds the <i>provider</i> contract (<see cref="IAuditSigningKeyProvider"/>
/// resolved via the public <c>AddAuditIntegrity()</c> DI path — no production test-factory, no IVT). The
/// sibling <c>AuditIntegrityShould</c> binds the <i>strategy</i> paths (<c>ComputeTagAsync</c>/<c>VerifyAsync</c>).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> a fabricate-on-miss provider (the defect class — returning a default/substitute key
/// instead of failing closed) would fail every negative assertion here; the positive case guards against
/// over-blocking.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "AuditIntegrity")]
public sealed class AuditSigningKeyProviderShould
{
	[Fact]
	public async Task FailClosedOnWrite_WhenNoSigningKeyConfigured()
	{
		// Arrange — integrity in use but no signing key configured.
		var provider = BuildProvider(key: null);

		// Act / Assert — the write path throws rather than emitting an unprotected/fabricated tag.
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await provider.GetCurrentSigningKeyAsync(CancellationToken.None));
		exception.Message.ShouldContain(nameof(AuditIntegrityOptions.SigningKey));
	}

	[Fact]
	public async Task FailClosedOnVerify_ReturnNull_WhenNoSigningKeyConfigured()
	{
		// Arrange — no key configured.
		var provider = BuildProvider(key: null);

		// Act — verify path for any keyId.
		var key = await provider.GetSigningKeyAsync("default", CancellationToken.None);

		// Assert — null → verification fails closed (record treated as unverifiable, never "valid").
		key.ShouldBeNull();
	}

	[Fact]
	public async Task FailClosedOnVerify_ReturnNull_ForAnUnknownKeyId()
	{
		// Arrange — a key configured under "k1".
		var provider = BuildProvider(key: RandomNumberGenerator.GetBytes(32), keyId: "k1");

		// Act — ask for a different keyId than the one configured.
		var key = await provider.GetSigningKeyAsync("some-other-key", CancellationToken.None);

		// Assert — unknown keyId → null (fail closed), never a substitute key.
		key.ShouldBeNull();
	}

	[Fact]
	public async Task ProvideTheConfiguredKeyAndId_ForWriteAndMatchingVerify()
	{
		// Arrange — a configured key + id (no over-blocking: the happy path must work).
		var secret = RandomNumberGenerator.GetBytes(32);
		var provider = BuildProvider(key: secret, keyId: "rotation-key-1");

		// Act
		var (keyId, writeKey) = await provider.GetCurrentSigningKeyAsync(CancellationToken.None);
		var verifyKey = await provider.GetSigningKeyAsync("rotation-key-1", CancellationToken.None);

		// Assert — the embedded keyId enables rotation; write and verify resolve the same secret.
		keyId.ShouldBe("rotation-key-1");
		writeKey.ShouldBe(secret);
		verifyKey.ShouldBe(secret);
	}

	// Resolves the shared default IAuditSigningKeyProvider via the public AddAuditIntegrity() DI path,
	// keyed from AuditIntegrityOptions (init-only SigningKey ⇒ object-initializer via Options.Create).
	private static IAuditSigningKeyProvider BuildProvider(byte[]? key, string keyId = "default")
	{
		var services = new ServiceCollection();
		services.AddSingleton(MsOptions.Create(new AuditIntegrityOptions { SigningKey = key, KeyId = keyId }));
		_ = services.AddAuditIntegrity();
		return services.BuildServiceProvider().GetRequiredService<IAuditSigningKeyProvider>();
	}
}
