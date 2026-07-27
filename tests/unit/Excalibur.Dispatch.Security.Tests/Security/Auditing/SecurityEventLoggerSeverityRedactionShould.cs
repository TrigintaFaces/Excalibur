// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Regression lock (author != impl) for the security-observability fix: high-severity events MUST be
/// emitted at their severity-derived <see cref="LogLevel"/> (not a fixed Information level that a
/// production logger at Warning+ silently drops), and PII (user id, source ip) MUST be redacted via
/// <see cref="ITelemetrySanitizer"/> before it reaches the log sink.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait("Feature", "Auditing")]
public sealed class SecurityEventLoggerSeverityRedactionShould : IAsyncDisposable
{
	private const string RawUserId = "user-secret-42";
	private const string RawSourceIp = "203.0.113.7";
	private const string RedactedUserId = "REDACTED_USER";
	private const string RedactedSourceIp = "REDACTED_IP";

	private readonly ILogger<SecurityEventLogger> _logger;
	private readonly ISecurityEventStore _eventStore;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly List<(LogLevel Level, string? Message)> _captured = new();
	private readonly SecurityEventLogger _sut;

	public SecurityEventLoggerSeverityRedactionShould()
	{
		_logger = A.Fake<ILogger<SecurityEventLogger>>();
		_eventStore = A.Fake<ISecurityEventStore>();
		_sanitizer = A.Fake<ITelemetrySanitizer>();

		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

		A.CallTo(() => _sanitizer.SanitizeTag("auth.user_id", A<string?>._)).Returns(RedactedUserId);
		A.CallTo(() => _sanitizer.SanitizeTag("auth.source_ip", A<string?>._)).Returns(RedactedSourceIp);

		// Capture every ILogger.Log(level, eventId, state, exception, formatter) call.
		A.CallTo(_logger)
			.Where(c => c.Method.Name == nameof(ILogger.Log))
			.Invokes(c => _captured.Add(((LogLevel)c.Arguments[0]!, c.Arguments[2]?.ToString())));

		_sut = new SecurityEventLogger(_logger, _eventStore, _sanitizer);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.StopAsync(CancellationToken.None);
		await _sut.DisposeAsync();
	}

	private static IMessageContext ContextWithPii()
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = RawUserId,
			["Client:IP"] = RawSourceIp,
		};
		A.CallTo(() => context.Items).Returns(items);
		return context;
	}

	[Theory]
	[InlineData(SecuritySeverity.Critical, LogLevel.Critical)]
	[InlineData(SecuritySeverity.High, LogLevel.Error)]
	[InlineData(SecuritySeverity.Medium, LogLevel.Warning)]
	[InlineData(SecuritySeverity.Low, LogLevel.Information)]
	public async Task EmitAtSeverityDerivedLevel(SecuritySeverity severity, LogLevel expectedLevel)
	{
		// Act — LogToStandardLogger runs synchronously inside LogSecurityEventAsync (no Start needed).
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationFailure,
			"severity mapping",
			severity,
			CancellationToken.None);

		// Assert — RED on the pre-fix fixed LogLevel.Information for Critical/High/Medium.
		_captured.ShouldContain(c => c.Level == expectedLevel);
	}

	[Fact]
	public async Task RedactPiiBeforeItReachesTheLogSink()
	{
		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationFailure,
			"pii redaction",
			SecuritySeverity.Critical,
			CancellationToken.None,
			ContextWithPii());

		// Assert — the raw PII never appears; the redacted token does. RED on the pre-fix raw logging.
		var message = _captured.ShouldHaveSingleItem().Message;
		message.ShouldNotBeNull();
		message.ShouldContain(RedactedUserId);
		message.ShouldContain(RedactedSourceIp);
		message.ShouldNotContain(RawUserId);
		message.ShouldNotContain(RawSourceIp);

		A.CallTo(() => _sanitizer.SanitizeTag("auth.user_id", RawUserId)).MustHaveHappened();
		A.CallTo(() => _sanitizer.SanitizeTag("auth.source_ip", RawSourceIp)).MustHaveHappened();
	}
}
