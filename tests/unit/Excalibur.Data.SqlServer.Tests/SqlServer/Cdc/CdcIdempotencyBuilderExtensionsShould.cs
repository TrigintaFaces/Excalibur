// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcIdempotencyBuilderExtensions"/>.
/// Verifies DI registration behavior for the in-memory CDC idempotency filter.
/// </summary>
/// <remarks>
/// Sprint 825 — bd-jmx38a: CDC idempotency filtering DI extensions.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterInMemoryIdempotencyFilter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseInMemoryIdempotencyFilter();

		// Assert — returns the builder for fluent chaining
		result.ShouldBe(builder);

		// Assert — ICdcIdempotencyFilter is registered
		var sp = services.BuildServiceProvider();
		var filter = sp.GetService<ICdcIdempotencyFilter>();
		filter.ShouldNotBeNull();
		filter.ShouldBeOfType<InMemoryCdcIdempotencyFilter>();
	}

	[Fact]
	public void NotOverrideExistingRegistration_WithTryAddSemantics()
	{
		// Arrange — pre-register a concrete implementation
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ICdcIdempotencyFilter, InMemoryCdcIdempotencyFilter>();

		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act — UseInMemoryIdempotencyFilter uses TryAdd, so should be a no-op
		_ = builder.UseInMemoryIdempotencyFilter();

		// Assert — only one registration exists (TryAdd didn't add a duplicate)
		var registrations = services.Where(d => d.ServiceType == typeof(ICdcIdempotencyFilter)).ToList();
		registrations.Count.ShouldBe(1);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => CdcIdempotencyBuilderExtensions.UseInMemoryIdempotencyFilter(null!));
	}
}
