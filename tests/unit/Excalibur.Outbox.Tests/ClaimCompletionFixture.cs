// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests;

/// <summary>Adapts legacy test input descriptions to the actual private immutable work buckets.</summary>
internal static class ClaimCompletionFixture
{
    internal static void AdaptEvidenceBuckets(MethodInfo method, object?[] arguments)
    {
        var parameters = method.GetParameters();
        var successType = parameters[1].ParameterType;
        var claimType = successType.GetGenericArguments()[0];
        object Claim(string id) => Activator.CreateInstance(claimType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, binder: null,
            args: [id, "claim-" + id], culture: null)!;
        var successes = (IList)Activator.CreateInstance(successType)!;
        foreach (var id in (IEnumerable<string>)arguments[1]!) { successes.Add(Claim(id)); }
        arguments[1] = successes;
        var terminalType = parameters[3].ParameterType;
        var terminalElement = terminalType.GetGenericArguments()[0];
        var terminals = (IList)Activator.CreateInstance(terminalType)!;
        foreach (var (id, attempts) in (IEnumerable<(string, int)>)arguments[3]!)
        {
            terminals.Add(Activator.CreateInstance(terminalElement, [Claim(id), attempts]));
        }
        arguments[3] = terminals;
    }

    internal static void AssertNoLegacyWrites(IOutboxStore store)
    {
        A.CallTo(() => store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
        if (store.GetService(typeof(IDeadLetterableOutboxStore)) is IDeadLetterableOutboxStore terminal)
        {
            A.CallTo(() => terminal.MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
    }
}
