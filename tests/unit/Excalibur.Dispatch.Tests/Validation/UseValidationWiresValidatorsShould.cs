// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
/// Author≠impl engage-test for S859 ADR-336 · <c>bd-jv02p5</c> — the canonical
/// <see cref="ValidationDispatchBuilderExtensions.UseValidation(IDispatchBuilder)"/> must wire the
/// <b>validator path</b> (register <see cref="IValidationService"/> via <c>AddDispatchValidation()</c>),
/// NOT merely the middleware. The middleware-only
/// <see cref="ValidationPipelineExtensions.UseValidationMiddleware(IDispatchBuilder)"/> registers the
/// middleware ALONE — the "silent-pass" variant that validates nothing.
/// </summary>
/// <remarks>
/// Before <c>jv02p5</c> there were two identical-signature <c>UseValidation</c> overloads (CS0121), and
/// the one bound by <c>WithDefaults()</c> was the silent-pass middleware-only variant — so "validation"
/// was advertised-but-dead. <b>RED on a regression</b> where canonical <c>UseValidation</c> degrades to
/// middleware-only (drops <c>AddDispatchValidation()</c>): the first fact would fail because
/// <see cref="IValidationService"/> would no longer be registered. The contrast fact pins the distinction
/// so the two methods cannot silently re-converge.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UseValidationWiresValidatorsShould
{
	[Fact]
	public void RegisterTheValidationServicePath_OnUseValidation()
	{
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		_ = builder.UseValidation();

		services.ShouldContain(
			d => d.ServiceType == typeof(Excalibur.Dispatch.Middleware.Validation.IValidationService),
			"canonical UseValidation must wire the validator path (AddDispatchValidation registers IValidationService), not just the middleware");
	}

	[Fact]
	public void NotRegisterTheValidationServicePath_OnUseValidationMiddlewareOnly()
	{
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		_ = builder.UseValidationMiddleware();

		services.ShouldNotContain(
			d => d.ServiceType == typeof(Excalibur.Dispatch.Middleware.Validation.IValidationService),
			"the middleware-only UseValidationMiddleware must NOT register the validator path — it is the explicit middleware-only (silent-pass) variant, distinct from canonical UseValidation");
	}
}
