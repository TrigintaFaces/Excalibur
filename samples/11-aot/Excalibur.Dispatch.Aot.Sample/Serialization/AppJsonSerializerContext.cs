// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Aot.Sample.Messages;

namespace Excalibur.Dispatch.Aot.Sample.Serialization;

/// <summary>
/// Source-generated JSON serialization context for AOT compatibility.
/// </summary>
/// <remarks>
/// <para>
/// AOT/Trimming Requirements:
/// This class enables source-generated JSON serialization, which is required for:
/// - Native AOT compilation (PublishAot=true)
/// - Full trimming (TrimMode=full)
/// - Reflection-free JSON handling (JsonSerializerIsReflectionEnabledByDefault=false)
/// </para>
///
/// <para>
/// How it works:
/// - At compile time, System.Text.Json generates optimized serialization code
/// - No runtime reflection or IL emit is needed
/// - The generated code is trimmer-safe and AOT-compatible
/// </para>
///
/// <para>
/// Adding new types:
/// When adding new message types, add a corresponding [JsonSerializable] attribute.
/// </para>
/// </remarks>
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderCreatedEvent))]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(OrderItem))]
[JsonSerializable(typeof(IReadOnlyList<OrderItem>))]
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
