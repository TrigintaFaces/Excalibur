// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Boundary.Tests;

internal sealed class RepositoryPolicyOptions(string property) : AnalyzerConfigOptionsProvider
{
    private sealed class Options(string property) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = "true";
            return key == "build_property." + property;
        }
    }

    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(property);
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
}
