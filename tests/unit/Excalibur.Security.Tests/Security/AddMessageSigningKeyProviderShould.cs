// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Dispatch;
using Excalibur.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Security;

/// <summary>
/// Contract lock for signing key material: the framework never supplies it, a host that does not ask
/// for signing never has to, and a host that asks for signing without supplying it is told so loudly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every arm resolves through a real <see cref="ServiceProvider"/> built from the production
/// registration path.</b> <see cref="HmacMessageSigningService"/> takes an <see cref="IKeyProvider"/> as
/// a constructor dependency, and a test that hands the service a key provider it built itself proves
/// only that the service works when someone hands it one, never what the container does. That gap is
/// where the defect lived: the signing service was registered, no key provider was, and every existing
/// unit test supplied a fake, so the container a consumer would actually get was never exercised.
/// </para>
/// <para>
/// <b>The three properties under lock.</b> (1) Composing the security stack without asking for signing
/// yields a container that builds and a dispatch pipeline that enumerates, which is the reported
/// defect. (2) Asking for signing without a key provider fails loudly at the startup gate, naming what
/// is missing, never silently and never with fabricated key material: a key nobody chose produces
/// signatures no other instance can verify, and an unverifiable signature is rejected as an
/// authorization failure by every instance that receives it. (3) The provider the host registers is the
/// one the signer receives.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AddMessageSigningKeyProviderShould
{
	/// <summary>
	/// The reported defect, in the shape it was reported: a host names encryption and rate limiting,
	/// and its entire dispatch pipeline becomes unresolvable because signing composed itself and
	/// brought an unsatisfiable key-provider dependency with it.
	/// </summary>
	[Fact]
	public void ComposeNoSigning_WhenTheHostNamesOtherSecurityFeatures()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchSecurityMiddleware(static options =>
		{
			options.Encryption.EnableEncryption = true;
			options.RateLimiting.EnableRateLimiting = true;
			options.Authentication.EnableAuthentication = false;
		});

		using var provider = services.BuildServiceProvider();

		// Enumerating the pipeline is what activates every registered middleware and its dependencies.
		// Pre-fix this threw on the unregistered IKeyProvider, so a host that never signs a single
		// message lost every middleware it had.
		var middleware = provider.GetServices<IDispatchMiddleware>().Select(m => m.GetType()).ToList();

		middleware.ShouldNotContain(
			typeof(MessageSigningMiddleware),
			"signing was never requested, so it must not be in the pipeline");
		provider.GetService<IMessageSigningService>().ShouldBeNull(
			"signing was never requested, so no signing service should have been composed");
	}

	/// <summary>
	/// The same property on the configuration-driven entry point, which is the one the documented
	/// complete setup goes through.
	/// </summary>
	[Fact]
	public void ComposeNoSigning_WhenConfigurationDoesNotEnableIt()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchSecurityMiddleware(EmptyConfiguration());

		// Asserted on the descriptors rather than by resolving: this overload also composes JWT
		// authentication, and this arm is about what signing did, not about activating its neighbours.
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IMessageSigningService));
		services.ShouldNotContain(sd => sd.ImplementationType == typeof(MessageSigningMiddleware));
	}

	/// <summary>
	/// The framework supplies no key material of its own. This is the structural half of the ruling:
	/// there is no registration to fall back to, so signing with a key nobody chose is not a state a
	/// host can reach by omission.
	/// </summary>
	[Theory]
	[MemberData(nameof(SigningEntryPoints))]
	public void RegisterNoKeyProviderOfItsOwn(string entryPoint)
	{
		var compose = EntryPoints[entryPoint];

		var services = new ServiceCollection();
		_ = services.AddLogging();
		compose(services);

		services.ShouldNotContain(
			sd => sd.ServiceType == typeof(IKeyProvider),
			"the framework must never register signing key material on behalf of the consumer");
	}

	/// <summary>
	/// And when signing is opted into without one, the startup gate says so, naming the missing
	/// dependency, rather than deferring to an activation failure at first dispatch.
	/// </summary>
	[Theory]
	[MemberData(nameof(SigningEntryPoints))]
	public void FailLoudlyAtStartup_WhenSigningIsEnabledWithNoKeyProvider(string entryPoint)
	{
		var compose = EntryPoints[entryPoint];

		var services = new ServiceCollection();
		_ = services.AddLogging();
		compose(services);

		using var provider = services.BuildServiceProvider();

		var exception = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		exception.Message.ShouldContain(
			nameof(IKeyProvider),
			customMessage: "the failure must name the dependency the consumer has to supply");
	}

	/// <summary>
	/// The startup gate stays quiet when signing is off, so a host that composed the signing package
	/// but disabled the feature is not blocked on infrastructure it will never use.
	/// </summary>
	[Fact]
	public void StayQuietAtStartup_WhenSigningIsDisabled()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMessageSigning(static o => o.Enabled = false);

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.ValidateStartupGates());
	}

	/// <summary>
	/// With a provider supplied, the container builds a signer that actually signs and verifies,
	/// resolved and never hand-constructed.
	/// </summary>
	[Theory]
	[MemberData(nameof(SigningEntryPoints))]
	public async Task BuildAWorkingSigner_WhenTheHostSuppliesAKeyProvider(string entryPoint)
	{
		var compose = EntryPoints[entryPoint];

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(StableKeyProvider());
		compose(services);

		await using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.ValidateStartupGates());

		var signer = provider.GetRequiredService<IMessageSigningService>();
		var context = new SigningContext();
		var signature = await signer.SignMessageAsync(
			"the message", context, TestContext.Current.CancellationToken);

		signature.ShouldNotBeNullOrEmpty();

		var verified = await signer.VerifySignatureAsync(
			"the message", signature, context, TestContext.Current.CancellationToken);

		verified.ShouldBeTrue(
			"a signature produced by the resolved signer must verify through the same container, "
			+ "otherwise the key provider it was given is not returning a stable key.");
	}

	/// <summary>
	/// A host that registers its own provider before composing signing keeps it.
	/// </summary>
	[Fact]
	public Task UseTheHostsKeyProvider_WhenItIsRegisteredFirst() =>
		AssertHostKeyProviderIsUsed(registerHostProviderFirst: true);

	/// <summary>
	/// And a host that registers it afterwards keeps it too, so nothing composed by the framework can
	/// shadow a deliberate choice made later in composition.
	/// </summary>
	[Fact]
	public Task UseTheHostsKeyProvider_WhenItIsRegisteredLast() =>
		AssertHostKeyProviderIsUsed(registerHostProviderFirst: false);

	// Rows carry the entry-point NAME only; the compose delegate is looked up inside the fact. A
	// delegate is not serializable, so a theory keyed on one collapses to a single unnamed row in the
	// runner instead of one row per entry point -- and an entry point that stopped being covered would
	// not show in the results.
	private static readonly Dictionary<string, Action<IServiceCollection>> EntryPoints = new(StringComparer.Ordinal)
	{
		{ "AddMessageSigning()", static services => services.AddMessageSigning() },
		{
			"AddMessageSigning(options)",
			static services => services.AddMessageSigning(static o =>
			{
				o.Enabled = true;
				o.DefaultAlgorithm = SigningAlgorithm.HMACSHA512;
			})
		},
		{
			"AddMessageSigning(configuration)",
			static services => services.AddMessageSigning(EnabledSigningConfiguration())
		},
		{
			// The asymmetric entry point replaces the HMAC signer with CompositeMessageSigningService,
			// which takes the same key-provider dependency and so carries the same contract.
			"AddAsymmetricSigning()",
			static services => services.AddAsymmetricSigning()
		},
		{
			"AddDispatchSecurityMiddleware(EnableSigning)",
			static services => services.AddDispatchSecurityMiddleware(static options =>
			{
				options.Signing.EnableSigning = true;
				options.Encryption.EnableEncryption = false;
				options.RateLimiting.EnableRateLimiting = false;
				options.Authentication.EnableAuthentication = false;
			})
		},
		{
			"AddDispatchSecurityMiddleware(configuration enabling signing)",
			static services => services.AddDispatchSecurityMiddleware(EnabledSigningConfiguration())
		},
	};

	public static TheoryData<string> SigningEntryPoints() => [.. EntryPoints.Keys];

	private static IKeyProvider StableKeyProvider()
	{
		var key = RandomNumberGenerator.GetBytes(64);
		var keyProvider = A.Fake<IKeyProvider>();
		_ = A.CallTo(() => keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(key));

		return keyProvider;
	}

	private static async Task AssertHostKeyProviderIsUsed(bool registerHostProviderFirst)
	{
		var hostProvider = StableKeyProvider();

		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (registerHostProviderFirst)
		{
			_ = services.AddSingleton(hostProvider);
			_ = services.AddMessageSigning();
		}
		else
		{
			_ = services.AddMessageSigning();
			_ = services.AddSingleton(hostProvider);
		}

		await using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IKeyProvider>().ShouldBeSameAs(
			hostProvider,
			"nothing the framework registers may displace a provider the host registered.");

		// Stronger than the descriptor check: the container must hand the host's provider to the real
		// signing service, which is the thing a host actually cares about.
		var signer = provider.GetRequiredService<IMessageSigningService>();
		_ = await signer.SignMessageAsync(
			"the message", new SigningContext(), TestContext.Current.CancellationToken);

		A.CallTo(() => hostProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

	private static IConfiguration EnabledSigningConfiguration() => new ConfigurationBuilder()
		.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Enabled"] = "true",
			["Security:Signing:Enabled"] = "true",
		})
		.Build();
}
