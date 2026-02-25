// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Routing.Policies;

/// <summary>
/// Loads routing rules from external JSON policy files.
/// </summary>
/// <remarks>
/// <para>
/// Supports JSON files with a <c>Rules</c> array containing <see cref="RoutingRule"/> objects.
/// Optionally watches the file for changes and reloads rules automatically.
/// </para>
/// <para>
/// Example policy file:
/// <code>
/// {
///   "Rules": [
///     { "MessageTypePattern": "Order*", "Transport": "rabbitmq", "Endpoint": "orders-queue", "Priority": 10 },
///     { "MessageTypePattern": "Notification*", "Transport": "kafka", "Endpoint": "notifications-topic" }
///   ]
/// }
/// </code>
/// </para>
/// </remarks>
/// <param name="options">The routing policy options.</param>
/// <param name="logger">The logger for diagnostic output.</param>
public sealed partial class RoutingPolicyFileLoader(
	IOptions<RoutingPolicyOptions> options,
	ILogger<RoutingPolicyFileLoader> logger) : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private readonly RoutingPolicyOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<RoutingPolicyFileLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private FileSystemWatcher? _watcher;
	private volatile IReadOnlyList<RoutingRule> _rules = [];
	private volatile bool _disposed;

	/// <summary>
	/// Gets the currently loaded routing rules.
	/// </summary>
	/// <value>The loaded rules, ordered by priority.</value>
	public IReadOnlyList<RoutingRule> Rules => _rules;

	/// <summary>
	/// Loads routing rules from the configured policy file.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The loaded routing rules.</returns>
	public async Task<IReadOnlyList<RoutingRule>> LoadAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_options.PolicyFilePath))
		{
			LogNoPolicyFileConfigured();
			return [];
		}

		var rules = await LoadRulesFromFileAsync(_options.PolicyFilePath, cancellationToken).ConfigureAwait(false);
		_rules = rules;

		if (_options.WatchForChanges && !_disposed)
		{
			SetupFileWatcher(_options.PolicyFilePath);
		}

		LogPolicyLoaded(rules.Count, _options.PolicyFilePath);
		return rules;
	}

	/// <summary>
	/// Finds the first matching routing rule for the given message type.
	/// </summary>
	/// <param name="messageTypeName">The message type name to match against rules.</param>
	/// <returns>The matching rule, or <see langword="null"/> if no rule matches.</returns>
	public RoutingRule? FindMatchingRule(string messageTypeName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

		foreach (var rule in _rules)
		{
			if (!rule.Enabled)
			{
				continue;
			}

			if (IsMatch(messageTypeName, rule.MessageTypePattern))
			{
				return rule;
			}
		}

		return null;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_watcher?.Dispose();
	}

	[LoggerMessage(31628, LogLevel.Information,
		"Loaded {RuleCount} routing rules from policy file {FilePath}")]
	private partial void LogPolicyLoaded(int ruleCount, string filePath);

	[LoggerMessage(31629, LogLevel.Debug,
		"No routing policy file configured")]
	private partial void LogNoPolicyFileConfigured();

	[LoggerMessage(31630, LogLevel.Warning,
		"Routing policy file not found: {FilePath}")]
	private partial void LogPolicyFileNotFound(string filePath);

	[LoggerMessage(31631, LogLevel.Information,
		"Routing policy file changed, reloading: {FilePath}")]
	private partial void LogPolicyFileChanged(string filePath);

	[LoggerMessage(31632, LogLevel.Error,
		"Failed to load routing policy file: {FilePath}")]
	private partial void LogPolicyLoadFailed(string filePath, Exception ex);

	/// <summary>
	/// Loads routing rules from a JSON file.
	/// </summary>
	private async Task<IReadOnlyList<RoutingRule>> LoadRulesFromFileAsync(
		string filePath,
		CancellationToken cancellationToken)
	{
		if (!File.Exists(filePath))
		{
			if (_options.ThrowOnMissingFile)
			{
				throw new FileNotFoundException(
					$"Routing policy file not found: {filePath}", filePath);
			}

			LogPolicyFileNotFound(filePath);
			return [];
		}

		try
		{
			await using var stream = File.OpenRead(filePath);
			var policyFile = await JsonSerializer.DeserializeAsync<RoutingPolicyFile>(
				stream, JsonOptions, cancellationToken).ConfigureAwait(false);

			if (policyFile?.Rules == null)
			{
				return [];
			}

			// Sort by priority (lower = higher priority) and return only enabled rules
			return policyFile.Rules
				.Where(static r => r.Enabled)
				.OrderBy(static r => r.Priority)
				.ToList()
				.AsReadOnly();
		}
		catch (JsonException ex)
		{
			LogPolicyLoadFailed(filePath, ex);
			throw new InvalidOperationException(
				$"Failed to parse routing policy file: {filePath}", ex);
		}
	}

	/// <summary>
	/// Sets up a file system watcher for hot-reload of the policy file.
	/// </summary>
	private void SetupFileWatcher(string filePath)
	{
		var directory = Path.GetDirectoryName(filePath);
		var fileName = Path.GetFileName(filePath);

		if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
		{
			return;
		}

		_watcher?.Dispose();
		_watcher = new FileSystemWatcher(directory, fileName)
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
			EnableRaisingEvents = true,
		};

		_watcher.Changed += OnPolicyFileChanged;
	}

	/// <summary>
	/// Handles the policy file changed event.
	/// </summary>
	private void OnPolicyFileChanged(object sender, FileSystemEventArgs e)
	{
		if (_disposed)
		{
			return;
		}

		LogPolicyFileChanged(e.FullPath);

		try
		{
			// Reload synchronously on file change (fire-and-forget pattern)
			var rules = LoadRulesFromFileAsync(e.FullPath, CancellationToken.None)
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult();
			_rules = rules;
		}
		catch (Exception ex)
		{
			LogPolicyLoadFailed(e.FullPath, ex);
			// Keep existing rules on reload failure
		}
	}

	/// <summary>
	/// Checks if a message type name matches a pattern (supports * wildcard).
	/// </summary>
	private static bool IsMatch(string messageTypeName, string pattern)
	{
		if (string.Equals(pattern, messageTypeName, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (pattern.Contains('*', StringComparison.Ordinal))
		{
			// Convert wildcard pattern to regex
			var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
			return Regex.IsMatch(messageTypeName, regexPattern, RegexOptions.IgnoreCase);
		}

		return false;
	}
}
