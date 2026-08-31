// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Registration locks for the <c>AddDispatchAspNetCore</c> one-liner.
/// </summary>
/// <remarks>
/// This metapackage documents the dispatcher, observability and request-scope integration — and, unlike
/// its persistence siblings, claims no outbox. <see cref="NotRegisterAnOutboxStore"/> holds it to that:
/// the same doc-versus-body divergence is a defect in whichever direction it points.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class DispatchAspNetCoreMetapackageShould : UnitTestBase
{
	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchAspNetCore();
		return services.BuildServiceProvider();
	}

	[Fact]
	public void RegisterTheDispatcher()
	{
		using var provider = BuildProvider();

		provider.GetService<IDispatcher>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterTheRequestScopeIntegration()
	{
		// The "scoped handlers resolve from the active request scope" half of the documented promise.
		using var provider = BuildProvider();

		provider.GetService<IDispatchAmbientScopeAccessor>().ShouldNotBeNull();
	}

	[Fact]
	public void NotRegisterAnOutboxStore()
	{
		// Documentation honesty in the other direction: this package promises no persistence, so it must
		// not quietly acquire an outbox either.
		using var provider = BuildProvider();

		provider.GetService<IOutboxStore>().ShouldBeNull();
	}
}
