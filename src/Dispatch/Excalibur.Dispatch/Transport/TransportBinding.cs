// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of a transport binding.
/// </summary>
public sealed class TransportBinding : ITransportBinding
{
	private readonly Regex? _endpointRegex;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportBinding"/> class.
	/// Creates a new transport binding.
	/// </summary>
	public TransportBinding(
		string name,
		ITransportAdapter transportAdapter,
		string endpointPattern,
		IPipelineProfile? pipelineProfile = null,
		MessageKinds acceptedMessageKinds = MessageKinds.All,
		int priority = 0)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(transportAdapter);
		ArgumentException.ThrowIfNullOrWhiteSpace(endpointPattern);

		Name = name;
		TransportAdapter = transportAdapter;
		EndpointPattern = endpointPattern;
		PipelineProfile = pipelineProfile;
		AcceptedMessageKinds = acceptedMessageKinds;
		Priority = priority;

		// If pattern contains wildcards, convert to regex
		if (endpointPattern.Contains('*', StringComparison.Ordinal) || endpointPattern.Contains('?', StringComparison.Ordinal))
		{
			var regexPattern = "^" + Regex.Escape(endpointPattern)
				.Replace(@"\*", ".*", StringComparison.Ordinal)
				.Replace(@"\?", ".", StringComparison.Ordinal) + "$";
			_endpointRegex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
		}
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public ITransportAdapter TransportAdapter { get; }

	/// <inheritdoc />
	public string EndpointPattern { get; }

	/// <inheritdoc />
	public IPipelineProfile? PipelineProfile { get; }

	/// <inheritdoc />
	public MessageKinds AcceptedMessageKinds { get; }

	/// <inheritdoc />
	public int Priority { get; }

	/// <inheritdoc />
	public bool Matches(string endpoint)
	{
		if (_endpointRegex != null)
		{
			return _endpointRegex.IsMatch(endpoint);
		}

		// Exact match
		return string.Equals(EndpointPattern, endpoint, StringComparison.OrdinalIgnoreCase);
	}
}
