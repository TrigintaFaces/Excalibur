// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Default implementation of <see cref="IRoutingBuilder"/>.
/// </summary>
internal sealed class RoutingBuilder : IRoutingBuilder
{
	private readonly TransportRoutingBuilder _transportBuilder = new();
	private readonly EndpointRoutingBuilder _endpointBuilder = new();
	private readonly FallbackRoutingBuilder _fallbackBuilder = new();

	/// <inheritdoc/>
	public ITransportRoutingBuilder Transport => _transportBuilder;

	/// <inheritdoc/>
	public IEndpointRoutingBuilder Endpoints => _endpointBuilder;

	/// <inheritdoc/>
	public IFallbackRoutingBuilder Fallback => _fallbackBuilder;
}

/// <summary>
/// Default implementation of <see cref="ITransportRoutingBuilder"/>.
/// </summary>
internal sealed class TransportRoutingBuilder : ITransportRoutingBuilder
{
	private readonly List<TransportRoutingRule> _rules = [];
	private string? _defaultTransport;

	/// <inheritdoc/>
	public string? DefaultTransport => _defaultTransport;

	/// <inheritdoc/>
	public ITransportRuleBuilder<TEvent> Route<TEvent>() where TEvent : IIntegrationEvent
	{
		return new TransportRuleBuilder<TEvent>(this);
	}

	/// <inheritdoc/>
	public ITransportRoutingBuilder Default(string transport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transport);
		_defaultTransport = transport;
		return this;
	}

	/// <inheritdoc/>
	public IReadOnlyList<TransportRoutingRule> GetRules() => _rules.AsReadOnly();

	internal void AddRule(TransportRoutingRule rule)
	{
		_rules.Add(rule);
	}
}

/// <summary>
/// Builder for transport routing rules.
/// </summary>
internal sealed class TransportRuleBuilder<TEvent> : ITransportRuleBuilder<TEvent>
	where TEvent : IIntegrationEvent
{
	private readonly TransportRoutingBuilder _parent;

	public TransportRuleBuilder(TransportRoutingBuilder parent)
	{
		_parent = parent;
	}

	/// <inheritdoc/>
	public ITransportRoutingBuilder To(string transport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transport);
		_parent.AddRule(new TransportRoutingRule(typeof(TEvent), transport));
		return _parent;
	}

	/// <inheritdoc/>
	public IConditionalTransportRuleBuilder<TEvent> When(Func<TEvent, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		return new ConditionalTransportRuleBuilder<TEvent>(_parent, (msg, _) => predicate((TEvent)msg));
	}

	/// <inheritdoc/>
	public IConditionalTransportRuleBuilder<TEvent> When(Func<TEvent, IMessageContext, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		return new ConditionalTransportRuleBuilder<TEvent>(_parent, (msg, ctx) => predicate((TEvent)msg, ctx));
	}
}

/// <summary>
/// Builder for conditional transport routing rules.
/// </summary>
internal sealed class ConditionalTransportRuleBuilder<TEvent> : IConditionalTransportRuleBuilder<TEvent>
	where TEvent : IIntegrationEvent
{
	private readonly TransportRoutingBuilder _parent;
	private readonly Func<IDispatchMessage, IMessageContext, bool> _predicate;

	public ConditionalTransportRuleBuilder(
		TransportRoutingBuilder parent,
		Func<IDispatchMessage, IMessageContext, bool> predicate)
	{
		_parent = parent;
		_predicate = predicate;
	}

	/// <inheritdoc/>
	public ITransportRoutingBuilder To(string transport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transport);
		_parent.AddRule(new TransportRoutingRule(typeof(TEvent), transport, _predicate));
		return _parent;
	}
}

/// <summary>
/// Default implementation of <see cref="IEndpointRoutingBuilder"/>.
/// </summary>
internal sealed class EndpointRoutingBuilder : IEndpointRoutingBuilder
{
	private readonly List<EndpointRoutingRule> _rules = [];

	/// <inheritdoc/>
	public IEndpointRuleBuilder<TMessage> Route<TMessage>() where TMessage : IDispatchMessage
	{
		return new EndpointRuleBuilder<TMessage>(this);
	}

	/// <inheritdoc/>
	public IReadOnlyList<EndpointRoutingRule> GetRules() => _rules.AsReadOnly();

	internal void AddRule(EndpointRoutingRule rule)
	{
		_rules.Add(rule);
	}
}

/// <summary>
/// Builder for endpoint routing rules.
/// </summary>
internal sealed class EndpointRuleBuilder<TMessage> : IEndpointRuleBuilder<TMessage>
	where TMessage : IDispatchMessage
{
	private readonly EndpointRoutingBuilder _parent;

	public EndpointRuleBuilder(EndpointRoutingBuilder parent)
	{
		_parent = parent;
	}

	/// <inheritdoc/>
	public IEndpointRuleChainBuilder<TMessage> To(params string[] endpoints)
	{
		ValidateEndpoints(endpoints);
		_parent.AddRule(new EndpointRoutingRule(typeof(TMessage), endpoints.ToList().AsReadOnly()));
		return new EndpointRuleChainBuilder<TMessage>(_parent);
	}

	private static void ValidateEndpoints(string[] endpoints)
	{
		if (endpoints is null || endpoints.Length == 0)
		{
			throw new ArgumentException("At least one endpoint must be specified.", nameof(endpoints));
		}

		foreach (var endpoint in endpoints)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
		}
	}
}

/// <summary>
/// Builder for chaining endpoint routing rules.
/// </summary>
internal sealed class EndpointRuleChainBuilder<TMessage> : IEndpointRuleChainBuilder<TMessage>
	where TMessage : IDispatchMessage
{
	private readonly EndpointRoutingBuilder _parent;

	public EndpointRuleChainBuilder(EndpointRoutingBuilder parent)
	{
		_parent = parent;
	}

	/// <inheritdoc/>
	public IConditionalEndpointBuilder<TMessage> When(Func<TMessage, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		return new ConditionalEndpointBuilder<TMessage>(_parent, (msg, _) => predicate((TMessage)msg));
	}

	/// <inheritdoc/>
	public IConditionalEndpointBuilder<TMessage> When(Func<TMessage, IMessageContext, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		return new ConditionalEndpointBuilder<TMessage>(_parent, (msg, ctx) => predicate((TMessage)msg, ctx));
	}

	/// <inheritdoc/>
	public IEndpointRuleBuilder<TOther> Route<TOther>() where TOther : IDispatchMessage
	{
		return new EndpointRuleBuilder<TOther>(_parent);
	}

	/// <inheritdoc/>
	public IReadOnlyList<EndpointRoutingRule> GetRules() => _parent.GetRules();
}

/// <summary>
/// Builder for conditional endpoint routing rules.
/// </summary>
internal sealed class ConditionalEndpointBuilder<TMessage> : IConditionalEndpointBuilder<TMessage>
	where TMessage : IDispatchMessage
{
	private readonly EndpointRoutingBuilder _parent;
	private readonly Func<IDispatchMessage, IMessageContext, bool> _predicate;

	public ConditionalEndpointBuilder(
		EndpointRoutingBuilder parent,
		Func<IDispatchMessage, IMessageContext, bool> predicate)
	{
		_parent = parent;
		_predicate = predicate;
	}

	/// <inheritdoc/>
	public IEndpointRuleChainBuilder<TMessage> AlsoTo(params string[] endpoints)
	{
		ValidateEndpoints(endpoints);
		_parent.AddRule(new EndpointRoutingRule(
			typeof(TMessage),
			endpoints.ToList().AsReadOnly(),
			_predicate));
		return new EndpointRuleChainBuilder<TMessage>(_parent);
	}

	private static void ValidateEndpoints(string[] endpoints)
	{
		if (endpoints is null || endpoints.Length == 0)
		{
			throw new ArgumentException("At least one endpoint must be specified.", nameof(endpoints));
		}

		foreach (var endpoint in endpoints)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
		}
	}
}

/// <summary>
/// Default implementation of <see cref="IFallbackRoutingBuilder"/>.
/// </summary>
internal sealed class FallbackRoutingBuilder : IFallbackRoutingBuilder
{
	/// <inheritdoc/>
	public string? Endpoint { get; private set; }

	/// <inheritdoc/>
	public string? Reason { get; private set; }

	/// <inheritdoc/>
	public IFallbackRoutingBuilder To(string endpoint)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
		Endpoint = endpoint;
		return this;
	}

	/// <inheritdoc/>
	public IFallbackRoutingBuilder WithReason(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		Reason = reason;
		return this;
	}
}
