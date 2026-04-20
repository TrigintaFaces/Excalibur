// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Transport binding implementation that resolves its <see cref="ITransportAdapter"/>
/// lazily from the <see cref="ITransportRegistry"/> on first access.
/// </summary>
/// <remarks>
/// <para>
/// Used by <c>AddEventBindings(...)</c> when a binding references a transport that
/// was registered via <see cref="TransportRegistry.RegisterTransportFactory"/> but
/// whose adapter has not yet been materialized — typical for any registration order
/// where <c>AddEventBindings</c> is called before the host has started.
/// </para>
/// <para>
/// The registry materializes factories into adapters during
/// <c>TransportAdapterHostedService.StartAsync</c> (via <c>InitializeFactories</c>).
/// By the time a pipeline consumer touches <see cref="TransportAdapter"/>, the
/// adapter is available. The deferred lookup throws a clear error if the
/// transport was never registered — caught as a validation failure when
/// <c>ValidateOnStart()</c> resolves options.
/// </para>
/// </remarks>
internal sealed class LazyTransportBinding : ITransportBinding
{
	private readonly ITransportRegistry _registry;
	private readonly string _transportName;
	private readonly Regex? _endpointRegex;
	private ITransportAdapter? _cachedAdapter;

	internal LazyTransportBinding(
		string name,
		string transportName,
		ITransportRegistry registry,
		string endpointPattern,
		IPipelineProfile? pipelineProfile = null,
		MessageKinds acceptedMessageKinds = MessageKinds.All,
		int priority = 0)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentException.ThrowIfNullOrWhiteSpace(endpointPattern);

		Name = name;
		_transportName = transportName;
		_registry = registry;
		EndpointPattern = endpointPattern;
		PipelineProfile = pipelineProfile;
		AcceptedMessageKinds = acceptedMessageKinds;
		Priority = priority;

		if (endpointPattern.Contains('*', StringComparison.Ordinal) || endpointPattern.Contains('?', StringComparison.Ordinal))
		{
			var regexPattern = "^" + Regex.Escape(endpointPattern)
				.Replace(@"\*", ".*", StringComparison.Ordinal)
				.Replace(@"\?", ".", StringComparison.Ordinal) + "$";
			// Defense-in-depth ReDoS guard: consumer-configured endpoint patterns reach
			// this call site and could in theory construct a pathological backtracking
			// regex. Explicit MatchTimeout bounds the worst-case evaluation time to a
			// second rather than allowing unbounded catastrophic backtracking.
			// [S795 bd-ilwc63]
			_endpointRegex = new Regex(
				regexPattern,
				RegexOptions.Compiled | RegexOptions.IgnoreCase,
				matchTimeout: TimeSpan.FromSeconds(1));
		}
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public ITransportAdapter TransportAdapter => _cachedAdapter ??= ResolveAdapter();

	/// <inheritdoc />
	public string EndpointPattern { get; }

	/// <inheritdoc />
	public IPipelineProfile? PipelineProfile { get; }

	/// <inheritdoc />
	public MessageKinds AcceptedMessageKinds { get; }

	/// <inheritdoc />
	public int Priority { get; }

	/// <summary>Gets the transport name the binding was declared against.</summary>
	internal string TransportName => _transportName;

	/// <inheritdoc />
	public bool Matches(string endpoint)
	{
		if (_endpointRegex is not null)
		{
			return _endpointRegex.IsMatch(endpoint);
		}

		return string.Equals(EndpointPattern, endpoint, StringComparison.OrdinalIgnoreCase);
	}

	private ITransportAdapter ResolveAdapter()
	{
		return _registry.GetTransportAdapter(_transportName)
			?? throw new InvalidOperationException(
				$"Binding '{Name}' references transport '{_transportName}' which is not registered. " +
				"Register the transport via services.AddInMemoryTransport/AddKafkaTransport/etc. before the host starts.");
	}
}
