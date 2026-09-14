// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Feature switches that let a trimmed application remove dispatch code paths it does not use.
/// </summary>
internal static class DispatchFeatureSwitches
{
	/// <summary>
	/// Gets a value indicating whether handler invocation may use reflection and runtime-compiled
	/// expression trees, rather than the source-generated invoker.
	/// </summary>
	/// <value>
	/// <see langword="true"/> unless the application sets the
	/// <c> Excalibur.Dispatch.UseReflectionInvoker </c> switch to <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// This exists because ahead-of-time publication and trimming are separate configurations and only
	/// the first of them removes the reflective path on its own. Publishing ahead-of-time makes dynamic
	/// code unavailable, and the reflective invoker is dropped from the application as unreachable.
	/// Publishing trimmed without it leaves dynamic code available, so the reflective invoker is kept —
	/// and the handler types it resolves at run time may already have been trimmed away.
	/// </para>
	/// <para>
	/// Setting this switch to <see langword="false"/> closes that gap: the reflective invoker becomes
	/// unreachable, the trimmer removes it along with everything only it needed, and dispatch runs
	/// entirely through the source-generated invoker. Handlers registered by name or discovered at
	/// build time are unaffected; only handlers that could not be seen at compile time are lost, and
	/// those cannot survive trimming in any case.
	/// </para>
	/// <para>
	/// The default preserves current behaviour, so an application that does not trim, or that trims
	/// without setting the switch, sees no change.
	/// </para>
	/// </remarks>
	// A note on what is NOT here, so nobody adds it and reverts the build:
	//
	//   [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
	//
	// would let the analyser treat this property as a guard and stop reporting inside the branch it
	// protects. It is rejected with IL4000 -- "Return value does not match FeatureGuardAttribute" --
	// because the attribute only accepts a property that FORWARDS a recognised guard directly. This
	// property cannot: it combines the platform's answer with the application's choice, and that
	// combination is the whole point of it. Verified both ways against the analyser: the direct-forward
	// shape is accepted, this one is not.
	[FeatureSwitchDefinition("Excalibur.Dispatch.UseReflectionInvoker")]
	internal static bool UseReflectionInvoker =>
		System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported
		&& (!AppContext.TryGetSwitch("Excalibur.Dispatch.UseReflectionInvoker", out var enabled) || enabled);
}
