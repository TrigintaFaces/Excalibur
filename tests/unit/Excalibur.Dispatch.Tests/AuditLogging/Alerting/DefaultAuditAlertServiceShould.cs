// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Alerting;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.AuditLogging.Alerting;

/// <summary>
/// Unit tests for <see cref="DefaultAuditAlertService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "AuditLogging")]
public sealed class DefaultAuditAlertServiceShould
{
	private static AuditEvent CreateTestEvent(string eventId = "evt-1", string action = "Login") =>
		new()
		{
			EventId = eventId,
			EventType = AuditEventType.Security,
			Action = action,
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
		};

	private static DefaultAuditAlertService CreateService(
		int maxAlertsPerMinute = 100,
		ILogger<DefaultAuditAlertService>? logger = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(new AuditAlertOptions { MaxAlertsPerMinute = maxAlertsPerMinute }),
			logger ?? NullLogger<DefaultAuditAlertService>.Instance);

	#region EvaluateAsync Tests

	[Fact]
	public async Task EvaluateAsync_ShouldTriggerAlert_WhenRuleConditionMatches()
	{
		// Arrange
		var logger = A.Fake<ILogger<DefaultAuditAlertService>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var service = CreateService(logger: logger);

		var rule = new AuditAlertRule
		{
			Name = "FailedLogin",
			Condition = e => e.Action == "Login" && e.Outcome == AuditOutcome.Failure,
			Severity = AuditAlertSeverity.Critical,
		};
		await service.RegisterRuleAsync(rule, CancellationToken.None);

		var evt = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.Security,
			Action = "Login",
			Outcome = AuditOutcome.Failure,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
		};

		// Act
		await service.EvaluateAsync(evt, CancellationToken.None);

		// Assert — verify a warning-level log was emitted for the alert
		A.CallTo(logger).Where(call =>
				call.Method.Name == "Log" &&
				call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappened();
	}

	[Fact]
	public async Task EvaluateAsync_ShouldNotTriggerAlert_WhenConditionDoesNotMatch()
	{
		// Arrange
		var logger = A.Fake<ILogger<DefaultAuditAlertService>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var service = CreateService(logger: logger);

		var rule = new AuditAlertRule
		{
			Name = "DeleteData",
			Condition = e => e.Action == "Delete",
			Severity = AuditAlertSeverity.Critical,
		};
		await service.RegisterRuleAsync(rule, CancellationToken.None);

		// Act
		await service.EvaluateAsync(CreateTestEvent(action: "Read"), CancellationToken.None);

		// Assert — no alert triggered (only rule registration log)
		A.CallTo(logger).Where(call =>
				call.Method.Name == "Log" &&
				call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task EvaluateAsync_ShouldThrow_WhenAuditEventIsNull()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => service.EvaluateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EvaluateAsync_ShouldIsolateRuleExceptions()
	{
		// Arrange — a rule that throws should not prevent other rules
		var service = CreateService();

		var faultyRule = new AuditAlertRule
		{
			Name = "FaultyRule",
			Condition = _ => throw new InvalidOperationException("Rule error"),
		};
		var goodRule = new AuditAlertRule
		{
			Name = "GoodRule",
			Condition = _ => true,
		};

		await service.RegisterRuleAsync(faultyRule, CancellationToken.None);
		await service.RegisterRuleAsync(goodRule, CancellationToken.None);

		// Act — should not throw even though one rule fails
		await Should.NotThrowAsync(
			() => service.EvaluateAsync(CreateTestEvent(), CancellationToken.None));
	}

	#endregion

	#region Rate Limiting Tests

	[Fact]
	public async Task EvaluateAsync_ShouldRateLimit_WhenMaxAlertsPerMinuteReached()
	{
		// Arrange
		var logger = A.Fake<ILogger<DefaultAuditAlertService>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var service = CreateService(maxAlertsPerMinute: 2, logger: logger);

		var rule = new AuditAlertRule
		{
			Name = "AlwaysTrigger",
			Condition = _ => true,
		};
		await service.RegisterRuleAsync(rule, CancellationToken.None);

		// Act — trigger 3 events; only 2 should generate alerts
		await service.EvaluateAsync(CreateTestEvent("evt-1"), CancellationToken.None);
		await service.EvaluateAsync(CreateTestEvent("evt-2"), CancellationToken.None);
		await service.EvaluateAsync(CreateTestEvent("evt-3"), CancellationToken.None);

		// Assert — 2 alert warnings + 1 rate limit warning = 3 total warning logs
		// The 3rd event should be suppressed by rate limiting
		A.CallTo(logger).Where(call =>
				call.Method.Name == "Log" &&
				call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappened(3, Times.Exactly);
	}

	#endregion

	#region RegisterRuleAsync Tests

	[Fact]
	public async Task RegisterRuleAsync_ShouldAddRule()
	{
		// Arrange
		var service = CreateService();
		var rule = new AuditAlertRule
		{
			Name = "TestRule",
			Condition = _ => true,
		};

		// Act — should not throw
		await service.RegisterRuleAsync(rule, CancellationToken.None);
	}

	[Fact]
	public async Task RegisterRuleAsync_ShouldOverrideExistingRule()
	{
		// Arrange
		var logger = A.Fake<ILogger<DefaultAuditAlertService>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var service = CreateService(logger: logger);

		var rule1 = new AuditAlertRule { Name = "Rule1", Condition = _ => false };
		var rule2 = new AuditAlertRule { Name = "Rule1", Condition = _ => true };

		await service.RegisterRuleAsync(rule1, CancellationToken.None);
		await service.RegisterRuleAsync(rule2, CancellationToken.None);

		// Act — evaluate with the overridden rule
		await service.EvaluateAsync(CreateTestEvent(), CancellationToken.None);

		// Assert — rule2 condition (always true) should trigger alert
		A.CallTo(logger).Where(call =>
				call.Method.Name == "Log" &&
				call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappened();
	}

	[Fact]
	public async Task RegisterRuleAsync_ShouldThrow_WhenRuleIsNull()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => service.RegisterRuleAsync(null!, CancellationToken.None));
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_ShouldThrow_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new DefaultAuditAlertService(null!, NullLogger<DefaultAuditAlertService>.Instance));
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new DefaultAuditAlertService(
				Microsoft.Extensions.Options.Options.Create(new AuditAlertOptions()),
				null!));
	}

	#endregion
}
