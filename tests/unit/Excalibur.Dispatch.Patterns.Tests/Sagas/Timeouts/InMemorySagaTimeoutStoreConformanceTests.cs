// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Timeouts;

/// <summary>
/// Binds <see cref="InMemorySagaTimeoutStore"/> to the shared <see cref="ISagaTimeoutStore"/> conformance kit.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Saga)]
[Trait("Pattern", "STORE")]
public sealed class InMemorySagaTimeoutStoreConformanceTests : SagaTimeoutStoreConformanceTestBase
{
	/// <inheritdoc/>
	protected override Task<ISagaTimeoutStore> CreateStoreAsync() =>
		Task.FromResult<ISagaTimeoutStore>(new InMemorySagaTimeoutStore(new TestTenantContext()));

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask;
}
