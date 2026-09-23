// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Security;
using Excalibur.Data.ElasticSearch.Security.Auditing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// rboryq — <see cref="AuditSigningKeyStartupProbe"/> expressed the Microsoft-first way
/// (<see cref="IValidateOptions{TOptions}"/> + <c>ValidateOnStart()</c>). When audit-log integrity is
/// required, the host MUST fail fast at startup if the configured <see cref="IAuditSigningKeyProvider"/>
/// cannot produce a signing key. When integrity is not required, the probe is a no-op (opt-in complexity).
/// </summary>
/// <remarks>
/// <b>RED mutants:</b> drop the empty/null-key check ⇒ the empty-key case passes (RED); drop the
/// try/catch ⇒ a throwing provider escapes as the wrong exception type (RED); drop the
/// <c>EnsureLogIntegrity</c> short-circuit ⇒ the not-required case fails (RED).
/// <para>
/// The UNIT arms below call <see cref="IValidateOptions{TOptions}.Validate"/> directly against a fake
/// provider — cheap, and enough to bind the four failure/success shapes. The PRODUCTION-PATH arms build a
/// real <see cref="ServiceProvider"/> through <c>AddSecurityAuditing()</c>, the public registration entry
/// point, and resolve <see cref="IOptions{TOptions}"/> — proving the validator is actually reachable from
/// composition, not merely correct in isolation (the gap the prior hand-constructed lock left open).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "AuditIntegrity")]
public sealed class AuditSigningKeyStartupProbeShould
{
	// ---------- UNIT: Validate() against a fake provider ----------

	[Fact]
	public void FailFast_WhenIntegrityRequiredButProviderReturnsEmptyKey()
	{
		var probe = new AuditSigningKeyStartupProbe(KeyProviderReturning(("kid", [])));

		var result = probe.Validate(name: null, new AuditOptions { EnsureLogIntegrity = true });

		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailFast_WhenIntegrityRequiredButProviderThrows()
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS unreachable"));
		var probe = new AuditSigningKeyStartupProbe(provider);

		var result = probe.Validate(name: null, new AuditOptions { EnsureLogIntegrity = true });

		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Pass_WhenIntegrityRequiredAndProviderSuppliesAKey()
	{
		var probe = new AuditSigningKeyStartupProbe(KeyProviderReturning(("kid", [1, 2, 3, 4])));

		var result = probe.Validate(name: null, new AuditOptions { EnsureLogIntegrity = true });

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Pass_WhenIntegrityNotRequired_EvenIfProviderWouldFail()
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("would fail, but must never be probed"));
		var probe = new AuditSigningKeyStartupProbe(provider);

		var result = probe.Validate(name: null, new AuditOptions { EnsureLogIntegrity = false });

		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	// ---------- PRODUCTION-PATH: real ServiceProvider through AddSecurityAuditing() ----------
	//
	// AuditOptions.EnsureLogIntegrity and AuditIntegrityOptions.SigningKey are both init-only, so they
	// cannot be set via services.Configure<T>(Action<T>) (which mutates an already-constructed instance).
	// Bound from an in-memory IConfiguration instead -- the ConfigurationBinder sets init accessors fine.

	private static IConfiguration ConfigOf(params (string Key, string? Value)[] entries) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
			.Build();

	[Fact]
	public void FailClosed_ThroughAddSecurityAuditing_WhenIntegrityRequiredWithNoUsableSigningKey()
	{
		// The default OptionsAuditSigningKeyProvider fails closed with no key configured -- exactly the
		// misconfiguration this probe exists to surface at start, rather than on the first audit write.
		// EnsureLogIntegrity defaults to true, so no explicit configuration is even needed here.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSecurityAuditing();

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<AuditOptions>>().Value);
		ex.Message.ShouldContain(nameof(AuditOptions.EnsureLogIntegrity));
	}

	[Fact]
	public async Task StartAndSupplyAKey_ThroughAddSecurityAuditing_WhenAWorkingProviderIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSecurityAuditing();
		_ = services.Configure<AuditIntegrityOptions>(
			ConfigOf(("SigningKey", Convert.ToBase64String(new byte[32]))));

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IOptions<AuditOptions>>().Value,
			"a signing key is configured, so the default OptionsAuditSigningKeyProvider can supply one");

		// Liveness beyond the verdict: the provider the probe checked is the one the auditor actually uses.
		var (keyId, key) = await provider.GetRequiredService<IAuditSigningKeyProvider>()
			.GetCurrentSigningKeyAsync(TestContext.Current.CancellationToken);
		key.ShouldNotBeEmpty();
		keyId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void StartWithNoSigningKeyProviderAtAll_ThroughAddSecurityAuditing_WhenIntegrityIsNotRequired()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSecurityAuditing();
		_ = services.Configure<AuditOptions>(ConfigOf(("EnsureLogIntegrity", "false")));

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IOptions<AuditOptions>>().Value,
			"EnsureLogIntegrity is explicitly false, so the probe never touches the signing-key provider");
	}

	private static IAuditSigningKeyProvider KeyProviderReturning((string KeyId, byte[] Key) result)
	{
		var provider = A.Fake<IAuditSigningKeyProvider>();
		_ = A.CallTo(() => provider.GetCurrentSigningKeyAsync(A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<(string, byte[])>(result));
		return provider;
	}
}
