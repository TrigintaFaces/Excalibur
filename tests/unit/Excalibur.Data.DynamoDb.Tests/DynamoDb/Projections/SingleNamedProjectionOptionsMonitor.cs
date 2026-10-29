// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.DynamoDb.Projections;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

/// <summary>
/// Serves one options instance under every name.
/// </summary>
/// <remarks>
/// The store resolves its options by the projection type's options name. A directly-constructed store
/// has no container to register that name against, so this returns the same instance whatever is asked
/// for — correct for a suite exercising exactly one projection type. A suite that needs two DISTINCT
/// configurations must go through a real container, as the destination-identity lock does.
/// </remarks>
internal sealed class SingleNamedProjectionOptionsMonitor(DynamoDbProjectionStoreOptions options)
	: IOptionsMonitor<DynamoDbProjectionStoreOptions>
{
	public DynamoDbProjectionStoreOptions CurrentValue => options;

	public DynamoDbProjectionStoreOptions Get(string? name) => options;

	public IDisposable? OnChange(Action<DynamoDbProjectionStoreOptions, string?> listener) => null;
}
