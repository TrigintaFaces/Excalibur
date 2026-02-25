// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// System.Text.Json implementation of <see cref="IJsonSerializer"/> for use with Dispatch Patterns.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via DI container")]
internal sealed class SystemTextJsonSerializer : IJsonSerializer
{
	private readonly JsonSerializerOptions _options;
	private readonly JsonSerializerContext? _context;

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemTextJsonSerializer"/> class.
	/// </summary>
	/// <param name="optionsAccessor">The options accessor for JSON serialization configuration.</param>
	public SystemTextJsonSerializer(IOptions<DispatchPatternsJsonOptions> optionsAccessor)
		: this(optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor)))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemTextJsonSerializer"/> class.
	/// </summary>
	/// <param name="options">The options for JSON serialization configuration.</param>
	internal SystemTextJsonSerializer(DispatchPatternsJsonOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.SerializerOptions ?? throw new ArgumentException("SerializerOptions must not be null.", nameof(options));
		_context = options.SerializerContext;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public string Serialize(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		if (_context is not null)
		{
			return JsonSerializer.Serialize(value, type, _context);
		}

		return JsonSerializer.Serialize(value, type, _options);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public object? Deserialize(string json, Type type)
	{
		ArgumentNullException.ThrowIfNull(json);
		ArgumentNullException.ThrowIfNull(type);

		if (_context is not null)
		{
			return JsonSerializer.Deserialize(json, type, _context);
		}

		return JsonSerializer.Deserialize(json, type, _options);
	}

}
