// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;
using Excalibur.Dispatch.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.ContentNegotiation;

/// <summary>
/// Locks that content-negotiation formatters use the APPLICATION's serializer registry.
/// </summary>
/// <remarks>
/// <para>
/// The defect these arms bind: <c>AddDispatchContentNegotiation</c> used to call
/// <c>builder.Services.BuildServiceProvider()</c> inside its <c>AddMvcOptions</c> callback and resolve
/// <see cref="ISerializerRegistry" /> from that. A second root container resolves its own singleton, so
/// the formatters held a registry that was not the application's, and the second provider owned every
/// disposable singleton it created for the process lifetime with nobody to dispose it.
/// </para>
/// <para>
/// Reference identity is the direct expression of that defect, which is why these arms assert on the
/// instance rather than on a symptom. The formatter captures the registry in its constructor, so the
/// field read here is the one the formatter will actually serialize through.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchContentNegotiationRegistryIdentityShould : UnitTestBase
{
	/// <summary>
	/// SAFETY: the OUTPUT formatter must hold the application's registry, not one from a second container.
	/// </summary>
	[Fact]
	public void GiveTheOutputFormatterTheApplicationsRegistry()
	{
		using var provider = BuildProvider();

		var applicationRegistry = provider.GetRequiredService<ISerializerRegistry>();
		var formatter = ResolveFormatters(provider)
			.OfType<DispatchOutputFormatter>()
			.ShouldHaveSingleItem();

		RegistryOf(formatter).ShouldBeSameAs(
			applicationRegistry,
			"a registry that is merely EQUAL is not enough — a second root container produces a "
			+ "distinct instance, and that is the defect");
	}

	/// <summary>
	/// SAFETY: same requirement for the INPUT formatter. Both were constructed from the same second
	/// provider, so fixing one and not the other would leave half the defect in place.
	/// </summary>
	[Fact]
	public void GiveTheInputFormatterTheApplicationsRegistry()
	{
		using var provider = BuildProvider();

		var applicationRegistry = provider.GetRequiredService<ISerializerRegistry>();
		var formatter = ResolveInputFormatters(provider)
			.OfType<DispatchInputFormatter>()
			.ShouldHaveSingleItem();

		RegistryOf(formatter).ShouldBeSameAs(applicationRegistry);
	}

	/// <summary>
	/// LIVENESS: the formatters are actually inserted, and at the FRONT. Without this the arms above
	/// are satisfied by a configuration that adds no formatters at all — zero formatters trivially
	/// hold no wrong registry.
	/// </summary>
	[Fact]
	public void InsertBothFormattersAtTheFrontOfThePipeline()
	{
		using var provider = BuildProvider();

		ResolveFormatters(provider)[0].ShouldBeOfType<DispatchOutputFormatter>(
			"the Dispatch output formatter must win content negotiation over the defaults");
		ResolveInputFormatters(provider)[0].ShouldBeOfType<DispatchInputFormatter>();
	}

	/// <summary>
	/// SAFETY: the registry factory runs EXACTLY ONCE. This is the direct expression of the second
	/// outcome — the leak — which reference identity alone does not reach.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A second root container resolves its own singletons, so it runs the registry factory a second
	/// time and then OWNS everything that factory created, for the process lifetime, with nobody to
	/// dispose it. Counting factory invocations detects that whether or not the two registries happen to
	/// compare equal, and it detects it even if a future change makes the formatters read the right
	/// instance while still building a container to find it.
	/// </para>
	/// <para>
	/// The counting registry is registered BEFORE <c>AddPluggableSerialization</c>, whose own
	/// registration is <c>TryAddSingleton</c> and therefore defers to it.
	/// </para>
	/// </remarks>
	[Fact]
	public void ResolveTheRegistryExactlyOnce()
	{
		var factoryCalls = 0;

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ISerializerRegistry>(_ =>
		{
			factoryCalls++;
			return new CountingRegistry();
		});
		_ = services.AddPluggableSerialization();
		_ = services.AddControllers().AddDispatchContentNegotiation();

		using var provider = services.BuildServiceProvider();

		// The APPLICATION resolves the registry first, as any real host does — the dispatcher and the
		// outbox all hold it. Without this the arm cannot discriminate: a second container's resolution
		// would simply be the first call anyone made, and the count would be 1 under the defect too.
		_ = provider.GetRequiredService<ISerializerRegistry>();

		// Materialise MvcOptions — this is the moment the formatters are built.
		_ = provider.GetRequiredService<IOptions<MvcOptions>>().Value.OutputFormatters;

		factoryCalls.ShouldBe(
			1,
			"a second call means a second root container built its own registry and now owns every "
			+ "disposable it created, for the process lifetime, with nothing to dispose it");
	}

	#region Helpers

	/// <summary>
	/// A registry that exists only to be counted. Contributing no serializers keeps this arm about
	/// INSTANTIATION and nothing else.
	/// </summary>
	private sealed class CountingRegistry : ISerializerRegistry
	{
		public (byte Id, ISerializer Serializer) GetCurrent() =>
			throw new InvalidOperationException("no current serializer is configured");

		public void Register(byte id, ISerializer serializer) { }

		public void SetCurrent(string serializerName) { }

		public ISerializer? GetById(byte id) => null;

		public IReadOnlyCollection<(byte Id, string Name, ISerializer Serializer)> GetAll() => [];
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddControllers().AddDispatchContentNegotiation();
		return services.BuildServiceProvider();
	}

	private static IList<Microsoft.AspNetCore.Mvc.Formatters.IOutputFormatter> ResolveFormatters(
		IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<MvcOptions>>().Value.OutputFormatters;

	private static IList<Microsoft.AspNetCore.Mvc.Formatters.IInputFormatter> ResolveInputFormatters(
		IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

	private static ISerializerRegistry RegistryOf(object formatter)
	{
		var field = formatter.GetType().GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException(
				$"{formatter.GetType().Name} no longer has a _registry field; this arm needs updating.");

		return (ISerializerRegistry)(field.GetValue(formatter)
			?? throw new InvalidOperationException("the formatter captured a null registry"));
	}

	#endregion
}
