// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// RuntimeFeature.IsDynamicCodeSupported is a feature SWITCH, not a probe of how this process was
// compiled. Setting PublishAot writes it false into runtimeconfig.json, so the check below also
// passes under `dotnet run` on the managed runtime. It establishes that dynamic code is disabled.
// It does NOT establish that this process is a native binary, and nothing in-process can.
if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
{
    throw new InvalidOperationException(
        "Dynamic code must be disabled for these checks. Passing this check does not mean the process is NativeAOT; publish and run the native executable for that.");
}

Console.WriteLine(
    $"Scope: checks run with dynamicCode={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}, " +
    $"jsonReflection={System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault}. " +
    "Native execution is established only by running the published native executable, never by this process.");

await Excalibur.Dispatch.Aot.Sample.DataProcessingSmoke.RunAsync();
