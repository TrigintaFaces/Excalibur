// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Concrete DTO for serializing exception details in poison message handling.
/// Replaces anonymous type for AOT compatibility.
/// </summary>
internal sealed record PoisonExceptionInfo
{
	[JsonPropertyName("Type")]
	public string? Type { get; init; }

	[JsonPropertyName("Message")]
	public string? Message { get; init; }

	[JsonPropertyName("StackTrace")]
	public string? StackTrace { get; init; }

	[JsonPropertyName("InnerException")]
	public PoisonExceptionInfo? InnerException { get; init; }

	[JsonPropertyName("Data")]
	public Dictionary<string, string?>? Data { get; init; }
}
