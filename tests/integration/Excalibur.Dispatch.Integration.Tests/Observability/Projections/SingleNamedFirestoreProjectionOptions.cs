// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Serves one <see cref="FirestoreProjectionStoreOptions"/> instance under every options name.
/// </summary>
/// <remarks>
/// The store resolves its settings by a name derived from the projection type. A directly-constructed
/// store has no container to register that name against, so this returns the same instance whatever is
/// asked for — correct here because each suite below drives exactly one projection type.
/// </remarks>
internal sealed class SingleNamedFirestoreProjectionOptions(FirestoreProjectionStoreOptions options)
	: IOptionsMonitor<FirestoreProjectionStoreOptions>
{
	public FirestoreProjectionStoreOptions CurrentValue => options;

	public FirestoreProjectionStoreOptions Get(string? name) => options;

	public IDisposable? OnChange(Action<FirestoreProjectionStoreOptions, string?> listener) => null;
}
