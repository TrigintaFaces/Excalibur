// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// The structural check 2lxfic asks for: an <c>AddDispatchXxx</c> method that owns a middleware must
/// register that middleware where the composed pipeline can find it. One table row per such method;
/// add a row when a new <c>AddDispatchXxx</c> is given ownership of a middleware, and this test fails
/// until its registration reaches <see cref="IDispatchMiddleware"/> the way
/// <c>AddOrderingValidation()</c> already does.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a DI-presence check, not a full dispatch: it proves the exact mechanism the bug lived
/// in -- <c>DispatchBuilder.BuildPipeline</c> unions middleware from
/// <c>serviceProvider.GetServices&lt;IDispatchMiddleware&gt;()</c> -- so a middleware only reachable via
/// <c>UseXxx()</c>'s <c>UseMiddleware&lt;T&gt;()</c> call (never via the bare <c>AddDispatchXxx()</c>
/// form) is invisible here exactly as it was invisible to the real pipeline. The stronger,
/// full-dispatch proof for the bead's headline case lives beside this file:
/// <see cref="Validation.AddDispatchValidationMiddlewareWiringShould"/>.
/// </para>
/// <para>
/// Composed through <c>AddDispatch(configure: null)</c> -- the modern, non-scanning path -- because
/// that is the path bgu603 found silently dropped this exact registration before its own fix.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public sealed class AddDispatchXxxMiddlewareReachesThePipelineShould
{
	// AddDispatchXxx method name -> the middleware it owns. Add a row here when a new AddDispatchXxx is
	// given ownership of a middleware.
	//
	// The AddDispatchCaching case (CachingMiddlewareWrapper / CacheInvalidationMiddlewareWrapper) lives
	// beside its feature in Excalibur.Dispatch.Caching.Tests -- this project does not reference that
	// package, and adding the reference for two rows is not worth it.
	public static TheoryData<string, Type> Cases() =>
		new()
		{
			{ "AddDispatchValidation", typeof(ValidationMiddleware) },
			{ "AddDispatchThreading", typeof(Excalibur.Dispatch.Threading.BackgroundExecutionMiddleware) },
			// AddDispatchThreading has two overloads and each carries its OWN copy of the registration
			// rather than funnelling through one shared method the way the caching entry points do, so
			// one row cannot speak for both. Measured: deleting the registration from the IConfiguration
			// overload left this whole suite green, so that overload could advertise background execution
			// and never run it -- the exact defect this test exists to catch. A row per overload.
			{ "AddDispatchThreading(IConfiguration)", typeof(Excalibur.Dispatch.Threading.BackgroundExecutionMiddleware) },
		};

	[Theory]
	[MemberData(nameof(Cases))]
	public void RegisterItsOwnedMiddlewareAsIDispatchMiddleware(string addMethodName, Type expectedMiddlewareType)
	{
		// Arrange: the exact shape the bead names -- the bare Add form beside AddDispatch(configure),
		// no UseXxx() call anywhere, so nothing but the Add form's own registration can wire it.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = addMethodName switch
		{
			"AddDispatchValidation" => services.AddDispatchValidation(),
			"AddDispatchThreading" => services.AddDispatchThreading(),
			"AddDispatchThreading(IConfiguration)" => services.AddDispatchThreading(new ConfigurationBuilder().Build()),
			_ => throw new ArgumentOutOfRangeException(
				nameof(addMethodName), addMethodName, "No invocation wired for this case in the switch above."),
		};

		_ = services.AddDispatch(configure: null);

		var provider = services.BuildServiceProvider();

		// Act: the exact union DispatchBuilder.BuildPipeline reads from.
		var registeredMiddlewareTypes = provider.GetServices<IDispatchMiddleware>().Select(static m => m.GetType()).ToList();

		// Assert
		registeredMiddlewareTypes.ShouldContain(
			expectedMiddlewareType,
			$"{addMethodName} owns {expectedMiddlewareType.Name} but did not register it as IDispatchMiddleware -- "
			+ "a consumer who called the bare Add form gets a clean build and this middleware never runs.");
	}
}
