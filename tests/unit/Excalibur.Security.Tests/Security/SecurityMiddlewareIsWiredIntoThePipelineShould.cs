// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Security;

/// <summary>
/// Regression lock: security middleware composed into the service collection MUST reach the dispatch
/// pipeline, and MUST NOT drag in the security features the host never asked for.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline discovers middleware by enumerating <see cref="IDispatchMiddleware" /> from DI.
/// Every security middleware was previously registered under its concrete type only, so none of them
/// was ever enumerated: a host that composed the documented security setup silently got no
/// authentication, no signing, no rate limiting, no encryption and no input validation.
/// </para>
/// <para>
/// <b>Both arms are load-bearing.</b> The liveness arm (the composed feature IS in the resolved
/// pipeline) fails when a registration is missing. The safety arm (every other security middleware is
/// NOT in the pipeline) fails when the fix over-corrects by registering all features from one method —
/// which would pass the liveness arm while being its own defect. Middleware is resolved through a real
/// <see cref="ServiceProvider" /> built from the production registration path, so this asserts the
/// instances the pipeline would actually receive, not the presence of a service descriptor.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityMiddlewareIsWiredIntoThePipelineShould
{
	// JWT options validation requires complete credentials; these are test-only values.
	private const string TestIssuer = "https://test.issuer.invalid";
	private const string TestAudience = "test-audience";
	private const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

	private static readonly Type[] AllSecurityMiddleware =
	[
		typeof(JwtAuthenticationMiddleware),
		typeof(RateLimitingMiddleware),
		typeof(MessageEncryptionMiddleware),
		typeof(MessageSigningMiddleware),
		typeof(InputValidationMiddleware),
	];

	[Fact]
	public void PlaceJwtAuthenticationInThePipeline_AndNothingElse() =>
		AssertPipelineContainsExactly(
			Compose(static services => services.AddJwtAuthentication(static o =>
			{
				o.Enabled = true;
				o.Credentials.ValidIssuer = TestIssuer;
				o.Credentials.ValidAudience = TestAudience;
				o.Credentials.SigningKey = TestSigningKey;
			})),
			typeof(JwtAuthenticationMiddleware));

	[Fact]
	public void PlaceRateLimitingInThePipeline_AndNothingElse() =>
		AssertPipelineContainsExactly(
			Compose(static services => services.AddRateLimiting(static o => o.Enabled = true)),
			typeof(RateLimitingMiddleware));

	[Fact]
	public void PlaceMessageEncryptionInThePipeline_AndNothingElse() =>
		AssertPipelineContainsExactly(
			Compose(static services => services.AddMessageEncryption(static o => o.Enabled = true)),
			typeof(MessageEncryptionMiddleware));

	[Fact]
	public void PlaceMessageSigningInThePipeline_AndNothingElse() =>
		AssertPipelineContainesExactlySigning();

	[Fact]
	public void PlaceInputValidationInThePipeline_AndNothingElse() =>
		AssertPipelineContainsExactly(
			Compose(static services => services.AddInputValidation(EmptyConfiguration())),
			typeof(InputValidationMiddleware));

	/// <summary>
	/// The safety arm in its purest form: a host that composes no security feature at all gets no
	/// security middleware. Guards against a future "register everything unconditionally" regression.
	/// </summary>
	[Fact]
	public void PlaceNoSecurityMiddlewareInThePipeline_WhenNoFeatureIsComposed()
	{
		using var provider = Compose(static _ => { }).BuildServiceProvider();

		var resolved = ResolvedMiddlewareTypes(provider);

		foreach (var middleware in AllSecurityMiddleware)
		{
			resolved.ShouldNotContain(middleware);
		}
	}

	/// <summary>
	/// The consumer-facing contract from the original defect report: the documented complete setup
	/// must actually deliver every feature it enables.
	/// </summary>
	[Fact]
	public void PlaceEveryEnabledFeatureInThePipeline_ForTheDocumentedCompleteSetup()
	{
		var services = Compose(static s => s.AddDispatchSecurityMiddleware(static options =>
		{
			options.Encryption.EnableEncryption = true;
			options.Signing.EnableSigning = true;
			options.RateLimiting.EnableRateLimiting = true;
			options.Authentication.EnableAuthentication = true;
			options.Authentication.JwtIssuer = TestIssuer;
			options.Authentication.JwtAudience = TestAudience;
			options.Authentication.JwtSigningKey = TestSigningKey;
		}));

		using var provider = services.BuildServiceProvider();
		var resolved = ResolvedMiddlewareTypes(provider);

		resolved.ShouldContain(typeof(JwtAuthenticationMiddleware));
		resolved.ShouldContain(typeof(RateLimitingMiddleware));
		resolved.ShouldContain(typeof(MessageEncryptionMiddleware));
		resolved.ShouldContain(typeof(MessageSigningMiddleware));
	}

	/// <summary>
	/// <c>Use*</c> means "add to the pipeline" in every .NET pipeline API. A <c>Use*</c> that adds
	/// nothing and returns the builder reads as correct to every reviewer, which is exactly how the
	/// original defect survived. When there is nothing to add, it must fail loud.
	/// </summary>
	[Fact]
	public void FailLoudly_WhenUseSecurityMiddlewareWouldAddNothing()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		var exception = Should.Throw<InvalidOperationException>(() => builder.UseSecurityMiddleware());

		exception.Message.ShouldContain("no security feature has been composed");
		// The message must tell the consumer that ordering is load-bearing: features composed after
		// AddDispatch(...) returns can never reach the pipeline.
		exception.Message.ShouldContain("BEFORE AddDispatch");
	}

	/// <summary>
	/// The liveness counterpart: with a feature composed, the call succeeds and chains.
	/// Without this arm, "throw always" would satisfy the safety arm above.
	/// </summary>
	[Fact]
	public void ReturnTheBuilder_WhenAtLeastOneSecurityFeatureIsComposed()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services)
			.Returns(Compose(static services => services.AddRateLimiting(static o => o.Enabled = true)));

		builder.UseSecurityMiddleware().ShouldBe(builder);
	}

	// ---------------------------------------------------------------------------------------------
	// BUILDER PATH.
	//
	// Everything above exercises the DI-enumerable path: AddDispatch() with no builder configuration
	// materializes DispatchPipeline from GetServices<IDispatchMiddleware>(). The builder path is a
	// DIFFERENT mechanism -- DispatchBuilder.Build() REPLACES IDispatchPipeline and
	// IDispatchMiddlewareInvoker with a pipeline composed only from middleware registered on the
	// builder, and never consults the DI enumerable. A fix verified on one path says nothing about
	// the other, which is the shape of the original defect.
	//
	// Build() appends a Scoped descriptor for every type it accepted as global middleware, so that
	// descriptor is the observable signal that UseSecurityMiddleware() actually placed the middleware
	// into the builder-materialized pipeline.
	//
	// ValidateScopes is OFF here deliberately. With it ON, Build() throws "Cannot resolve scoped
	// service ... from root provider" -- a PRE-EXISTING framework defect reproducible with the
	// framework's own builder.UseMiddleware<T>() and nothing from this package. See the report.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public void PlaceJwtAuthenticationInTheBuilderPipeline() =>
		AssertBuilderPipelineWiring(
			static services => services.AddJwtAuthentication(static o =>
			{
				o.Enabled = true;
				o.Credentials.ValidIssuer = TestIssuer;
				o.Credentials.ValidAudience = TestAudience;
				o.Credentials.SigningKey = TestSigningKey;
			}),
			typeof(JwtAuthenticationMiddleware));

	[Fact]
	public void PlaceRateLimitingInTheBuilderPipeline() =>
		AssertBuilderPipelineWiring(
			static services => services.AddRateLimiting(static o => o.Enabled = true),
			typeof(RateLimitingMiddleware));

	[Fact]
	public void PlaceMessageEncryptionInTheBuilderPipeline() =>
		AssertBuilderPipelineWiring(
			static services => services.AddMessageEncryption(static o => o.Enabled = true),
			typeof(MessageEncryptionMiddleware));

	[Fact]
	public void PlaceMessageSigningInTheBuilderPipeline() =>
		AssertBuilderPipelineWiring(
			static services => services.AddMessageSigning(static o => o.Enabled = true),
			typeof(MessageSigningMiddleware));

	[Fact]
	public void PlaceInputValidationInTheBuilderPipeline() =>
		AssertBuilderPipelineWiring(
			static services => services.AddInputValidation(EmptyConfiguration()),
			typeof(InputValidationMiddleware));

	/// <summary>
	/// Liveness: composing the feature and calling <c>UseSecurityMiddleware()</c> places the
	/// middleware in the builder-materialized pipeline, and that pipeline builds.
	/// Safety: composing the feature WITHOUT calling it leaves the middleware out -- which is the
	/// original defect, and the arm that fails if UseSecurityMiddleware ever goes inert again.
	/// </summary>
	private static void AssertBuilderPipelineWiring(Action<IServiceCollection> composeFeature, Type expected)
	{
		// LIVENESS -- feature composed, UseSecurityMiddleware called.
		var wired = Compose(composeFeature);
		_ = wired.AddDispatch(static b => b.UseSecurityMiddleware());

		using (var provider = wired.BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false }))
		{
			// The builder-materialized pipeline resolves -- Build() did not reject the middleware.
			provider.GetRequiredService<IDispatchMiddlewareInvoker>().ShouldNotBeNull();
		}

		AcceptedAsGlobalMiddleware(wired, expected).ShouldBeTrue(
			$"{expected.Name} was composed and UseSecurityMiddleware() was called, but the builder " +
			"pipeline did not accept it -- the middleware is inert on the builder path.");

		// SAFETY -- same feature composed, UseSecurityMiddleware NOT called.
		var unwired = Compose(composeFeature);
		_ = unwired.AddDispatch(static _ => { });

		AcceptedAsGlobalMiddleware(unwired, expected).ShouldBeFalse(
			$"{expected.Name} reached the builder pipeline without UseSecurityMiddleware() being " +
			"called, so the test cannot tell a working Use* from an inert one.");
	}

	/// <summary>
	/// DispatchBuilder.Build() appends a Scoped descriptor for each type it took as global
	/// middleware. The security features register their concrete types as Singleton or Transient, so
	/// a Scoped descriptor for the type can only have come from the builder accepting it.
	/// </summary>
	private static bool AcceptedAsGlobalMiddleware(IServiceCollection services, Type middleware) =>
		services.Any(d => d.ServiceType == middleware && d.Lifetime == ServiceLifetime.Scoped);

	private void AssertPipelineContainesExactlySigning() =>
		AssertPipelineContainsExactly(
			Compose(static services => services.AddMessageSigning(static o => o.Enabled = true)),
			typeof(MessageSigningMiddleware));

	private void AssertPipelineContainsExactly(IServiceCollection services, Type expected)
	{
		using var provider = services.BuildServiceProvider();

		var resolved = ResolvedMiddlewareTypes(provider);

		// LIVENESS: the feature the host composed actually reaches the pipeline.
		resolved.ShouldContain(
			expected,
			$"{expected.Name} was composed but is not in the resolved pipeline — the middleware is inert.");

		// SAFETY: composing one feature must not enable the others.
		foreach (var other in AllSecurityMiddleware.Where(t => t != expected))
		{
			resolved.ShouldNotContain(
				other,
				$"{other.Name} was never composed but reached the pipeline.");
		}
	}

	private static List<Type> ResolvedMiddlewareTypes(IServiceProvider provider) =>
		[.. provider.GetServices<IDispatchMiddleware>().Select(m => m.GetType())];

	/// <summary>
	/// Builds a service collection carrying only the collaborators the security features expect a host
	/// to supply, so each test composes exactly one feature and nothing more.
	/// </summary>
	private static IServiceCollection Compose(Action<IServiceCollection> composeFeature)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDataProtection();
		_ = services.AddSingleton(A.Fake<IKeyProvider>());
		_ = services.AddSingleton(A.Fake<ITelemetrySanitizer>());
		_ = services.AddSingleton(A.Fake<ISecurityEventLogger>());

		composeFeature(services);

		return services;
	}

	private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();
}
