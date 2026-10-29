// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Runtime.CompilerServices;
using Excalibur.EventSourcing;
using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore;

/// <summary>Records transport identity only for the exact clients constructed by this provider.</summary>
internal static class FirestoreClientScope
{
    private static readonly ConditionalWeakTable<FirestoreDb, BoundScope> Scopes = new();

    internal static FirestoreDb Bind(FirestoreDb database, string endpoint, bool insecure)
    {
        var address = new Uri((insecure ? "http://" : "https://") + endpoint);
        var scope = "firestore/" + Uri.EscapeDataString(address.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped));
        Scopes.Add(database, new BoundScope(scope));
        return database;
    }

    internal static IProjectionDestinationScope? Get(FirestoreDb database) =>
        Scopes.TryGetValue(database, out var scope) ? scope : null;

    private sealed record BoundScope(string Scope) : IProjectionDestinationScope;
}
