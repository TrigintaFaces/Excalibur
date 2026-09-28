// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Configuration;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Every extension a consumer calls on the dispatch builder must be reachable from the same namespace
/// the builder's own entry point lives in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this exists to prevent.</b> A consumer writes
/// <c>services.AddDispatch(dispatch =&gt; dispatch.UseOutbox())</c>. <c>AddDispatch</c> is declared in
/// <c>Microsoft.Extensions.DependencyInjection</c>, which the Web SDK imports implicitly, so it needs no
/// using at all. If <c>UseOutbox</c> is declared somewhere else, it does not appear in IntelliSense
/// after the dot, the compiler reports only that the method does not exist, and the namespace to import
/// cannot be derived from the call site -- the consumer has to read our source to find it.
/// </para>
/// <para>
/// That was not hypothetical. Seating outbox staging on the default pipeline made <c>UseOutbox()</c>
/// MANDATORY for any host with an outbox store, and the shipped template then failed to compile against
/// the published feed for exactly this reason. At the time, 25 extension classes sat across 11
/// namespaces while the transports and stores already used the DI namespace.
/// </para>
/// <para>
/// <b>Why a reflection test rather than a compile-time one.</b> A scaffold that compiles proves the
/// property for the methods it happens to call; this asserts it for every extension in the assembly,
/// including ones added later by someone who never reads this file.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public sealed class PipelineExtensionsAreDiscoverableShould
{
	/// <summary>
	/// The namespace a consumer gets for free: it is where <c>AddDispatch</c> lives, and the Web SDK
	/// imports it implicitly, so anything declared here needs no using directive.
	/// </summary>
	private const string DiscoverableNamespace = "Microsoft.Extensions.DependencyInjection";

	/// <summary>
	/// The assembly that DECLARES the builder extensions, which is not the one that declares the
	/// interface: IDispatchBuilder lives in Excalibur.Dispatch.Abstractions while the extensions live in
	/// Excalibur.Dispatch. Anchored on a type known to sit beside them, so the query cannot quietly
	/// point at an assembly with no extensions in it -- which is exactly what the liveness arm below
	/// caught on this file's first run.
	/// </summary>
	private static Assembly ExtensionAssembly =>
		typeof(global::Microsoft.Extensions.DependencyInjection.DispatchServiceCollectionExtensions).Assembly;

	// SAFETY. RED the moment an extension on IDispatchBuilder is declared outside the namespace the
	// builder's entry point lives in -- which is the state that made a mandatory method undiscoverable
	// and broke the shipped template. Mutant: move any one of these classes back to a nested
	// Excalibur.Dispatch.* namespace and this arm names it.
	[Fact]
	public void DeclareEveryDispatchBuilderExtensionWhereAddDispatchLives()
	{
		var offenders = ExtensionAssembly
			.GetTypes()
			.Where(t => t is { IsSealed: true, IsAbstract: true, IsPublic: true })   // static class
			.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
			.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
			.Where(m => m.GetParameters().Length > 0
				&& m.GetParameters()[0].ParameterType == typeof(IDispatchBuilder))
			.Where(m => m.DeclaringType!.Namespace != DiscoverableNamespace)
			.Select(m => $"{m.DeclaringType!.Namespace}.{m.DeclaringType.Name}.{m.Name}")
			.Distinct()
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();

		offenders.ShouldBeEmpty(
			"every extension on IDispatchBuilder must be declared in " + DiscoverableNamespace
			+ ", where AddDispatch is declared and which the Web SDK imports implicitly. An extension "
			+ "declared elsewhere does not appear in IntelliSense after the builder's dot, and the "
			+ "consumer cannot derive the missing using from the call site. Offenders: "
			+ string.Join(", ", offenders));
	}

	// LIVENESS, and it is what stops the arm above passing because the query found nothing. A filter
	// that matched no methods at all would report an empty offender list and look identical to success.
	[Fact]
	public void FindTheExtensionsItClaimsToBeChecking()
	{
		var extensions = ExtensionAssembly
			.GetTypes()
			.Where(t => t is { IsSealed: true, IsAbstract: true, IsPublic: true })
			.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
			.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
			.Count(m => m.GetParameters().Length > 0
				&& m.GetParameters()[0].ParameterType == typeof(IDispatchBuilder));

		extensions.ShouldBeGreaterThan(
			20,
			"this assembly declares dozens of builder extensions. A count at or near zero means the "
			+ "reflection query stopped matching them -- at which point the safety arm above is "
			+ "asserting emptiness over an empty set and can never fail");
	}
}
