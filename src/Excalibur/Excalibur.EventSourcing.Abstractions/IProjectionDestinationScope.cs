// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Optional stable configuration identity of the actual projection cluster for automatic inline setup.</summary>
/// <remarks>
/// Resolve through the store's IServiceProvider capability chain. Scope excludes credentials and is
/// immutable for the store lifetime. Equivalent configured endpoints must agree across cooperating
/// hosts. This is not a physical resource identity: alias changes and recreated indices require an
/// explicit rebuild. Stores or decorators unable to establish the scope must not expose this capability.
/// </remarks>
public interface IProjectionDestinationScope
{
	/// <summary>Gets the nonblank, credential-free cluster configuration identity.</summary>
	string Scope { get; }
}

/// <summary>Resolves a storage scope when the actual service must supply account or resource metadata.</summary>
/// <remarks>Resolve through the outer store capability chain. Successful resolution must pin subsequent operations
/// to that scope. Failed or cancelled resolution must remain retryable; never return an unverified label.</remarks>
public interface IAsyncProjectionDestinationScope
{
    /// <summary>Resolves the credential-free scope of the actual destination.</summary>
    /// <param name="cancellationToken">Cancels metadata resolution.</param>
    /// <returns>The verified storage scope.</returns>
    ValueTask<string> GetScopeAsync(CancellationToken cancellationToken);
}
