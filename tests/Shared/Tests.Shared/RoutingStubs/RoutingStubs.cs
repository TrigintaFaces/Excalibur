// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.RoutingStubs;

/// <summary>Routing rule type enumeration for tests.</summary>
public enum RoutingRuleType
{
	/// <summary>Message type-based routing.</summary>
	MessageType,

	/// <summary>Attribute-based routing.</summary>
	Attribute,

	/// <summary>Predicate-based routing.</summary>
	Predicate,

	/// <summary>Content-based routing.</summary>
	ContentBased,

	/// <summary>Header-based routing.</summary>
	HeaderBased
}

/// <summary>Routing context interface for tests.</summary>
public interface IRoutingContext
{
	/// <summary>Gets the context properties.</summary>
	IReadOnlyDictionary<string, object> Properties { get; }

	/// <summary>Gets the cancellation token.</summary>
	CancellationToken CancellationToken { get; }
}

/// <summary>Routing rule interface stub matching production signature.</summary>
public interface IRoutingRule
{
	/// <summary>Gets the rule name.</summary>
	string Name { get; }

	/// <summary>Gets the rule type.</summary>
	RoutingRuleType RuleType { get; }

	/// <summary>Gets the rule priority (lower = higher priority).</summary>
	int Priority { get; }

	/// <summary>Gets the destination endpoints.</summary>
	IReadOnlyList<string> Destinations { get; }

	/// <summary>Gets whether to stop evaluating after match.</summary>
	bool StopOnMatch { get; }

	/// <summary>Gets the rule metadata.</summary>
	IReadOnlyDictionary<string, string> Metadata { get; }

	/// <summary>Evaluates whether the message matches this rule.</summary>
	ValueTask<bool> EvaluateAsync(IDispatchMessage message, IRoutingContext context, CancellationToken cancellationToken);
}

/// <summary>In-memory routing context for testing.</summary>
public class InMemoryRoutingContext : IRoutingContext
{
	private readonly Dictionary<string, object> _properties = new();

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, object> Properties => _properties;

	/// <inheritdoc/>
	public CancellationToken CancellationToken { get; set; }

	/// <summary>Sets a property value.</summary>
	public void SetProperty(string key, object value) => _properties[key] = value;
}

/// <summary>Simple routing rule implementation for testing.</summary>
public class SimpleRoutingRule : IRoutingRule
{
	/// <inheritdoc/>
	public string Name { get; set; } = string.Empty;

	/// <inheritdoc/>
	public RoutingRuleType RuleType { get; set; } = RoutingRuleType.MessageType;

	/// <inheritdoc/>
	public int Priority { get; set; }

	/// <inheritdoc/>
	public IReadOnlyList<string> Destinations { get; set; } = [];

	/// <inheritdoc/>
	public bool StopOnMatch { get; set; } = true;

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

	/// <summary>Gets or sets the match predicate.</summary>
	public Func<IDispatchMessage, bool>? MatchPredicate { get; set; }

	/// <inheritdoc/>
	public ValueTask<bool> EvaluateAsync(IDispatchMessage message, IRoutingContext context, CancellationToken cancellationToken)
	{
		var result = MatchPredicate?.Invoke(message) ?? true;
		return ValueTask.FromResult(result);
	}
}
