// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Tests.A3.Authorization.Stores.InMemory;

/// <summary>Holds the grant-query contract against the in-memory grant store.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryGrantQueryStoreConformanceShould : GrantQueryStoreConformanceTestBase
{
	/// <inheritdoc />
	protected override Task<IGrantStore> CreateStoreAsync() => Task.FromResult<IGrantStore>(new InMemoryGrantStore());
}
