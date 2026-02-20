// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Comprehensive source generation context for System.Text.Json serialization of all Dispatch message types. Optimized for AOT compilation
/// and trimmed deployments. Additional message types can be added at application compile time via partial classes. R7.6: High-performance
/// serialization with source generation for optimal throughput.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	GenerationMode = JsonSourceGenerationMode.Default,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]

// Core message abstractions (concrete types only) - MessageEnvelope excluded due to IValidationResult interface serialization issues
[JsonSerializable(typeof(object))]

// Message results (concrete types only) - MessageResult types excluded due to IValidationResult interface serialization issues
[JsonSerializable(typeof(Abstractions.MessageProblemDetails))]

// Validation types (concrete types only)
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(SerializableValidationResult))]

// Context and metadata - SerializableMessageContext excluded due to IValidationResult interface serialization issues
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]

// Common .NET types used in messaging
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(byte[]))]

// Nullable variants for robustness
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(DateTimeOffset?))]
[JsonSerializable(typeof(TimeSpan?))]
[JsonSerializable(typeof(Guid?))]
public partial class DispatchJsonContext : JsonSerializerContext;
