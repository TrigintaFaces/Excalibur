// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>Preserves the legacy options type and declared accessors for existing consumers.</summary>
public sealed class ProblemDetailsOptions : Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.ProblemDetailsOptions
{
    /// <summary>Gets or sets the base URL for status type references.</summary>
    public new string StatusTypeBaseUrl
    {
        get => base.StatusTypeBaseUrl;
        set => base.StatusTypeBaseUrl = value;
    }

    /// <summary>Gets the shared collection of supported locale identifiers.</summary>
    public new HashSet<string> SupportedLocales => base.SupportedLocales;
}