// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Dispatch.Compliance.Vault;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Vault;

/// <summary>
/// Verifies the ISP split of VaultOptions into sub-options (Auth, Keys, Retry)
/// and confirms shim removal is complete.
/// Sprint 564 S564.52: VaultOptions ISP split verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultOptionsIspSplitShould
{
	#region Root Property Count (ISP Gate)

	[Fact]
	public void RootOptions_HaveAtMost10Properties()
	{
		// Arrange
		var properties = typeof(VaultOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// Assert - ISP gate: root <= 10 properties
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"VaultOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void AuthSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(VaultAuthOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"VaultAuthOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void KeysSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(VaultKeyOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"VaultKeyOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void RetrySubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(VaultRetryOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"VaultRetryOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	#endregion

	#region Sub-Options Initialized

	[Fact]
	public void HaveNonNullAuthSubOptions()
	{
		var options = new VaultOptions();
		options.Auth.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullKeysSubOptions()
	{
		var options = new VaultOptions();
		options.Keys.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullRetrySubOptions()
	{
		var options = new VaultOptions();
		options.Retry.ShouldNotBeNull();
	}

	#endregion

	#region Root Default Values

	[Fact]
	public void HaveDefaultTransitMountPath()
	{
		var options = new VaultOptions();
		options.TransitMountPath.ShouldBe("transit");
	}

	[Fact]
	public void HaveDefaultKeyNamePrefix()
	{
		var options = new VaultOptions();
		options.KeyNamePrefix.ShouldBe("excalibur-dispatch-");
	}

	[Fact]
	public void HaveDefaultMetadataCacheDuration()
	{
		var options = new VaultOptions();
		options.MetadataCacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultHttpTimeout()
	{
		var options = new VaultOptions();
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultEnableDetailedTelemetry()
	{
		var options = new VaultOptions();
		options.EnableDetailedTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullVaultUriByDefault()
	{
		var options = new VaultOptions();
		options.VaultUri.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNamespaceByDefault()
	{
		var options = new VaultOptions();
		options.Namespace.ShouldBeNull();
	}

	#endregion

	#region Auth Sub-Options Default Values

	[Fact]
	public void Auth_HaveDefaultAuthMethod()
	{
		var auth = new VaultAuthOptions();
		auth.AuthMethod.ShouldBe(VaultAuthMethod.Token);
	}

	[Fact]
	public void Auth_HaveDefaultAppRoleMountPath()
	{
		var auth = new VaultAuthOptions();
		auth.AppRoleMountPath.ShouldBe("approle");
	}

	[Fact]
	public void Auth_HaveDefaultKubernetesMountPath()
	{
		var auth = new VaultAuthOptions();
		auth.KubernetesMountPath.ShouldBe("kubernetes");
	}

	[Fact]
	public void Auth_HaveDefaultKubernetesJwtPath()
	{
		var auth = new VaultAuthOptions();
		auth.KubernetesJwtPath.ShouldBe("/var/run/secrets/kubernetes.io/serviceaccount/token");
	}

	#endregion

	#region Keys Sub-Options Default Values

	[Fact]
	public void Keys_HaveDefaultKeyType()
	{
		var keys = new VaultKeyOptions();
		keys.DefaultKeyType.ShouldBe("aes256-gcm96");
	}

	[Fact]
	public void Keys_HaveDefaultAllowKeyExport()
	{
		var keys = new VaultKeyOptions();
		keys.AllowKeyExport.ShouldBeFalse();
	}

	[Fact]
	public void Keys_HaveDefaultAllowPlaintextBackup()
	{
		var keys = new VaultKeyOptions();
		keys.AllowPlaintextBackup.ShouldBeFalse();
	}

	[Fact]
	public void Keys_HaveDefaultEnableConvergentEncryption()
	{
		var keys = new VaultKeyOptions();
		keys.EnableConvergentEncryption.ShouldBeFalse();
	}

	[Fact]
	public void Keys_HaveDefaultEnableKeyDerivation()
	{
		var keys = new VaultKeyOptions();
		keys.EnableKeyDerivation.ShouldBeFalse();
	}

	#endregion

	#region Retry Sub-Options Default Values

	[Fact]
	public void Retry_HaveDefaultEnableRetry()
	{
		var retry = new VaultRetryOptions();
		retry.EnableRetry.ShouldBeTrue();
	}

	[Fact]
	public void Retry_HaveDefaultMaxRetryAttempts()
	{
		var retry = new VaultRetryOptions();
		retry.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Retry_HaveDefaultRetryDelay()
	{
		var retry = new VaultRetryOptions();
		retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region Nested Initializer Tests

	[Fact]
	public void SupportNestedInitializerForAuth()
	{
		var options = new VaultOptions
		{
			Auth = new VaultAuthOptions
			{
				AuthMethod = VaultAuthMethod.AppRole,
				AppRoleId = "my-role-id",
				AppRoleSecretId = "my-secret-id",
			},
		};

		options.Auth.AuthMethod.ShouldBe(VaultAuthMethod.AppRole);
		options.Auth.AppRoleId.ShouldBe("my-role-id");
		options.Auth.AppRoleSecretId.ShouldBe("my-secret-id");
	}

	[Fact]
	public void SupportNestedInitializerForKeys()
	{
		var options = new VaultOptions
		{
			Keys = new VaultKeyOptions
			{
				DefaultKeyType = "rsa-4096",
				AllowKeyExport = true,
				EnableConvergentEncryption = true,
			},
		};

		options.Keys.DefaultKeyType.ShouldBe("rsa-4096");
		options.Keys.AllowKeyExport.ShouldBeTrue();
		options.Keys.EnableConvergentEncryption.ShouldBeTrue();
	}

	[Fact]
	public void SupportNestedInitializerForRetry()
	{
		var options = new VaultOptions
		{
			Retry = new VaultRetryOptions
			{
				EnableRetry = false,
				MaxRetryAttempts = 5,
				RetryDelay = TimeSpan.FromSeconds(2),
			},
		};

		options.Retry.EnableRetry.ShouldBeFalse();
		options.Retry.MaxRetryAttempts.ShouldBe(5);
		options.Retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void SupportCombinedConfiguration()
	{
		var options = new VaultOptions
		{
			VaultUri = new Uri("https://vault.example.com:8200"),
			TransitMountPath = "custom-transit",
			KeyNamePrefix = "myapp-",
			Namespace = "production",
			MetadataCacheDuration = TimeSpan.FromMinutes(10),
			HttpTimeout = TimeSpan.FromSeconds(60),
			EnableDetailedTelemetry = true,
			Auth = new VaultAuthOptions
			{
				AuthMethod = VaultAuthMethod.Kubernetes,
				KubernetesRole = "vault-role",
			},
			Keys = new VaultKeyOptions
			{
				DefaultKeyType = "ed25519",
				AllowKeyExport = true,
			},
			Retry = new VaultRetryOptions
			{
				MaxRetryAttempts = 5,
			},
		};

		options.VaultUri.AbsoluteUri.ShouldBe("https://vault.example.com:8200/");
		options.TransitMountPath.ShouldBe("custom-transit");
		options.KeyNamePrefix.ShouldBe("myapp-");
		options.Namespace.ShouldBe("production");
		options.Auth.AuthMethod.ShouldBe(VaultAuthMethod.Kubernetes);
		options.Auth.KubernetesRole.ShouldBe("vault-role");
		options.Keys.DefaultKeyType.ShouldBe("ed25519");
		options.Retry.MaxRetryAttempts.ShouldBe(5);
	}

	#endregion

	#region No Stale Shims

	[Fact]
	public void NotContainObsoleteProperties()
	{
		var allProperties = typeof(VaultOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var prop in allProperties)
		{
			var obsoleteAttr = prop.GetCustomAttribute<ObsoleteAttribute>();
			obsoleteAttr.ShouldBeNull(
				$"VaultOptions.{prop.Name} still has [Obsolete] shim — shim removal incomplete");
		}
	}

	[Fact]
	public void VaultAuthMethod_HaveExpectedValues()
	{
		// Verify enum values exist
		Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.Token).ShouldBeTrue();
		Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.AppRole).ShouldBeTrue();
		Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.Kubernetes).ShouldBeTrue();
	}

	#endregion
}
