// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.KeyManagement;

/// <summary>
/// Binds two guarantees about how key management is selected from configuration:
/// a host that enables field encryption without naming a key provider is refused rather than
/// silently given the in-memory development provider, and Azure Key Vault settings bind from the
/// same parent section the feature reads its own settings from.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class KeyManagementConfigurationBindingShould
{
	private const string KeyManagementSection = "Elasticsearch:Security:Encryption:KeyManagement";
	private const string FieldEncryptionKey = "Elasticsearch:Security:Encryption:FieldLevelEncryption";

	private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
			.Build();

	// SAFETY: field encryption is on and no provider is named, so the composition is refused.
	// Under the previous behaviour this returned the in-memory development provider, and the
	// consumer learned about it only when a restart made their encrypted field data unreadable.
	[Fact]
	public void RefuseAHostThatEnablesFieldEncryptionWithoutNamingAKeyProvider()
	{
		var services = new ServiceCollection();

		var ex = Should.Throw<InvalidOperationException>(() =>
			services.AddKeyManagement(Configuration((FieldEncryptionKey, "true"))));

		ex.Message.ShouldContain($"{KeyManagementSection}:Provider");
		services.ShouldNotContain(d => d.ServiceType == typeof(IKeyManagementProvider));
	}

	// SAFETY: a provider this package has no implementation for is refused too, rather than
	// resolving to the development provider through the switch's default arm. A consumer who asks
	// for hardware-backed keys must never be handed an in-memory dictionary instead.
	[Fact]
	public void RefuseAProviderThatIsNamedButNotImplemented()
	{
		var services = new ServiceCollection();

		var ex = Should.Throw<NotSupportedException>(() => services.AddKeyManagement(Configuration(
			(FieldEncryptionKey, "true"),
			($"{KeyManagementSection}:Provider", nameof(KeyManagementProvider.Hsm)))));

		ex.Message.ShouldContain(nameof(KeyManagementProvider.Hsm));
		services.ShouldNotContain(d => d.ServiceType == typeof(IKeyManagementProvider));
	}

	// LIVENESS: naming the development provider explicitly still yields a provider that WORKS.
	// Asserting only that the development provider is not selected would be satisfied by a
	// provider that refuses everything, so this rotates a key through the real resolved instance
	// rather than asserting a registration exists.
	[Fact]
	public async Task StillResolveAWorkingProviderWhenTheDevelopmentProviderIsNamed()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddKeyManagement(Configuration(
			(FieldEncryptionKey, "true"),
			($"{KeyManagementSection}:Provider", nameof(KeyManagementProvider.Local))));

		await using var sp = services.BuildServiceProvider();
		var keyProvider = sp.GetRequiredService<IKeyManagementProvider>();

		var rotation = await keyProvider.RotateKeyAsync(
			"field-key", EncryptionAlgorithm.Aes256Gcm, purpose: null, expiresAt: null,
			TestContext.Current.CancellationToken);

		rotation.Success.ShouldBeTrue();
		(await keyProvider.GetKeyAsync("field-key", TestContext.Current.CancellationToken)).ShouldNotBeNull();
	}

	// LIVENESS: the refusal is scoped to hosts that actually encrypt fields. AddElasticsearchSecurity
	// always calls AddKeyManagement, so a consumer using authentication, auditing or monitoring
	// without field encryption must keep composing exactly as before -- no key material is at risk
	// there. Without this arm the safety arms above would be satisfied by refusing every host.
	[Fact]
	public void StillComposeAHostThatDoesNotEnableFieldEncryption()
	{
		var services = new ServiceCollection();

		_ = services.AddKeyManagement(Configuration());

		services.ShouldContain(d => d.ServiceType == typeof(IKeyManagementProvider));
		services.ShouldContain(d => d.ServiceType == typeof(IEncryptionProviderRegistry));
	}

	// LIVENESS: Azure Key Vault CONNECTION-CREDENTIAL settings, nested under the parent section the
	// feature documents and reads, bind POPULATED. This is the connection-credential store
	// (AddAzureKeyVaultCredentialStorage), a distinct capability from field-encryption key management
	// (AddKeyManagement) -- see the 6zlc1i convergence.
	[Fact]
	public void BindAzureKeyVaultOptionsFromTheSectionTheFeatureReads()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureKeyVaultCredentialStorage(Configuration(
			($"{KeyManagementSection}:AzureKeyVault:VaultUri", "https://contoso.vault.azure.net/"),
			($"{KeyManagementSection}:AzureKeyVault:TenantId", "tenant-7")));

		using var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;

		options.VaultUri.ShouldBe("https://contoso.vault.azure.net/");
		options.TenantId.ShouldBe("tenant-7");
	}
}
