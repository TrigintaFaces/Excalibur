// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Security;

/// <summary>
/// Contract lock for the security composition surface: a caller receives the components it named and
/// nothing else, and the two overloads compose the same set for the same settings.
/// </summary>
/// <remarks>
/// <para>
/// Both overloads used to compose more than the caller asked for, by two different mechanisms. The
/// delegate overload gated each component on an <c>Enable</c> flag that defaulted to on, so configuring
/// encryption also produced rate limiting and JWT authentication with a null signing key — the delegate
/// read as configuration and behaved as "turn the whole stack on". The configuration overload composed
/// encryption without consulting a setting at all, so a host that had disabled it got it anyway.
/// </para>
/// <para>
/// The arms below are stated as absences because an absence is what the defect violated. The positive
/// arm keeps them honest: composition still works, and the named component still reaches the resolved
/// pipeline, so a change that composed nothing at all would fail here rather than pass everywhere.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityCompositionIsOptInPerFeatureShould
{
	[Fact]
	public void ComposeOnlyEncryption_WhenOnlyEncryptionIsNamed()
	{
		var services = Host();

		_ = services.AddDispatchSecurityMiddleware(options => options.Encryption.EnableEncryption = true);

		MiddlewareIn(services).ShouldBe([typeof(MessageEncryptionMiddleware)]);
	}

	[Fact]
	public void ComposeOnlyRateLimiting_WhenOnlyRateLimitingIsNamed()
	{
		var services = Host();

		_ = services.AddDispatchSecurityMiddleware(options => options.RateLimiting.EnableRateLimiting = true);

		MiddlewareIn(services).ShouldBe([typeof(RateLimitingMiddleware)]);
	}

	[Fact]
	public void ComposeNothing_WhenTheDelegateNamesNothing()
	{
		var services = Host();

		_ = services.AddDispatchSecurityMiddleware(_ => { });

		MiddlewareIn(services).ShouldBeEmpty();
	}

	[Fact]
	public void ComposeNothing_WhenTheConfigurationEnablesNothing()
	{
		var services = Host();

		_ = services.AddDispatchSecurityMiddleware(Configuration([]));

		MiddlewareIn(services).ShouldBeEmpty();
	}

	[Fact]
	public void ComposeTheSameSet_ForTheSameSettings_ThroughEitherOverload()
	{
		var viaDelegate = Host();
		_ = viaDelegate.AddDispatchSecurityMiddleware(options => options.Encryption.EnableEncryption = true);

		var viaConfiguration = Host();
		_ = viaConfiguration.AddDispatchSecurityMiddleware(
			Configuration(new Dictionary<string, string?> { ["Security:Encryption:Enabled"] = "true" }));

		MiddlewareIn(viaConfiguration).ShouldBe(MiddlewareIn(viaDelegate));
	}

	[Fact]
	public void ResolveTheNamedComponent_FromTheRealPipeline()
	{
		var services = Host();
		_ = services.AddDispatchSecurityMiddleware(options => options.Encryption.EnableEncryption = true);

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IDispatchMiddleware>().Select(middleware => middleware.GetType())
			.ShouldContain(typeof(MessageEncryptionMiddleware));
	}

	private static ServiceCollection Host()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		return services;
	}

	private static IConfiguration Configuration(Dictionary<string, string?> values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values).Build();

	private static Type[] MiddlewareIn(IServiceCollection services) =>
		[.. services
			.Where(descriptor => descriptor.ServiceType == typeof(IDispatchMiddleware))
			.Select(descriptor => descriptor.ImplementationType!)
			.OrderBy(type => type.FullName, StringComparer.Ordinal)];
}
