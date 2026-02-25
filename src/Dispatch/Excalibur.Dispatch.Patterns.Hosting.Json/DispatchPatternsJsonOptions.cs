// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Configuration options for the Excalibur.Dispatch.Patterns hosting JSON serializer.
/// </summary>
public sealed class DispatchPatternsJsonOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchPatternsJsonOptions" /> class.
	/// </summary>
	public DispatchPatternsJsonOptions() =>
		SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false, };

	/// <summary>
	/// Gets the configurable <see cref="JsonSerializerOptions" /> used when no source-generated context is supplied.
	/// </summary>
	/// <value> The configurable <see cref="JsonSerializerOptions" /> used when no source-generated context is supplied. </value>
	public JsonSerializerOptions SerializerOptions { get; }

	/// <summary>
	/// Gets or sets the optional <see cref="JsonSerializerContext" /> used for AOT-friendly serialization. When provided, the serializer
	/// prefers the context and falls back to <see cref="SerializerOptions" /> if type info is missing.
	/// </summary>
	/// <value>
	/// The optional <see cref="JsonSerializerContext" /> used for AOT-friendly serialization. When provided, the serializer prefers the
	/// context and falls back to <see cref="SerializerOptions" /> if type info is missing.
	/// </value>
	public JsonSerializerContext? SerializerContext { get; set; }
}
