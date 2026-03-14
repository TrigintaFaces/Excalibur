// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;

/// <summary>
/// An ASP.NET Core <see cref="OutputFormatter"/> that uses the Dispatch <see cref="ISerializerRegistry"/>
/// to serialize response bodies based on content type negotiation.
/// </summary>
/// <remarks>
/// <para>
/// This formatter bridges the Dispatch serialization infrastructure with ASP.NET Core's content
/// negotiation pipeline. It discovers supported media types from registered <see cref="ISerializer"/>
/// instances and delegates serialization to the matching serializer.
/// </para>
/// <para>
/// MS Reference: <c>Microsoft.AspNetCore.Mvc.Formatters.OutputFormatter</c> (3 methods: CanWriteResult, WriteResponseBody, GetSupportedContentTypes).
/// </para>
/// </remarks>
public sealed class DispatchOutputFormatter : OutputFormatter
{
	private readonly ISerializerRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchOutputFormatter"/> class.
	/// </summary>
	/// <param name="registry">The serializer registry to discover supported media types.</param>
	public DispatchOutputFormatter(ISerializerRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;

		foreach (var (_, _, serializer) in _registry.GetAll())
		{
			if (!string.IsNullOrWhiteSpace(serializer.ContentType))
			{
				SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(serializer.ContentType));
			}
		}
	}

	/// <inheritdoc/>
	protected override bool CanWriteType(Type? type)
	{
		// Support serialization of any type through the registry
		return type != null;
	}

	/// <inheritdoc/>
	public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var response = context.HttpContext.Response;
		var contentType = context.ContentType.Value;

		if (context.Object is null)
		{
			return;
		}

		var serializer = FindSerializerByContentType(contentType);
		if (serializer is null)
		{
			// Fall back to current serializer
			var (_, current) = _registry.GetCurrent();
			serializer = current;
		}

		var data = serializer.SerializeObject(context.Object, context.ObjectType ?? context.Object.GetType());
		await response.Body.WriteAsync(data, context.HttpContext.RequestAborted).ConfigureAwait(false);
	}

	private ISerializer? FindSerializerByContentType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return null;
		}

		var parsedContentType = new MediaType(contentType);

		foreach (var (_, _, serializer) in _registry.GetAll())
		{
			if (string.IsNullOrWhiteSpace(serializer.ContentType))
			{
				continue;
			}

			var serializerMediaType = new MediaType(serializer.ContentType);
			if (serializerMediaType.IsSubsetOf(parsedContentType) || parsedContentType.IsSubsetOf(serializerMediaType))
			{
				return serializer;
			}
		}

		return null;
	}
}
