// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Verifies that dispatch under Native AOT resolves result factories only from the source-generated
/// registry, and fails fast naming the type when one is missing, rather than falling back to the
/// reflective factory.
/// </summary>
/// <remarks>
/// The AOT decision is passed as a parameter because <c>RuntimeFeature.IsDynamicCodeSupported</c> is a
/// JIT-folded constant that cannot be flipped on a test host.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Priority", "0")]
public sealed class AotResultFactoryFailFastShould
{
	/// <summary>
	/// Never appears as a type argument of <c>MessageResult&lt;T&gt;</c>, <c>IDispatchAction&lt;T&gt;</c>,
	/// or <c>IActionHandler&lt;,&gt;</c> anywhere in this assembly, so the source generator cannot discover
	/// and register it. That is what makes it a genuine registry miss.
	/// </summary>
	private sealed class UnregisteredResult
	{
		public int Value { get; init; }
	}

	private sealed class RegisteredResult
	{
		public int Value { get; init; }
	}

	private static MessageContext PlainDispatchContext(object result) => new() { Result = result };

	private static MessageContext RoutedDispatchContext(object result)
	{
		var context = new MessageContext { Result = result };
		context.CachedRoutingDecision = RoutingDecision.Local;
		return context;
	}

	#region Fail-fast (safety)

	[Fact]
	public void FailFast_OnPlainDispatch_WhenResultTypeHasNoRegisteredFactory()
	{
		// The plain dispatch case: no routing, validation, or authorization result. This is the path
		// that previously reached Expression.Compile before the AOT registry was ever consulted.
		var context = PlainDispatchContext(new UnregisteredResult { Value = 7 });

		var exception = Should.Throw<InvalidOperationException>(
			() => FinalDispatchHandler.CreateTypedResult(context, actionResultType: null, dynamicCodeSupported: false));

		exception.Message.ShouldContain(typeof(UnregisteredResult).FullName!);
		exception.Message.ShouldContain("RegisterFactory");
	}

	[Fact]
	public void FailFast_OnRoutedDispatch_WhenResultTypeHasNoRegisteredFactory()
	{
		var context = RoutedDispatchContext(new UnregisteredResult { Value = 9 });

		var exception = Should.Throw<InvalidOperationException>(
			() => FinalDispatchHandler.CreateTypedResult(context, actionResultType: null, dynamicCodeSupported: false));

		exception.Message.ShouldContain(typeof(UnregisteredResult).FullName!);
	}

	#endregion

	#region Registered types still dispatch (liveness)

	[Fact]
	public void DispatchNormally_OnPlainDispatch_WhenResultTypeIsRegistered()
	{
		ResultFactoryRegistry.RegisterFactory<RegisteredResult>();
		var context = PlainDispatchContext(new RegisteredResult { Value = 42 });

		var result = FinalDispatchHandler.CreateTypedResult(context, actionResultType: null, dynamicCodeSupported: false);

		result.Succeeded.ShouldBeTrue();
		var typed = result.ShouldBeOfType<SimpleSuccessMessageResultOfT<RegisteredResult>>();
		typed.ReturnValue!.Value.ShouldBe(42);
	}

	[Fact]
	public void DispatchNormally_OnRoutedDispatch_WhenResultTypeIsRegistered()
	{
		ResultFactoryRegistry.RegisterFactory<RegisteredResult>();
		var context = RoutedDispatchContext(new RegisteredResult { Value = 43 });

		var result = FinalDispatchHandler.CreateTypedResult(context, actionResultType: null, dynamicCodeSupported: false);

		result.Succeeded.ShouldBeTrue();

		// A routing decision is present, so the full factory must have been used, not the lean one.
		result.ShouldNotBeOfType<SimpleSuccessMessageResultOfT<RegisteredResult>>();
	}

	/// <summary>
	/// A single <c>RegisterFactory&lt;T&gt;</c> call must cover both the plain and the routed path, so
	/// neither callers nor the source generator have to know which path a dispatch will take.
	/// </summary>
	[Fact]
	public void CoverBothPaths_FromASingleRegisterFactoryCall()
	{
		ResultFactoryRegistry.RegisterFactory<RegisteredResult>();

		ResultFactoryRegistry.GetFactory(typeof(RegisteredResult)).ShouldNotBeNull();
		ResultFactoryRegistry.GetLeanFactory(typeof(RegisteredResult)).ShouldNotBeNull();
	}

	#endregion

	#region JIT is unaffected

	[Fact]
	public void StillUseTheReflectiveFactory_OnJit_ForAnUnregisteredResultType()
	{
		// The same type that fails fast under AOT must keep working on JIT, where building a factory
		// at runtime is available.
		var context = PlainDispatchContext(new UnregisteredResult { Value = 11 });

		var result = FinalDispatchHandler.CreateTypedResult(context, actionResultType: null, dynamicCodeSupported: true);

		result.Succeeded.ShouldBeTrue();
		var typed = result.ShouldBeOfType<SimpleSuccessMessageResultOfT<UnregisteredResult>>();
		typed.ReturnValue!.Value.ShouldBe(11);
	}

	#endregion
}
