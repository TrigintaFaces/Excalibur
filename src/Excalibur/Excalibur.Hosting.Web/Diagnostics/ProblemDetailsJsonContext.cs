// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Source-generated JSON serializer context for <see cref="ProblemDetails"/>.
/// Enables AOT-compatible JSON serialization in the <see cref="GlobalExceptionHandler"/>.
/// </summary>
[JsonSerializable(typeof(ProblemDetails))]
internal sealed partial class ProblemDetailsJsonContext : JsonSerializerContext;
