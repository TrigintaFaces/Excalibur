// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Security;
using Excalibur.Data.ElasticSearch.Security.Auditing;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// vbv0at-A — author≠impl fail-closed lock (TestsDeveloper) for <see cref="AuditSigningKeyStartupProbe"/>.
/// When audit-log integrity is required, the host MUST fail fast at startup if the configured
/// <see cref="IAuditSigningKeyProvider"/> cannot produce a signing key — surfacing the misconfiguration
/// at boot rather than silently failing to protect integrity on the first audit write. When integrity is
/// not required, the probe is a no-op (opt-in complexity).
/// </summary>
/// <remarks>
/// <b>RED mutants:</b> drop the empty/null-key check ⇒ the empty-key case passes (RED); drop the
/// try/catch rethrow ⇒ a throwing provider is swallowed (RED); drop the <c>EnsureLogIntegrity</c>
/// short-circuit ⇒ the not-required case throws (RED).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "AuditIntegrity")]
public sealed class AuditSigningKeyStartupProbeShould
{
	[Fact]
	public async Task FailFast_WhenIntegrityRequiredButProviderReturnsEmptyKey()
	{
		var probe = CreateProbe(ensureIntegrity: true, KeyProviderReturning(("kid", [])));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await probe.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task FailFast_WhenIntegrityRequiredButProviderThrows()
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS unreachable"));
		var probe = CreateProbe(ensureIntegrity: true, provider);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await probe.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task Passes_WhenIntegrityRequiredAndProviderSuppliesAKey()
	{
		var probe = CreateProbe(ensureIntegrity: true, KeyProviderReturning(("kid", [1, 2, 3, 4])));

		// Should NOT throw — a real key is available at startup.
		await probe.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Passes_WhenIntegrityNotRequired_EvenIfProviderWouldFail()
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("would fail, but must never be probed"));
		var probe = CreateProbe(ensureIntegrity: false, provider);

		// EnsureLogIntegrity=false short-circuits before touching the provider — no throw.
		await probe.StartAsync(CancellationToken.None);
		A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	private static IAuditSigningKeyProvider KeyProviderReturning((string KeyId, byte[] Key) result)
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<(string, byte[])>(result));
		return provider;
	}

	private static AuditSigningKeyStartupProbe CreateProbe(bool ensureIntegrity, IAuditSigningKeyProvider provider) =>
		new(MsOptions.Create(new AuditOptions { EnsureLogIntegrity = ensureIntegrity }), provider);
}
