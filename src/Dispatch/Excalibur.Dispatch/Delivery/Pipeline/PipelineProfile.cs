// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Middleware.Auth;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Default implementation of a pipeline profile that defines middleware composition for specific processing scenarios.
/// </summary>
internal sealed class PipelineProfile : IPipelineProfile, IPipelineProfileMatcher
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineProfile"/> class.
	/// Creates a new pipeline profile.
	/// </summary>
	public PipelineProfile(
		string name,
		string description,
		IEnumerable<Type> middlewareTypes,
		bool isStrict = false,
		MessageKinds supportedMessageKinds = MessageKinds.All)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);
		ArgumentNullException.ThrowIfNull(middlewareTypes);

		Name = name;
		Description = description;
		IsStrict = isStrict;
		SupportedMessageKinds = supportedMessageKinds;

		var middlewareTypeList = new List<Type>();
		foreach (var type in middlewareTypes)
		{
			if (!typeof(IDispatchMiddleware).IsAssignableFrom(type))
			{
				throw new ArgumentException(
								ErrorMessages.TypeDoesNotImplementInterface,
								nameof(middlewareTypes));
			}

			middlewareTypeList.Add(type);
		}

		var entries = new MiddlewareEntry[middlewareTypeList.Count];
		for (var i = 0; i < middlewareTypeList.Count; i++)
		{
			entries[i] = new MiddlewareEntry(middlewareTypeList[i], MiddlewareCriticality.Required);
		}

		MiddlewareEntries = Array.AsReadOnly(entries);
		for (var i = 0; i < middlewareTypeList.Count; i++)
		{
		}
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string Description { get; }

	/// <inheritdoc />
	/// <remarks>
	/// Every declared entry is <see cref="MiddlewareCriticality.Required" />: this profile validates at construction that each type
	/// implements the middleware contract, so a type reaching this list was deliberately named and must not be silently dropped when it
	/// cannot be resolved.
	/// </remarks>
	public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

	/// <inheritdoc />
	public bool IsStrict { get; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds { get; }

	/// <inheritdoc />
	public bool IsCompatible(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Quick check if we support all message kinds
		if (SupportedMessageKinds == MessageKinds.All)
		{
			return true;
		}

		// Determine the message's kinds
		var messageKinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(message.GetType());

		// Check if any of the message's kinds are supported
		return (SupportedMessageKinds & messageKinds) != MessageKinds.None;
	}
}
