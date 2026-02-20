// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Binding configuration builder implementation.
/// </summary>
internal sealed class BindingConfigurationBuilder(
	Dictionary<string, ITransportAdapter> transportAdapters,
	IPipelineProfileRegistry profileRegistry)
	: IBindingConfigurationBuilder
{
	private string? _name;
	private string? _transportName;
	private string? _endpointPattern;
	private string? _pipelineName;
	private MessageKinds _messageKinds = MessageKinds.All;
	private int _priority;

	/// <inheritdoc />
	public IBindingConfigurationBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <inheritdoc />
	public IBindingConfigurationBuilder ForTransport(string transportName)
	{
		_transportName = transportName;
		return this;
	}

	/// <inheritdoc />
	public IBindingConfigurationBuilder ForEndpoint(string endpointPattern)
	{
		_endpointPattern = endpointPattern;
		return this;
	}

	/// <inheritdoc />
	public IBindingConfigurationBuilder UsePipeline(string pipelineName)
	{
		_pipelineName = pipelineName;
		return this;
	}

	/// <inheritdoc />
	public IBindingConfigurationBuilder AcceptMessageKinds(MessageKinds kinds)
	{
		_messageKinds = kinds;
		return this;
	}

	/// <inheritdoc />
	public IBindingConfigurationBuilder WithPriority(int priority)
	{
		_priority = priority;
		return this;
	}

	public ITransportBinding Build()
	{
		if (string.IsNullOrWhiteSpace(_name))
		{
			throw new InvalidOperationException(ErrorMessages.BindingNameIsRequired);
		}

		if (string.IsNullOrWhiteSpace(_transportName))
		{
			throw new InvalidOperationException(ErrorMessages.TransportNameIsRequired);
		}

		if (string.IsNullOrWhiteSpace(_endpointPattern))
		{
			throw new InvalidOperationException(ErrorMessages.EndpointPatternIsRequired);
		}

		if (!transportAdapters.TryGetValue(_transportName, out var adapter))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.BindingConfigurationBuilder_TransportNotFoundFormat,
					_transportName));
		}

		IPipelineProfile? profile = null;
		if (!string.IsNullOrWhiteSpace(_pipelineName))
		{
			profile = profileRegistry.GetProfile(_pipelineName);
			if (profile == null)
			{
				throw new InvalidOperationException(
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.BindingConfigurationBuilder_PipelineNotFoundFormat,
						_pipelineName));
			}
		}

		return new TransportBinding(
			_name,
			adapter,
			_endpointPattern,
			profile,
			_messageKinds,
			_priority);
	}
}
