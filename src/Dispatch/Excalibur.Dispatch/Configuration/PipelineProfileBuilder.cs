// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Default implementation of the pipeline profile builder.
/// </summary>
internal sealed class PipelineProfileBuilder : IPipelineProfileBuilder
{
	private readonly string _name;
	private readonly string _description;
	private readonly List<Type> _middlewareTypes = [];
	private readonly bool _isStrict;
	private MessageKinds _messageKinds = MessageKinds.All;

	public PipelineProfileBuilder(string name, string description, bool isStrict = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);

		_name = name;
		_description = description;
		_isStrict = isStrict;
	}

	/// <inheritdoc/>
	public IPipelineProfileBuilder ForMessageKinds(MessageKinds messageKinds)
	{
		_messageKinds = messageKinds;
		return this;
	}

	/// <inheritdoc/>
	public IPipelineProfileBuilder UseMiddleware<TMiddleware>()
		where TMiddleware : IDispatchMiddleware
	{
		_middlewareTypes.Add(typeof(TMiddleware));
		return this;
	}

	/// <inheritdoc/>
	public IPipelineProfile Build() =>
		new PipelineProfile(
			_name,
			_description,
			_middlewareTypes,
			_isStrict,
			_messageKinds);
}
