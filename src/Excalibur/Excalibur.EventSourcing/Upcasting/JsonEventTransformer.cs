// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Declarative event upgrader that applies JSON path-based transformations
/// without requiring compile-time event types.
/// </summary>
/// <remarks>
/// <para>
/// This transformer parses the serialized event data as a <see cref="JsonNode"/>,
/// applies the configured transformation rules (rename, remove, add default, move),
/// and returns the modified JSON as the upgraded event.
/// </para>
/// <para>
/// Use <see cref="JsonTransformRuleBuilder"/> to construct the transformation rules fluently.
/// </para>
/// </remarks>
public sealed class JsonEventTransformer : IEventUpgrader
{
	private readonly IReadOnlyList<JsonTransformRule> _rules;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonEventTransformer"/> class.
	/// </summary>
	/// <param name="eventType">The event type this transformer handles.</param>
	/// <param name="fromVersion">The source version this transformer upgrades from.</param>
	/// <param name="toVersion">The target version this transformer upgrades to.</param>
	/// <param name="rules">The transformation rules to apply.</param>
	public JsonEventTransformer(
		string eventType,
		int fromVersion,
		int toVersion,
		IReadOnlyList<JsonTransformRule> rules)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventType);
		ArgumentNullException.ThrowIfNull(rules);

		EventType = eventType;
		FromVersion = fromVersion;
		ToVersion = toVersion;
		_rules = rules;
	}

	/// <inheritdoc />
	public string EventType { get; }

	/// <inheritdoc />
	public int FromVersion { get; }

	/// <inheritdoc />
	public int ToVersion { get; }

	/// <inheritdoc />
	public bool CanUpgrade(string eventType, int fromVersion) =>
		string.Equals(eventType, EventType, StringComparison.Ordinal) && fromVersion == FromVersion;

	/// <inheritdoc />
	public object Upgrade(object oldEvent)
	{
		ArgumentNullException.ThrowIfNull(oldEvent);

		var json = oldEvent switch
		{
			string s => s,
			byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
			JsonElement element => element.GetRawText(),
			_ => throw new ArgumentException(
				$"Expected event data as string, byte[], or JsonElement, but got {oldEvent.GetType().Name}.",
				nameof(oldEvent))
		};

		var node = JsonNode.Parse(json);
		if (node is not JsonObject obj)
		{
			throw new InvalidOperationException(
				$"Event data for '{EventType}' is not a JSON object.");
		}

		ApplyRules(obj);

		return obj.ToJsonString();
	}

	private void ApplyRules(JsonObject obj)
	{
		foreach (var rule in _rules)
		{
			switch (rule.Operation)
			{
				case JsonTransformOperation.Rename:
					ApplyRename(obj, rule);
					break;
				case JsonTransformOperation.Remove:
					obj.Remove(rule.Path);
					break;
				case JsonTransformOperation.AddDefault:
					ApplyAddDefault(obj, rule);
					break;
				case JsonTransformOperation.Move:
					ApplyMove(obj, rule);
					break;
				default:
					throw new InvalidOperationException(
						$"Unknown transform operation: {rule.Operation}");
			}
		}
	}

	private static void ApplyRename(JsonObject obj, JsonTransformRule rule)
	{
		if (obj.TryGetPropertyValue(rule.Path, out var value))
		{
			obj.Remove(rule.Path);
			obj[rule.TargetPath!] = value is not null ? JsonNode.Parse(value.ToJsonString()) : null;
		}
	}

	private static void ApplyAddDefault(JsonObject obj, JsonTransformRule rule)
	{
		if (!obj.ContainsKey(rule.Path))
		{
			obj[rule.Path] = rule.DefaultValue is not null
				? JsonSerializer.SerializeToNode(rule.DefaultValue)
				: null;
		}
	}

	private static void ApplyMove(JsonObject obj, JsonTransformRule rule)
	{
		if (obj.TryGetPropertyValue(rule.Path, out var value))
		{
			obj.Remove(rule.Path);
			obj[rule.TargetPath!] = value is not null ? JsonNode.Parse(value.ToJsonString()) : null;
		}
	}
}
