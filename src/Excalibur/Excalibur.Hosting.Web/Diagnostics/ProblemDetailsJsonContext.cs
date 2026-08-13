// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Source-generated JSON serializer context for <see cref="ProblemDetails"/>.
/// Enables AOT-compatible JSON serialization in the <see cref="GlobalExceptionHandler"/>.
/// </summary>
/// <remarks>
/// A source-generated context is closed: every concrete runtime type that can flow through
/// <see cref="ProblemDetails.Extensions"/> (an <c>IDictionary&lt;string, object?&gt;</c>, so its values
/// are resolved polymorphically) must be declared here explicitly, or serialization throws
/// <see cref="NotSupportedException"/> instead of writing a response.
/// <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, string[]&gt;</see> is the concrete type
/// <see cref="Excalibur.Dispatch.Exceptions.ValidationException.ValidationErrors"/> carries when
/// <see cref="GlobalExceptionHandler"/> assigns it to the <c>"errors"</c> extension member.
/// </remarks>
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal sealed partial class ProblemDetailsJsonContext : JsonSerializerContext;
