// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class SagaCorrelationSmoke
{
    internal static void Run()
    {
        Check(!RuntimeFeature.IsDynamicCodeSupported && !RuntimeFeature.IsDynamicCodeCompiled, "Actual native executable required.");
        Check(!JsonSerializer.IsReflectionEnabledByDefault, "Reflection JSON must remain disabled.");
        Check(SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001") == Guid.Parse("d78942e7-1813-8939-b17c-230d208af7b3"), "Framed derivation/network byte order drifted.");
        Check(SagaCorrelation.ToSagaId("a\u001fb", "c") != SagaCorrelation.ToSagaId("a", "b\u001fc"), "Literal delimiter aliases remain.");
        Check(SagaCorrelation.ToSagaId("\u03C0\U0001F69A", "\u00E9\0x") == Guid.Parse("ee33ec56-70f5-8556-9664-98e693ff5339"), "Literal Unicode/NUL UTF8 frame drifted.");
        var typed = SagaCorrelation.ToSagaId<GenericSaga<Dictionary<string, int[]>>>("ORDER-1001");
        Check(typed == Guid.Parse("8c84d839-469d-829d-b89a-ce8260ebe3e3"), "Version-free generic type scope drifted under NativeAOT.");
        Check(typed == SagaCorrelation.ToSagaId<GenericSaga<Dictionary<string, int[]>>>("ORDER-1001"), "Typed repeat drifted.");
        Check(SagaCorrelation.ToSagaId<GenericSaga<int[]>>("key") != SagaCorrelation.ToSagaId<GenericSaga<int[,]>>("key"), "Array shape lost from typed identity.");
        Console.WriteLine("PASS actual NativeAOT saga correlation: framed literal Unicode keys, typed generic/array scope, UUIDv8 golden values; reflection JSON disabled.");
    }
    private static void Check(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private sealed class GenericSaga<T> { }
}
