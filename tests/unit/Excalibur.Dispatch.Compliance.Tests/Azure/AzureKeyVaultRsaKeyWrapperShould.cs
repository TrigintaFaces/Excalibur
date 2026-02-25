// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

using Excalibur.Dispatch.Compliance.Azure;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Azure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AzureKeyVaultRsaKeyWrapperShould
{
	[Fact]
	public void Constructor_ThrowsForNullArguments()
	{
		var wrappingOptions = Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
		{
			KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/"),
			KeyName = "dispatch-rsa"
		});
		var vaultOptions = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://unit-tests.vault.azure.net/")
		});
		var logger = NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance;

		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultRsaKeyWrapper(null!, vaultOptions, logger));
		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultRsaKeyWrapper(wrappingOptions, null!, logger));
		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultRsaKeyWrapper(wrappingOptions, vaultOptions, null!));
	}

	[Fact]
	public void Constructor_ThrowsWhenKeyVaultUrlMissing()
	{
		var wrappingOptions = Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
		{
			KeyName = "dispatch-rsa"
		});
		var vaultOptions = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://unit-tests.vault.azure.net/")
		});

		_ = Should.Throw<ArgumentException>(() => new AzureKeyVaultRsaKeyWrapper(
			wrappingOptions,
			vaultOptions,
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsWhenKeyNameMissing()
	{
		var wrappingOptions = Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
		{
			KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/")
		});
		var vaultOptions = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://unit-tests.vault.azure.net/")
		});

		_ = Should.Throw<ArgumentException>(() => new AzureKeyVaultRsaKeyWrapper(
			wrappingOptions,
			vaultOptions,
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance));
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		var sut = new AzureKeyVaultRsaKeyWrapper(
			Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
			{
				KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/"),
				KeyName = "dispatch-rsa"
			}),
			Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri("https://unit-tests.vault.azure.net/")
			}),
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance);

		sut.Dispose();
		sut.Dispose();
	}

	[Fact]
	public void MapAlgorithm_MapsSupportedValues()
	{
		var mapAlgorithm = typeof(AzureKeyVaultRsaKeyWrapper).GetMethod(
			"MapAlgorithm",
			BindingFlags.NonPublic | BindingFlags.Static);

		mapAlgorithm.ShouldNotBeNull();

		var rsaOaep = (KeyWrapAlgorithm)mapAlgorithm!.Invoke(null, [RsaWrappingAlgorithm.RsaOaep])!;
		var rsaOaep256 = (KeyWrapAlgorithm)mapAlgorithm.Invoke(null, [RsaWrappingAlgorithm.RsaOaep256])!;

		rsaOaep.ShouldBe(KeyWrapAlgorithm.RsaOaep);
		rsaOaep256.ShouldBe(KeyWrapAlgorithm.RsaOaep256);
	}

	[Fact]
	public void MapAlgorithm_UsesFallbackForUnknownValue()
	{
		var mapAlgorithm = typeof(AzureKeyVaultRsaKeyWrapper).GetMethod(
			"MapAlgorithm",
			BindingFlags.NonPublic | BindingFlags.Static);

		mapAlgorithm.ShouldNotBeNull();

		var unknown = (RsaWrappingAlgorithm)999;
		var resolved = (KeyWrapAlgorithm)mapAlgorithm!.Invoke(null, [unknown])!;

		resolved.ShouldBe(KeyWrapAlgorithm.RsaOaep256);
	}

	[Fact]
	public async Task WrapAndUnwrap_ThrowForInvalidArguments_WithoutCallingAzure()
	{
		var sut = new AzureKeyVaultRsaKeyWrapper(
			Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
			{
				KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/"),
				KeyName = "dispatch-rsa"
			}),
			Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri("https://unit-tests.vault.azure.net/")
			}),
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance);

		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.WrapKeyAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.WrapKeyAsync([], CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.UnwrapKeyAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.UnwrapKeyAsync([], CancellationToken.None));
	}

	[Fact]
	public async Task WrapAndUnwrap_ThrowObjectDisposedException_AfterDispose()
	{
		var sut = new AzureKeyVaultRsaKeyWrapper(
			Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
			{
				KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/"),
				KeyName = "dispatch-rsa"
			}),
			Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri("https://unit-tests.vault.azure.net/")
			}),
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance);

		sut.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(() => sut.WrapKeyAsync([1], CancellationToken.None));
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => sut.UnwrapKeyAsync([1], CancellationToken.None));
	}

	[Fact]
	public async Task WrapKeyAsync_UsesConfiguredCryptoClient()
	{
		var sut = CreateWrapper();
		var cryptoClient = A.Fake<CryptographyClient>();
		SetPrivateField(sut, "_cryptoClient", cryptoClient);

		A.CallTo(() => cryptoClient.WrapKeyAsync(A<KeyWrapAlgorithm>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				global::Azure.Security.KeyVault.Keys.CryptographyModelFactory.WrapResult("kid", [7, 8, 9], KeyWrapAlgorithm.RsaOaep256)));

		var wrapped = await sut.WrapKeyAsync([1, 2, 3, 4], CancellationToken.None);

		wrapped.ShouldBe([7, 8, 9]);
		A.CallTo(() => cryptoClient.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, A<byte[]>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UnwrapKeyAsync_UsesConfiguredCryptoClient()
	{
		var sut = CreateWrapper();
		var cryptoClient = A.Fake<CryptographyClient>();
		SetPrivateField(sut, "_cryptoClient", cryptoClient);

		A.CallTo(() => cryptoClient.UnwrapKeyAsync(A<KeyWrapAlgorithm>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				global::Azure.Security.KeyVault.Keys.CryptographyModelFactory.UnwrapResult("kid", [3, 2, 1], KeyWrapAlgorithm.RsaOaep256)));

		var unwrapped = await sut.UnwrapKeyAsync([9, 8, 7], CancellationToken.None);

		unwrapped.ShouldBe([3, 2, 1]);
		A.CallTo(() => cryptoClient.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, A<byte[]>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WrapKeyAsync_RethrowsClientException()
	{
		var sut = CreateWrapper();
		var cryptoClient = A.Fake<CryptographyClient>();
		SetPrivateField(sut, "_cryptoClient", cryptoClient);

		A.CallTo(() => cryptoClient.WrapKeyAsync(A<KeyWrapAlgorithm>._, A<byte[]>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("wrap-fail"));

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => sut.WrapKeyAsync([1], CancellationToken.None));
		ex.Message.ShouldBe("wrap-fail");
	}

	[Fact]
	public async Task UnwrapKeyAsync_RethrowsClientException()
	{
		var sut = CreateWrapper();
		var cryptoClient = A.Fake<CryptographyClient>();
		SetPrivateField(sut, "_cryptoClient", cryptoClient);

		A.CallTo(() => cryptoClient.UnwrapKeyAsync(A<KeyWrapAlgorithm>._, A<byte[]>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("unwrap-fail"));

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => sut.UnwrapKeyAsync([1], CancellationToken.None));
		ex.Message.ShouldBe("unwrap-fail");
	}

	private static AzureKeyVaultRsaKeyWrapper CreateWrapper()
	{
		return new AzureKeyVaultRsaKeyWrapper(
			Microsoft.Extensions.Options.Options.Create(new RsaKeyWrappingOptions
			{
				KeyVaultUrl = new Uri("https://unit-tests.vault.azure.net/"),
				KeyName = "dispatch-rsa"
			}),
			Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri("https://unit-tests.vault.azure.net/")
			}),
			NullLogger<AzureKeyVaultRsaKeyWrapper>.Instance);
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}
}
