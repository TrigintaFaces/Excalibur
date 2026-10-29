// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators;

internal static class GeneratorDisplayFormats
{
    // For declarations and generic arguments, including nested interface arguments in typeof.
    // Only top-level nullable reference operands of typeof require annotation-free rendering.
    internal static readonly SymbolDisplayFormat TypeReference = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
