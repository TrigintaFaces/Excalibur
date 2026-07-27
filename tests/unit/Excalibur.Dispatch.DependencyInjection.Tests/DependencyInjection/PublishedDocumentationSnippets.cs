// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;   // required for UseAuthorization()

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Holds, verbatim, the code snippets published in the pipeline-profiles documentation, so a
/// snippet cannot drift into code that does not compile. This is a permanent fixture, not a
/// scratch probe — deleting it removes the only check that the published samples build.
/// </summary>
/// <remarks>
/// <para>
/// WHAT THIS PROVES: the published snippet compiles — every API it names exists and
/// binds from the namespaces the snippet imports. It caught exactly that defect once:
/// the snippet omitted the <c>Excalibur.Dispatch.Middleware.Auth</c> import and would
/// not have compiled for a consumer who copied it.
/// </para>
/// <para>
/// WHAT THIS DOES *NOT* PROVE — read before citing it: it does NOT show the documented
/// path is safe. The dangerous custom-profile example this snippet replaced compiles
/// too, so compilation has no power to discriminate between them. The fail-closed
/// behaviour is proven separately, by
/// <c>PinTheDefect_SilentlyOmitAuthorizationWhenItsServiceIsUnregistered</c>, which is
/// RED against the pre-fix builder.
/// </para>
/// <para>
/// This is therefore a drift guard on the sample, not an acceptance test for the
/// documented behaviour. A single test asserting BOTH — the snippet compiles AND the
/// documented path refuses to build without its dependency — would supersede this file.
/// </para>
/// </remarks>
internal static class PublishedDocumentationSnippets
{
	internal static void TheDocumentedRedirectSnippet(IServiceCollection services)
	{
		// ---- BEGIN verbatim from the published "Creating Custom Profiles" snippet ----
		services.AddDispatch(dispatch =>
		{
			dispatch.ConfigurePipeline("my-custom-profile", pipeline => { /* … */ });

			// Required: if IAuthorizationService is not registered, the build fails and
			// names that service, instead of producing a pipeline without authorization.
			dispatch.UseAuthorization();
		});
		// ---- END verbatim ----
	}
}
