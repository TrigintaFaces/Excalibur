// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;
using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests;

[UnitTest]
[Trait("Component", "Security")]
public sealed class HashiCorpVaultCredentialStoreShould : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly HashiCorpVaultCredentialStore _sut;
	private readonly IConfiguration _configuration;

	public HashiCorpVaultCredentialStoreShould()
	{
		var configValues = new Dictionary<string, string?>
		{
			["Vault:Url"] = "https://vault.example.com",
			["Vault:Token"] = "test-token-123",
			["Vault:MountPath"] = "secret",
			["Vault:Secrets:my-secret"] = "super-secret-value",
			["Vault:Secrets:db-password"] = "p@ssw0rd!",
		};

		_configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		_httpClient = new HttpClient();

		_sut = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance,
			_configuration,
			_httpClient);
	}

	// ========================================
	// Type Design Tests
	// ========================================

	[Fact]
	public void BeInternalAndSealed()
	{
		var type = typeof(HashiCorpVaultCredentialStore);
		type.IsSealed.ShouldBeTrue();
		type.IsNotPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIWritableCredentialStore()
	{
		_sut.ShouldBeAssignableTo<IWritableCredentialStore>();
	}

	[Fact]
	public void ImplementICredentialStore()
	{
		_sut.ShouldBeAssignableTo<ICredentialStore>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	// ========================================
	// Constructor Validation Tests
	// ========================================

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				null!,
				_configuration,
				_httpClient))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance,
				null!,
				_httpClient))
			.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void ThrowWhenHttpClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance,
				_configuration,
				null!))
			.ParamName.ShouldBe("httpClient");
	}

	[Fact]
	public void ThrowWhenVaultUrlMissing()
	{
		var configValues = new Dictionary<string, string?>
		{
			["Vault:Token"] = "test-token",
		};
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		Should.Throw<InvalidOperationException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance,
				config,
				new HttpClient()));
	}

	[Fact]
	public void ThrowWhenVaultTokenMissing()
	{
		var configValues = new Dictionary<string, string?>
		{
			["Vault:Url"] = "https://vault.example.com",
		};
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		Should.Throw<InvalidOperationException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance,
				config,
				new HttpClient()));
	}

	[Fact]
	public void ConfigureHttpClientBaseAddress()
	{
		_httpClient.BaseAddress.ShouldNotBeNull();
		_httpClient.BaseAddress.ToString().ShouldBe("https://vault.example.com/");
	}

	[Fact]
	public void ConfigureHttpClientVaultTokenHeader()
	{
		_httpClient.DefaultRequestHeaders.Contains("X-Vault-Token").ShouldBeTrue();
		_httpClient.DefaultRequestHeaders.GetValues("X-Vault-Token")
			.ShouldContain("test-token-123");
	}

	[Fact]
	public void ConfigureHttpClientTimeout()
	{
		_httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void UseDefaultMountPathWhenNotConfigured()
	{
		var configValues = new Dictionary<string, string?>
		{
			["Vault:Url"] = "https://vault.example.com",
			["Vault:Token"] = "test-token",
			["Vault:Secrets:test-key"] = "test-value",
		};
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance,
			config,
			new HttpClient());

		// Should not throw - default mount path "secret" is used
		Should.NotThrow(() => store.GetCredentialAsync("test-key", CancellationToken.None));
	}

	[Fact]
	public void AcceptCustomMountPath()
	{
		var configValues = new Dictionary<string, string?>
		{
			["Vault:Url"] = "https://vault.example.com",
			["Vault:Token"] = "test-token",
			["Vault:MountPath"] = "custom-engine",
			["Vault:Secrets:my-key"] = "my-value",
		};
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		// Should not throw with custom mount path
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance,
			config,
			new HttpClient());

		Should.NotThrow(() => store.GetCredentialAsync("my-key", CancellationToken.None));
	}

	// ========================================
	// GetCredentialAsync Tests
	// ========================================

	[Fact]
	public async Task ReturnSecureStringForExistingSecret()
	{
		var result = await _sut.GetCredentialAsync("my-secret", CancellationToken.None);

		result.ShouldNotBeNull();
		result.IsReadOnly().ShouldBeTrue();
		SecureStringToString(result).ShouldBe("super-secret-value");
	}

	[Fact]
	public async Task ReturnNullForNonExistentSecret()
	{
		var result = await _sut.GetCredentialAsync("non-existent-key", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowOnGetCredentialWithInvalidKey(string? key)
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetCredentialAsync(key, CancellationToken.None));
	}

	[Fact]
	public async Task RetrieveDifferentSecretsCorrectly()
	{
		var secret1 = await _sut.GetCredentialAsync("my-secret", CancellationToken.None);
		var secret2 = await _sut.GetCredentialAsync("db-password", CancellationToken.None);

		secret1.ShouldNotBeNull();
		secret2.ShouldNotBeNull();
		SecureStringToString(secret1).ShouldBe("super-secret-value");
		SecureStringToString(secret2).ShouldBe("p@ssw0rd!");
	}

	[Fact]
	public async Task ReturnReadOnlySecureStringFromGetCredential()
	{
		var result = await _sut.GetCredentialAsync("db-password", CancellationToken.None);

		result.ShouldNotBeNull();
		result.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task SupportGetCredentialCancellation()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.GetCredentialAsync("my-secret", cts.Token));
	}

	// ========================================
	// StoreCredentialAsync Tests
	// ========================================

	[Fact]
	public async Task StoreCredentialWithoutThrowing()
	{
		var credential = CreateSecureString("new-secret-value");

		await Should.NotThrowAsync(
			() => _sut.StoreCredentialAsync("new-key", credential, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowOnStoreCredentialWithInvalidKey(string? key)
	{
		var credential = CreateSecureString("value");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.StoreCredentialAsync(key, credential, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnStoreCredentialWithNullCredential()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.StoreCredentialAsync("key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnStoreEmptyCredential()
	{
		var emptyCredential = new SecureString();
		emptyCredential.MakeReadOnly();

		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.StoreCredentialAsync("key", emptyCredential, CancellationToken.None));
	}

	[Fact]
	public async Task SupportStoreCredentialCancellation()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var credential = CreateSecureString("some-value");

		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.StoreCredentialAsync("key", credential, cts.Token));
	}

	// ========================================
	// Concurrent Access Tests
	// ========================================

	[Fact]
	public async Task HandleConcurrentGetCredentialCalls()
	{
		// Launch multiple concurrent reads to verify the semaphore serializes access
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => _sut.GetCredentialAsync("my-secret", CancellationToken.None))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		foreach (var result in results)
		{
			result.ShouldNotBeNull();
			SecureStringToString(result).ShouldBe("super-secret-value");
		}
	}

	[Fact]
	public async Task HandleConcurrentStoreCredentialCalls()
	{
		// Launch multiple concurrent stores to verify the semaphore serializes access
		var tasks = Enumerable.Range(0, 10)
			.Select(i =>
			{
				var credential = CreateSecureString($"value-{i}");
				return _sut.StoreCredentialAsync($"key-{i}", credential, CancellationToken.None);
			})
			.ToArray();

		await Should.NotThrowAsync(() => Task.WhenAll(tasks));
	}

	// ========================================
	// Dispose Tests
	// ========================================

	[Fact]
	public void DisposeWithoutThrowing()
	{
		var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance,
			_configuration,
			new HttpClient());

		Should.NotThrow(() => store.Dispose());
	}

	[Fact]
	public void DisposeMultipleTimesWithoutThrowing()
	{
		var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance,
			_configuration,
			new HttpClient());

		Should.NotThrow(() =>
		{
			store.Dispose();
			store.Dispose();
		});
	}

	// ========================================
	// DI Registration Tests
	// ========================================

	[Fact]
	public void RegisterHashiCorpVaultWhenVaultUrlIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_configuration);
		_ = services.AddLogging();

		_ = services.AddSecureCredentialManagement(_configuration);

		services.ShouldContain(s =>
			s.ServiceType == typeof(ICredentialStore)
			&& s.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void RegisterIWritableCredentialStoreWhenVaultUrlIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_configuration);
		_ = services.AddLogging();

		_ = services.AddSecureCredentialManagement(_configuration);

		services.ShouldContain(s =>
			s.ServiceType == typeof(IWritableCredentialStore)
			&& s.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void NotRegisterHashiCorpVaultWhenVaultUrlIsMissing()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();
		var services = new ServiceCollection();

		_ = services.AddSecureCredentialManagement(config);

		services.ShouldNotContain(s =>
			s.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void RegisterAsSingletonLifetime()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_configuration);
		_ = services.AddLogging();

		_ = services.AddSecureCredentialManagement(_configuration);

		var descriptor = services.First(s =>
			s.ServiceType == typeof(ICredentialStore)
			&& s.ImplementationType == typeof(HashiCorpVaultCredentialStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddSecureCredentialManagement_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();

		var result = services.AddSecureCredentialManagement(_configuration);

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddSecureCredentialManagement_ThrowsWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddSecureCredentialManagement(null!));
	}

	public void Dispose()
	{
		_sut.Dispose();
		_httpClient.Dispose();
	}

	private static SecureString CreateSecureString(string value)
	{
		var secure = new SecureString();
		foreach (var c in value)
		{
			secure.AppendChar(c);
		}

		secure.MakeReadOnly();
		return secure;
	}

	private static string SecureStringToString(SecureString secureString)
	{
		var ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
			return Marshal.PtrToStringUni(ptr) ?? string.Empty;
		}
		finally
		{
			if (ptr != IntPtr.Zero)
			{
				Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}
}
