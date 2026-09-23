// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.PolicyData;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.A3;

/// <summary>
/// Pins <c>AddExcaliburA3()</c> to the minimal-wiring idempotence contract: a second call must add
/// nothing, and must not change which implementation wins.
/// </summary>
/// <remarks>
/// <para>
/// A host that composes A3 from two modules, or calls it after a composite extension already did,
/// calls this twice. Duplicate descriptors on the authorization seams are not merely wasteful:
/// <see cref="IAuthorizationPolicyProvider"/> decides authorization policy, and a duplicate changes
/// what an <c>IEnumerable</c> resolve or a descriptor count sees.
/// </para>
/// <para>
/// The drift is measured as a multiset difference over descriptors rather than by comparing
/// <see cref="IServiceCollection.Count"/> across a positional window. <c>AddTenantContext()</c>
/// registers through <c>Replace</c>, which removes one descriptor and appends another; a positional
/// window reports the appended copy as an addition and misattributes the drift to whatever the shift
/// pushed into view.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Registration")]
public sealed class A3ServiceCollectionExtensionsIdempotenceShould
{
	/// <summary>
	/// Every contract <c>AddExcaliburA3()</c> is the sole intended registrar of, or deliberately
	/// displaces a lighter <c>AddExcaliburA3Core()</c> registration on. None is resolved as an
	/// <c>IEnumerable</c> anywhere in the tree, so exactly one descriptor is the contract.
	/// </summary>
	private static readonly Type[] SingleDescriptorContracts =
	[
		typeof(Activities),
		typeof(ActivityGroups),
		typeof(UserGrants),
		typeof(IAuthorizationPolicy),
		typeof(IAuthorizationPolicyProvider),
		typeof(IActivityGroupService),
		typeof(IAuthenticationTokenProvider),
		typeof(ITenantContext),
	];

	private static ServiceCollection Compose(int calls)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging(static b => b.AddProvider(NullLoggerProvider.Instance));
		_ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

		for (var i = 0; i < calls; i++)
		{
#pragma warning disable IL2026, IL3050
			_ = services.AddExcaliburA3();
#pragma warning restore IL2026, IL3050
		}

		return services;
	}

	private static string DescriptorKey(ServiceDescriptor descriptor)
	{
		var implementation = descriptor.ImplementationType?.FullName
			?? (descriptor.ImplementationInstance is { } instance ? "instance:" + instance.GetType().FullName : "<factory>");
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{descriptor.Lifetime} {descriptor.ServiceType.FullName} -> {implementation}");
	}

	private static bool IsBenignOptionsDescriptor(ServiceDescriptor descriptor)
	{
		if (!descriptor.ServiceType.IsGenericType)
		{
			return false;
		}

		var definition = descriptor.ServiceType.GetGenericTypeDefinition();
		return definition == typeof(IConfigureOptions<>)
			|| definition == typeof(IPostConfigureOptions<>)
			|| definition == typeof(IValidateOptions<>);
	}

	private static Dictionary<string, int> Census(IServiceCollection services)
	{
		var census = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var descriptor in services)
		{
			if (IsBenignOptionsDescriptor(descriptor))
			{
				continue;
			}

			var key = DescriptorKey(descriptor);
			census[key] = census.TryGetValue(key, out var count) ? count + 1 : 1;
		}

		return census;
	}

	[Fact]
	public void RegisterExactlyOneDescriptorPerSingleDescriptorContract()
	{
		var services = Compose(2);

		foreach (var contract in SingleDescriptorContracts)
		{
			var count = services.Count(descriptor => descriptor.ServiceType == contract);
			count.ShouldBe(
				1,
				$"{contract.FullName} must have exactly one descriptor after two AddExcaliburA3() calls.");
		}
	}

	[Fact]
	public void AddNothingNonBenignOnASecondCall()
	{
		var once = Census(Compose(1));
		var twice = Census(Compose(2));

		var drift = twice
			.Where(entry => !once.TryGetValue(entry.Key, out var before) || before != entry.Value)
			.Select(entry => $"  {(once.TryGetValue(entry.Key, out var b) ? b : 0)} -> {entry.Value}   {entry.Key}")
			.OrderBy(static line => line, StringComparer.Ordinal)
			.ToList();

		drift.ShouldBeEmpty(
			"a second AddExcaliburA3() call must be a no-op outside the MS-standard options families."
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, drift));
	}

	[Fact]
	public void StillLetTheFullStackImplementationsWinAfterASecondCall()
	{
		// Liveness. Idempotence achieved by deferring to AddExcaliburA3Core()'s lighter registrations
		// would satisfy the two arms above while silently demoting the full-stack composition -- the
		// cached policy provider is the one A3.Core registers its own as a placeholder for.
		var services = Compose(2);

		Last(services, typeof(IAuthorizationPolicyProvider)).ImplementationType
			.ShouldBe(typeof(AuthorizationPolicyProvider));
		Last(services, typeof(ActivityGroups)).Lifetime.ShouldBe(ServiceLifetime.Transient);
		Last(services, typeof(UserGrants)).Lifetime.ShouldBe(ServiceLifetime.Transient);

		// The tenant context is chosen by a resolver, not by which descriptor is last, so assert what resolves.
		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<ITenantContext>().GetType().Name.ShouldBe("AmbientTenantContext");

		static ServiceDescriptor Last(IServiceCollection services, Type serviceType) =>
			services.Last(descriptor => descriptor.ServiceType == serviceType);
	}
}
