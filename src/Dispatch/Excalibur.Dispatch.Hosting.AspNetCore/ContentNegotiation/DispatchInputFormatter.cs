// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;

/// <summary>
/// An ASP.NET Core <see cref="InputFormatter"/> that uses the Dispatch <see cref="ISerializerRegistry"/>
/// to deserialize request bodies based on content type negotiation.
/// </summary>
/// <remarks>
/// <para>
/// This formatter bridges the Dispatch serialization infrastructure with ASP.NET Core's content
/// negotiation pipeline. It discovers supported media types from registered <see cref="ISerializer"/>
/// instances and delegates deserialization to the matching serializer.
/// </para>
/// <para>
/// MS Reference: <c>Microsoft.AspNetCore.Mvc.Formatters.InputFormatter</c> (3 methods: CanRead, ReadRequestBody, GetSupportedContentTypes).
/// </para>
/// </remarks>
public sealed class DispatchInputFormatter : InputFormatter
{
	private readonly ISerializerRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchInputFormatter"/> class.
	/// </summary>
	/// <param name="registry">The serializer registry to discover supported media types.</param>
	public DispatchInputFormatter(ISerializerRegistry registry)
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
	protected override bool CanReadType(Type type)
	{
		// Support deserialization of any type through the registry
		return type != null;
	}

	/// <inheritdoc/>
	public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var request = context.HttpContext.Request;
		var contentType = request.ContentType;

		if (string.IsNullOrWhiteSpace(contentType))
		{
			return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
		}

		var serializer = FindSerializerByContentType(contentType);
		if (serializer is null)
		{
			return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
		}

		using var memoryStream = new MemoryStream();
		await request.Body.CopyToAsync(memoryStream, context.HttpContext.RequestAborted).ConfigureAwait(false);
		var data = memoryStream.ToArray();

		if (data.Length == 0)
		{
			return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
		}

		var result = serializer.DeserializeObject(data, context.ModelType);
		return await InputFormatterResult.SuccessAsync(result).ConfigureAwait(false);
	}

	private ISerializer? FindSerializerByContentType(string contentType)
	{
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
