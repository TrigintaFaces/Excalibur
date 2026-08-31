// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Locks that the strict profile a consumer actually GETS is the one carrying the security middleware.
/// </summary>
/// <remarks>
/// <para>
/// The registry auto-selects on <c>IsStrict</c>. The shipped strict profile did not set it, so the
/// profile carrying the Required Authentication, Authorization, TenantIdentity, InputSanitization and
/// Throttling entries could never be selected, and an action fell through to a profile that declares no
/// security boundary at all. That is a selection failure, not a naming nit.
/// </para>
/// <para>
/// WHY THIS ASSERTS THE SELECTED PROFILE'S CONTENT, NOT ITS FLAG. Asserting <c>IsStrict == true</c>
/// alone passes for an empty profile that merely carries the flag, which is the exact shape that
/// produced the defect. Only the middleware set proves the right object was selected.
/// </para>
/// <para>
/// PAIRED ARMS. A registry that returned the strict profile to everybody would satisfy the safety arm
/// while routing internal events through external-traffic security, so the safety arm is paired with a
/// liveness arm proving a non-strict message still resolves its own profile.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class StrictProfileAutoSelectionShould
{
	/// <summary>An action, which is the kind the registry answers with a strict profile.</summary>
	private sealed class ExternalAction : IDispatchAction;

	/// <summary>An event, which must keep resolving a non-strict profile.</summary>
	private sealed class InternalEvent : IDispatchEvent;

	[Fact]
	public void SelectTheProfileCarryingTheRequiredSecurityMiddlewareForAnAction()
	{
		var registry = new PipelineProfileRegistry();

		var selected = registry.SelectProfile(new ExternalAction());

		_ = selected.ShouldNotBeNull();

		var required = selected.MiddlewareEntries
			.Where(e => e.Criticality == MiddlewareCriticality.Required)
			.Select(e => e.MiddlewareType)
			.ToList();

		required.ShouldNotBeEmpty(
			"an action must resolve the profile that declares a security boundary, not one that declares none");
		required.ShouldContain(
			typeof(AuthenticationMiddleware),
			"the selected profile must be the one that refuses to build without authentication");
		required.ShouldContain(
			typeof(AuthorizationMiddleware),
			"the selected profile must be the one that refuses to build without authorization");
	}

	[Fact]
	public void StillResolveANonStrictProfileForAnEvent()
	{
		var registry = new PipelineProfileRegistry();

		var selected = registry.SelectProfile(new InternalEvent());

		_ = selected.ShouldNotBeNull();
		selected.IsStrict.ShouldBeFalse(
			"a registry that answered every message with the strict profile would satisfy the safety arm "
			+ "while routing internal events through external-traffic security");
	}
}
