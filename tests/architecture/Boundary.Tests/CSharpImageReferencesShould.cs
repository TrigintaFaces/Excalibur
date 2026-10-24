// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CSharpImageReferencesShould
{
    [Fact]
    public void DecodeEscapedImageName() => CSharpImageReferences.Read("var image = \"vendor:lat\\u0065st\";")
        .ShouldContain("vendor:" + "latest");

    [Fact]
    public void ResolveConstantSuffix() => CSharpImageReferences.Read("const string tag = \"latest\"; var image = \"vendor:\" + tag;")
        .ShouldContain("vendor:" + "latest");

    [Fact]
    public void IgnoreComments() => CSharpImageReferences.Read("// vendor:" + "latest").ShouldBeEmpty();

    [Fact]
    public void RefuseBrokenSource() => Should.Throw<InvalidOperationException>(() => CSharpImageReferences.Read("var s = \"broken;").ToArray());
}
