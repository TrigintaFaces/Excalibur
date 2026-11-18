// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Dispatch;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds both erasure background passes to the REAL container, through the production registration path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Constructing a processor with hand-supplied dependencies proves it works when handed them. It
/// proves nothing about whether the container can produce them.</b> Both passes are resolved by a
/// background service through <c>GetService</c>, so a registration that cannot be satisfied does not
/// throw at startup — the service silently finds nothing and the pass never runs. For the hold
/// re-evaluation pass that failure mode is the exact defect the pass exists to fix: a deferred erasure
/// never resumes, and nothing reports that it did not.
/// </para>
/// <para>
/// <b>Both passes are asserted together deliberately.</b> The completion pass had no resolve lock of its
/// own, and the two share a registration site and a resolution shape, so one arm covering the class is
/// worth more than one covering only the newer half.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheBackgroundPassesResolveFromTheRealContainerShould
{
	// RED if either registration is removed, or if a dependency either processor needs is not registered
	// by the production path -- which is the case GetService turns into silence rather than a failure.
	[Fact]
	public void Resolve_both_background_passes_through_the_production_registration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDefaultTenantContext();
		_ = services.AddGdprErasure();

		// The data-subject hasher is fail-closed: it refuses an unkeyed hash rather than falling back to
		// one, so a container without a pepper cannot produce a store. That is correct, and supplying one
		// here is fixture setup rather than a workaround.
		_ = services.AddDataSubjectHashing();
		_ = services.Configure<DataSubjectHashingOptions>(options =>
			options.Pepper = "test-pepper-0123456789abcdef0123456789ab");

		_ = services.AddInMemoryErasureStore();
		_ = services.AddInMemoryLegalHoldStore();
		_ = services.AddLegalHoldService();
		_ = services.AddInMemoryDataInventoryStore();
		_ = services.AddDataInventoryService();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		_ = scope.ServiceProvider.GetRequiredService<IErasureHoldReevaluationProcessor>().ShouldNotBeNull(
			"the scheduler resolves this pass with GetService, so an unsatisfiable registration is silent: "
			+ "deferred erasures would never return to the queue and nothing would report it");

		_ = scope.ServiceProvider.GetRequiredService<IErasureCompletionProcessor>().ShouldNotBeNull(
			"the same silence applies to the completion pass: requests awaiting a provider's key "
			+ "destruction would never be confirmed");
	}
}
