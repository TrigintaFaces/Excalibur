// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;
using System.Security;

using Excalibur.Security;
using Excalibur.Security.Vault;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests;

// bd-ts66sh (S841, ADR-336): HashiCorpVaultCredentialStore now reads/writes secrets through the REAL Vault
// KV v2 HTTP API via an injectable IVaultSecretClient seam — no longer the silent config-fallback placeholder
// (GetCredentialAsync read Vault:Secrets:* from IConfiguration; StoreCredentialAsync discarded the secret while
// logging success). The behavior tests below are the INDEPENDENT engage-test (author≠impl, TestsDeveloper): a
// store→get round-trip MUST persist + retrieve THROUGH THE PORT (FakeVaultSecretClient), never via configuration,
// and a backend error must surface (never logged-as-success). No real credentials in any tracked artifact.
[UnitTest]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
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
			["Vault:Token"] = "test-token-123", // pragma: allowlist secret — example token, not a real credential
			["Vault:MountPath"] = "secret",
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
	public void ImplementIWritableCredentialStore() => _sut.ShouldBeAssignableTo<IWritableCredentialStore>();

	[Fact]
	public void ImplementICredentialStore() => _sut.ShouldBeAssignableTo<ICredentialStore>();

	[Fact]
	public void ImplementIDisposable() => _sut.ShouldBeAssignableTo<IDisposable>();

	// ========================================
	// Constructor Validation Tests
	// ========================================

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(null!, _configuration, _httpClient))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, null!, _httpClient))
			.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void ThrowWhenHttpClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, _configuration, null!))
			.ParamName.ShouldBe("httpClient");
	}

	[Fact]
	public void ThrowWhenInjectedClientIsNull()
	{
		// The internal test seam ctor must guard the port.
		Should.Throw<ArgumentNullException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, (IVaultSecretClient)null!))
			.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ThrowWhenVaultUrlMissing()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Token"] = "test-token", // pragma: allowlist secret — example token
			})
			.Build();

		Should.Throw<InvalidOperationException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, config, new HttpClient()));
	}

	[Fact]
	public void ThrowWhenVaultTokenMissing()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Url"] = "https://vault.example.com",
			})
			.Build();

		Should.Throw<InvalidOperationException>(() =>
			new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, config, new HttpClient()));
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
		_httpClient.DefaultRequestHeaders.GetValues("X-Vault-Token").ShouldContain("test-token-123");
	}

	[Fact]
	public void ConfigureHttpClientTimeout() => _httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(30));

	[Fact]
	public void UseDefaultMountPathWhenNotConfigured()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Url"] = "https://vault.example.com",
				["Vault:Token"] = "test-token", // pragma: allowlist secret — example token
			})
			.Build();

		// Default mount path "secret" is used — construction must not throw.
		Should.NotThrow(() =>
		{
			using var store = new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, config, new HttpClient());
		});
	}

	[Fact]
	public void AcceptCustomMountPath()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Url"] = "https://vault.example.com",
				["Vault:Token"] = "test-token", // pragma: allowlist secret — example token
				["Vault:MountPath"] = "custom-engine",
			})
			.Build();

		Should.NotThrow(() =>
		{
			using var store = new HashiCorpVaultCredentialStore(
				NullLogger<HashiCorpVaultCredentialStore>.Instance, config, new HttpClient());
		});
	}

	// ========================================
	// Engage-test: real port-backed round-trip (AC-1) — RED on the pre-fix config-fallback stub
	// ========================================

	[Fact]
	public async Task RoundTripCredentialThroughTheVaultPort()
	{
		// Arrange — a fake Vault transport that actually persists (proves the store delegates to the port,
		// not to IConfiguration). RED on the old stub: store discarded the secret + get read config → no round-trip.
		var client = new FakeVaultSecretClient();
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, client);

		// Act
		await store.StoreCredentialAsync("api-key", CreateSecureString("s3cr3t-value"), CancellationToken.None);
		var result = await store.GetCredentialAsync("api-key", CancellationToken.None);

		// Assert — the value round-trips THROUGH THE PORT.
		result.ShouldNotBeNull();
		result.IsReadOnly().ShouldBeTrue();
		SecureStringToString(result).ShouldBe("s3cr3t-value");
		client.SetCalls.ShouldBe(1, "the secret must be persisted via the Vault port, not discarded");
		client.GetCalls.ShouldBe(1, "the secret must be read via the Vault port, not from IConfiguration");
	}

	[Fact]
	public async Task ReturnNullForNonExistentSecret()
	{
		var client = new FakeVaultSecretClient();
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, client);

		var result = await store.GetCredentialAsync("non-existent-key", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task SurfaceBackendError_NeverLoggedAsSuccess_OnGet()
	{
		// EC-2 — a transport/backend failure must surface as an error, distinct from a missing secret.
		var client = new FakeVaultSecretClient
		{
			GetBehavior = _ => throw new HttpRequestException("vault unreachable"),
		};
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, client);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.GetCredentialAsync("api-key", CancellationToken.None));
	}

	[Fact]
	public async Task SurfaceBackendError_OnStore()
	{
		var client = new FakeVaultSecretClient
		{
			SetBehavior = (_, _) => throw new HttpRequestException("vault write rejected"),
		};
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, client);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.StoreCredentialAsync("api-key", CreateSecureString("v"), CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowOnGetCredentialWithInvalidKey(string? key)
	{
		await Should.ThrowAsync<ArgumentException>(() => _sut.GetCredentialAsync(key!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowOnStoreCredentialWithInvalidKey(string? key)
	{
		var credential = CreateSecureString("value");
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.StoreCredentialAsync(key!, credential, CancellationToken.None));
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
		var client = new FakeVaultSecretClient();
		using var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, client);
		var emptyCredential = new SecureString();
		emptyCredential.MakeReadOnly();

		// An empty credential is rejected with ArgumentException (the store's `catch (ArgumentException) throw;`
		// re-throws it unwrapped — it is a caller error, not a backend failure).
		await Should.ThrowAsync<ArgumentException>(
			() => store.StoreCredentialAsync("key", emptyCredential, CancellationToken.None));
	}

	// ========================================
	// Dispose Tests
	// ========================================

	[Fact]
	public void DisposeWithoutThrowing()
	{
		var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, _configuration, new HttpClient());

		Should.NotThrow(store.Dispose);
	}

	[Fact]
	public void DisposeMultipleTimesWithoutThrowing()
	{
		var store = new HashiCorpVaultCredentialStore(
			NullLogger<HashiCorpVaultCredentialStore>.Instance, _configuration, new HttpClient());

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

		services.ShouldContain(s => s.ServiceType == typeof(HashiCorpVaultCredentialStore));
		services.ShouldContain(s =>
			s.ServiceType == typeof(ICredentialStore) && s.ImplementationFactory != null);
	}

	[Fact]
	public void RegisterIWritableCredentialStoreWhenVaultUrlIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_configuration);
		_ = services.AddLogging();

		_ = services.AddSecureCredentialManagement(_configuration);

		services.ShouldContain(s =>
			s.ServiceType == typeof(IWritableCredentialStore) && s.ImplementationFactory != null);
	}

	[Fact]
	public void NotRegisterHashiCorpVaultWhenVaultUrlIsMissing()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();
		var services = new ServiceCollection();

		_ = services.AddSecureCredentialManagement(config);

		services.ShouldNotContain(s => s.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void RegisterAsSingletonLifetime()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_configuration);
		_ = services.AddLogging();

		_ = services.AddSecureCredentialManagement(_configuration);

		var concreteDescriptor = services.First(s => s.ServiceType == typeof(HashiCorpVaultCredentialStore));
		concreteDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
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

		Should.Throw<ArgumentNullException>(() => services.AddSecureCredentialManagement(null!));
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

	/// <summary>
	/// Hand-written dictionary-backed fake of the internal <see cref="IVaultSecretClient"/> seam (FakeItEasy
	/// cannot proxy an internal interface here). Persists writes in-memory so the store→get round-trip exercises
	/// the store's real delegation to the port — never a committed credential.
	/// </summary>
	private sealed class FakeVaultSecretClient : IVaultSecretClient
	{
		private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);

		public int GetCalls { get; private set; }
		public int SetCalls { get; private set; }
		public Func<string, string?>? GetBehavior { get; init; }
		public Action<string, string>? SetBehavior { get; init; }

		public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			GetCalls++;
			if (GetBehavior is not null)
			{
				return Task.FromResult(GetBehavior(key));
			}

			return Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);
		}

		public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SetCalls++;
			if (SetBehavior is not null)
			{
				SetBehavior(key, value);
				return Task.CompletedTask;
			}

			_store[key] = value;
			return Task.CompletedTask;
		}
	}
}
