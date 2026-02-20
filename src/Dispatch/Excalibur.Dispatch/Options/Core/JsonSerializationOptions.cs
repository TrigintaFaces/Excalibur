// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for JSON serialization using System.Text.Json.
/// </summary>
public sealed class JsonSerializationOptions
{
	private JsonSerializerOptions _jsonSerializerOptions = new();

	/// <summary>
	/// Gets or sets the JSON serializer options.
	/// </summary>
	/// <value> The underlying System.Text.Json serializer configuration. </value>
	public JsonSerializerOptions JsonSerializerOptions
	{
		get => _jsonSerializerOptions;
		set => _jsonSerializerOptions = value ?? new JsonSerializerOptions();
	}

	/// <summary>
	/// Gets or sets a value indicating whether to preserve object references during serialization.
	/// </summary>
	/// <value> <see langword="true" /> to include reference metadata; otherwise, <see langword="false" />. </value>
	public bool PreserveReferences { get; set; }

	/// <summary>
	/// Gets or sets the maximum depth when serializing nested objects.
	/// </summary>
	/// <value> The maximum allowed depth for JSON serialization. </value>
	public int MaxDepth { get; set; } = 64;

	/// <summary>
	/// Builds a <see cref="System.Text.Json.JsonSerializerOptions" /> instance with all configured settings applied.
	/// </summary>
	/// <returns> A fully configured <see cref="System.Text.Json.JsonSerializerOptions" /> instance. </returns>
	public JsonSerializerOptions BuildJsonSerializerOptions()
	{
		var options = _jsonSerializerOptions;
		options.MaxDepth = MaxDepth;

		if (PreserveReferences)
		{
			options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
		}

		return options;
	}
}
