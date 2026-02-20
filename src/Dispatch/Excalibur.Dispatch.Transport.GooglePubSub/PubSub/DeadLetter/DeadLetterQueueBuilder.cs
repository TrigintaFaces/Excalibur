// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Builder for configuring dead letter queue services.
/// </summary>
public sealed class DeadLetterQueueBuilder
{
	private readonly IServiceCollection _services;
	private bool _analyticsEnabled;
	private bool _poisonDetectionEnabled = true;
	private bool _retryPoliciesEnabled = true;

	internal DeadLetterQueueBuilder(IServiceCollection services) => _services = services;

	/// <summary>
	/// Configures the dead letter topic.
	/// </summary>
	public DeadLetterQueueBuilder WithDeadLetterTopic(string projectId, string topicName)
	{
		_ = _services.Configure<DeadLetterOptions>(options =>
			options.DeadLetterTopicName = new TopicName(projectId, topicName));

		return this;
	}

	/// <summary>
	/// Configures basic options.
	/// </summary>
	public DeadLetterQueueBuilder WithOptions(Action<DeadLetterOptions> configure)
	{
		_ = _services.Configure(configure);
		return this;
	}

	/// <summary>
	/// Enables analytics with configuration.
	/// </summary>
	public DeadLetterQueueBuilder WithAnalytics(Action<DeadLetterAnalyticsOptions>? configure = null)
	{
		_analyticsEnabled = true;

		if (configure != null)
		{
			_ = _services.Configure(configure);
		}

		return this;
	}

	/// <summary>
	/// Configures poison detection.
	/// </summary>
	public DeadLetterQueueBuilder WithPoisonDetection(Action<PoisonDetectionOptions> configure)
	{
		_poisonDetectionEnabled = true;
		_ = _services.Configure(configure);
		return this;
	}

	/// <summary>
	/// Disables poison detection.
	/// </summary>
	public DeadLetterQueueBuilder WithoutPoisonDetection()
	{
		_poisonDetectionEnabled = false;
		return this;
	}

	/// <summary>
	/// Configures retry policies.
	/// </summary>
	public DeadLetterQueueBuilder WithRetryPolicies(Action<RetryPolicyOptions> configure)
	{
		_retryPoliciesEnabled = true;
		_ = _services.Configure(configure);
		return this;
	}

	/// <summary>
	/// Disables retry policies.
	/// </summary>
	public DeadLetterQueueBuilder WithoutRetryPolicies()
	{
		_retryPoliciesEnabled = false;
		return this;
	}

	/// <summary>
	/// Adds a custom poison detection rule.
	/// </summary>
	public DeadLetterQueueBuilder AddPoisonRule(IPoisonDetectionRule rule)
	{
		_ = _services.AddPoisonDetectionRule(rule);
		return this;
	}

	/// <summary>
	/// Adds a custom retry strategy.
	/// </summary>
	public DeadLetterQueueBuilder AddRetryStrategy(string messageType, RetryStrategy strategy)
	{
		_ = _services.AddCustomRetryStrategy(messageType, strategy);
		return this;
	}

	/// <summary>
	/// Builds the configuration.
	/// </summary>
	internal void Build()
	{
		// Register core DLQ manager — shared Transport.Abstractions interface
		_services.TryAddSingleton<IDeadLetterQueueManager, PubSubDeadLetterQueueManager>();

		// Register optional components
		if (_poisonDetectionEnabled)
		{
			_ = _services.AddPoisonMessageDetection();
		}

		if (_retryPoliciesEnabled)
		{
			_ = _services.AddRetryPolicies();
		}

		if (_analyticsEnabled)
		{
			_ = _services.AddDeadLetterAnalytics();
		}
	}
}
