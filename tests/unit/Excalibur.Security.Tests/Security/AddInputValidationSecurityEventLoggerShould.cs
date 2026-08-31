// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Security;

/// <summary>
/// Contract lock for the input-validation middleware's audit sink: it is telemetry for the control,
/// not the control itself, so its absence degrades the record and never breaks the container.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InputValidationMiddleware"/> writes validation failures to
/// <see cref="ISecurityEventLogger"/>, which only the security-auditing registration supplies. When it
/// was a required dependency, a host that composed input validation on its own got a middleware in
/// <c>IEnumerable&lt;IDispatchMiddleware&gt;</c> that could not be activated, so enumerating the
/// pipeline took the whole pipeline down over a missing log destination.
/// </para>
/// <para>
/// The arms below compose through the production registration path and resolve, because the existing
/// wiring tests supply a fake logger and therefore cannot see this: the defect is in what the container
/// can build, not in what the middleware does once built. The contrast with signing key material is
/// deliberate and is the line these two locks draw together: missing key material for a security
/// control fails loud, a missing sink for its telemetry does not.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AddInputValidationSecurityEventLoggerShould
{
	[Fact]
	public void ResolveTheMiddleware_WhenNoSecurityEventLoggerIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInputValidation(EmptyConfiguration());

		using var provider = services.BuildServiceProvider();

		provider.GetService<ISecurityEventLogger>().ShouldBeNull(
			"the arm is only meaningful while nothing supplies the audit sink");

		// Enumerating is what activates the middleware. Pre-fix this threw on the unresolvable
		// ISecurityEventLogger, so composing input validation alone cost the host every middleware.
		provider.GetServices<IDispatchMiddleware>().Select(m => m.GetType())
			.ShouldContain(typeof(InputValidationMiddleware));
	}

	[Fact]
	public void UseTheRegisteredSecurityEventLogger_WhenTheHostSuppliesOne()
	{
		var securityEventLogger = A.Fake<ISecurityEventLogger>();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(securityEventLogger);
		_ = services.AddInputValidation(EmptyConfiguration());

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IDispatchMiddleware>().Select(m => m.GetType())
			.ShouldContain(typeof(InputValidationMiddleware));
		provider.GetRequiredService<ISecurityEventLogger>().ShouldBeSameAs(securityEventLogger);
	}

	private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();
}
