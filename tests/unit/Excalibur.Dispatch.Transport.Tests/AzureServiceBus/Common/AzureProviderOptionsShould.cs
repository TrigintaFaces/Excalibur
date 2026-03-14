// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureProviderOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureProviderOptions();

		// Assert
		options.Provider.ShouldBe(CloudProviderType.Azure);
		options.SubscriptionId.ShouldBe(string.Empty);
		options.Authentication.TenantId.ShouldBe(string.Empty);
		options.Authentication.ClientId.ShouldBe(string.Empty);
		options.Authentication.ClientSecret.ShouldBe(string.Empty);
		options.Authentication.UseManagedIdentity.ShouldBeFalse();
		options.ResourceGroup.ShouldBe(string.Empty);
		options.KeyVaultUrl.ShouldBeNull();
		options.Storage.StorageAccountName.ShouldBe(string.Empty);
		options.Storage.StorageAccountKey.ShouldBe(string.Empty);
		options.FullyQualifiedNamespace.ShouldBeNull();
		options.Storage.StorageAccountUri.ShouldBeNull();
		options.MaxMessageSizeBytes.ShouldBe(256 * 1024);
		options.EnableSessions.ShouldBeFalse();
		options.PrefetchCount.ShouldBe(10);
		options.RetryOptions.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAuthenticationProperties()
	{
		// Arrange & Act
		var options = new AzureProviderOptions
		{
			FullyQualifiedNamespace = "myns.servicebus.windows.net",
		};
		options.Authentication.TenantId = "tenant-123";
		options.Authentication.ClientId = "client-456";
		options.Authentication.ClientSecret = "secret-789";
		options.Authentication.UseManagedIdentity = true;

		// Assert
		options.Authentication.TenantId.ShouldBe("tenant-123");
		options.Authentication.ClientId.ShouldBe("client-456");
		options.Authentication.ClientSecret.ShouldBe("secret-789");
		options.Authentication.UseManagedIdentity.ShouldBeTrue();
		options.FullyQualifiedNamespace.ShouldBe("myns.servicebus.windows.net");
	}

	[Fact]
	public void AllowSettingStorageProperties()
	{
		// Arrange & Act
		var options = new AzureProviderOptions();
		options.Storage.StorageAccountName = "mystorage";
		options.Storage.StorageAccountKey = "mykey";
		options.Storage.StorageAccountUri = new Uri("https://mystorage.blob.core.windows.net");

		// Assert
		options.Storage.StorageAccountName.ShouldBe("mystorage");
		options.Storage.StorageAccountKey.ShouldBe("mykey");
		options.Storage.StorageAccountUri.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingSessionAndPrefetchConfiguration()
	{
		// Arrange & Act
		var options = new AzureProviderOptions
		{
			EnableSessions = true,
			PrefetchCount = 50,
			MaxMessageSizeBytes = 1024 * 1024,
		};

		// Assert
		options.EnableSessions.ShouldBeTrue();
		options.PrefetchCount.ShouldBe(50);
		options.MaxMessageSizeBytes.ShouldBe(1024 * 1024);
	}
}
