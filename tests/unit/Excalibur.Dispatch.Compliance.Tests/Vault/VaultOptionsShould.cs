// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Vault;

namespace Excalibur.Dispatch.Compliance.Tests.Vault;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultOptionsShould
{
	[Fact]
	public void VaultOptions_Defaults_AreExpected()
	{
		var options = new VaultOptions();

		options.TransitMountPath.ShouldBe("transit");
		options.KeyNamePrefix.ShouldBe("excalibur-dispatch-");
		options.MetadataCacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableDetailedTelemetry.ShouldBeFalse();
		options.Auth.ShouldNotBeNull();
		options.Keys.ShouldNotBeNull();
		options.Retry.ShouldNotBeNull();
	}

	[Fact]
	public void VaultAuthOptions_Defaults_AreExpected()
	{
		var options = new VaultAuthOptions();

		options.AuthMethod.ShouldBe(VaultAuthMethod.Token);
		options.AppRoleMountPath.ShouldBe("approle");
		options.KubernetesMountPath.ShouldBe("kubernetes");
		options.KubernetesJwtPath.ShouldContain("/var/run/secrets/kubernetes.io/serviceaccount/token");
	}

	[Fact]
	public void VaultKeyOptions_Defaults_AreExpected()
	{
		var options = new VaultKeyOptions();

		options.DefaultKeyType.ShouldBe("aes256-gcm96");
		options.AllowKeyExport.ShouldBeFalse();
		options.AllowPlaintextBackup.ShouldBeFalse();
		options.MinDecryptionVersion.ShouldBe(0);
		options.MinEncryptionVersion.ShouldBe(0);
		options.EnableConvergentEncryption.ShouldBeFalse();
		options.EnableKeyDerivation.ShouldBeFalse();
	}

	[Fact]
	public void VaultRetryOptions_Defaults_AreExpected()
	{
		var options = new VaultRetryOptions();

		options.EnableRetry.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Options_CanBeConfigured_ForAppRoleAndKubernetesAuth()
	{
		var options = new VaultOptions
		{
			VaultUri = new Uri("https://vault.example.com:8200"),
			Namespace = "enterprise",
			EnableDetailedTelemetry = true,
			Auth = new VaultAuthOptions
			{
				AuthMethod = VaultAuthMethod.AppRole,
				AppRoleId = "role-id",
				AppRoleSecretId = "secret-id",
				KubernetesRole = "dispatch-service",
			},
			Keys = new VaultKeyOptions
			{
				AllowKeyExport = true,
				EnableConvergentEncryption = true,
				EnableKeyDerivation = true,
			},
			Retry = new VaultRetryOptions
			{
				EnableRetry = false,
				MaxRetryAttempts = 8,
				RetryDelay = TimeSpan.FromSeconds(4),
			},
		};

		options.VaultUri.ShouldBe(new Uri("https://vault.example.com:8200"));
		options.Namespace.ShouldBe("enterprise");
		options.EnableDetailedTelemetry.ShouldBeTrue();
		options.Auth.AuthMethod.ShouldBe(VaultAuthMethod.AppRole);
		options.Auth.AppRoleId.ShouldBe("role-id");
		options.Keys.AllowKeyExport.ShouldBeTrue();
		options.Keys.EnableConvergentEncryption.ShouldBeTrue();
		options.Retry.EnableRetry.ShouldBeFalse();
		options.Retry.MaxRetryAttempts.ShouldBe(8);
	}
}
