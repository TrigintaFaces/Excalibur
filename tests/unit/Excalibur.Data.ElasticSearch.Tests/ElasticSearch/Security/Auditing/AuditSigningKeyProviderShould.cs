// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// Regression lock for <c>bd-ptaqsv</c> (Sprint 847, Lane E — audit-log integrity): the audit signing-key
/// provider MUST fail closed when no key is available — never fabricate or substitute key material — so a
/// keyed-MAC integrity tag can never silently degrade to an unprotected/forgeable value.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (author ≠ impl). CSO-reviewed lane (@REVIEW_ARCH).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the keyed-MAC seam (<see cref="IAuditSigningKeyProvider"/>) did not exist pre-fix
/// (audit integrity used an <i>unkeyed</i> <c>SHA-256</c>), so a true-pre-fix-parent RED cannot compile.
/// These assertions instead bind the property that is <i>inexpressible</i> under the pre-fix scheme: a
/// key provider that <b>fails closed</b> (throws on the write path / returns <see langword="null"/> on the
/// verify path) when no key is configured, rather than returning a fabricated or default key. A
/// fabricate-on-miss provider (the defect class) would fail every assertion here. The end-to-end
/// write→tamper→verify round-trip over a real keyed HMAC is the integration lock (Elasticsearch
/// TestContainers, TEST/CRUCIBLE).
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class AuditSigningKeyProviderShould
{
	[Fact]
	public async Task FailClosedOnWrite_WhenNoSigningKeyConfigured()
	{
		// Arrange — integrity enabled (default) but no signing key configured.
		var provider = new OptionsAuditSigningKeyProvider(Options.Create(new AuditOptions()));

		// Act / Assert — the write path throws rather than emitting an unprotected/fabricated tag.
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await provider.GetCurrentSigningKeyAsync(CancellationToken.None));
		exception.Message.ShouldContain(nameof(AuditOptions.IntegritySigningKey));
	}

	[Fact]
	public async Task FailClosedOnVerify_ReturnNull_WhenNoSigningKeyConfigured()
	{
		// Arrange — no key configured.
		var provider = new OptionsAuditSigningKeyProvider(Options.Create(new AuditOptions()));

		// Act — verify path for any keyId.
		var key = await provider.GetSigningKeyAsync("default", CancellationToken.None);

		// Assert — null → verification fails closed (record treated as unverifiable, never "valid").
		key.ShouldBeNull();
	}

	[Fact]
	public async Task FailClosedOnVerify_ReturnNull_ForAnUnknownKeyId()
	{
		// Arrange — a key configured under "k1".
		var provider = new OptionsAuditSigningKeyProvider(
			Options.Create(new AuditOptions { IntegritySigningKey = RandomNumberGenerator.GetBytes(32), IntegrityKeyId = "k1" }));

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
		var provider = new OptionsAuditSigningKeyProvider(
			Options.Create(new AuditOptions { IntegritySigningKey = secret, IntegrityKeyId = "rotation-key-1" }));

		// Act
		var (keyId, writeKey) = await provider.GetCurrentSigningKeyAsync(CancellationToken.None);
		var verifyKey = await provider.GetSigningKeyAsync("rotation-key-1", CancellationToken.None);

		// Assert — the embedded keyId enables rotation; write and verify resolve the same secret.
		keyId.ShouldBe("rotation-key-1");
		writeKey.ShouldBe(secret);
		verifyKey.ShouldBe(secret);
	}
}
