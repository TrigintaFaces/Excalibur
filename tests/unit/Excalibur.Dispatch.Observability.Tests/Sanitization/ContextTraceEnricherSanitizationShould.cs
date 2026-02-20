// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests that <see cref="ContextTraceEnricher"/> correctly uses <see cref="ITelemetrySanitizer"/>
/// for PII fields (user.id, tenant.id) in span tags and W3C baggage propagation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ContextTraceEnricherSanitizationShould : IDisposable
{
	private readonly ActivityListener _listener;
	private readonly ActivitySource _testSource = new("Test.Enricher.Sanitization", "1.0.0");

	public ContextTraceEnricherSanitizationShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		_testSource.Dispose();
	}

	private static string ExpectedHash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return "sha256:" + Convert.ToHexStringLower(bytes);
	}

	private static ContextTraceEnricher CreateEnricher(
		ITelemetrySanitizer? sanitizer = null,
		Action<ContextObservabilityOptions>? configureOptions = null)
	{
		var options = new ContextObservabilityOptions();
		configureOptions?.Invoke(options);

		return new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			MsOptions.Create(options),
			sanitizer ?? new HashingTelemetrySanitizer(
				MsOptions.Create(new TelemetrySanitizerOptions())));
	}

	private static IMessageContext CreateFakeContext(
		string? userId = "alice",
		string? tenantId = "tenant-1",
		string? correlationId = "corr-123",
		string? messageId = "msg-001")
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.UserId).Returns(userId);
		A.CallTo(() => context.TenantId).Returns(tenantId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CausationId).Returns("cause-001");
		A.CallTo(() => context.ExternalId).Returns("ext-001");
		A.CallTo(() => context.Source).Returns("test-source");
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.DeliveryCount).Returns(1);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	#region Span Tag Sanitization

	[Fact]
	public void HashUserIdInSpanTags()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(userId: "alice@example.com");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert
		var userIdTag = activity.GetTagItem("user.id") as string;
		userIdTag.ShouldNotBeNull();
		userIdTag.ShouldStartWith("sha256:");
		userIdTag.ShouldBe(ExpectedHash("alice@example.com"));
	}

	[Fact]
	public void HashTenantIdInSpanTags()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(tenantId: "tenant-acme");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert
		var tenantIdTag = activity.GetTagItem("tenant.id") as string;
		tenantIdTag.ShouldNotBeNull();
		tenantIdTag.ShouldStartWith("sha256:");
		tenantIdTag.ShouldBe(ExpectedHash("tenant-acme"));
	}

	[Fact]
	public void NotHashNonSensitiveSpanTags()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert — message.id and correlation.id should be raw
		activity.GetTagItem("message.id").ShouldBe("msg-001");
		activity.GetTagItem("correlation.id").ShouldBe("corr-123");
	}

	[Fact]
	public void NotSetUserIdTagWhenSanitizerReturnsNull()
	{
		// Arrange — use a sanitizer where user.id is suppressed
		var sanitizer = new HashingTelemetrySanitizer(
			MsOptions.Create(new TelemetrySanitizerOptions
			{
				SuppressedTagNames = ["user.id"],
			}));

		using var enricher = CreateEnricher(sanitizer);
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(userId: "should-be-suppressed");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert — user.id tag should not be set
		activity.GetTagItem("user.id").ShouldBeNull();
	}

	#endregion

	#region IncludeRawPii Pass-Through

	[Fact]
	public void PassThroughRawUserIdWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = new HashingTelemetrySanitizer(
			MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true }));

		using var enricher = CreateEnricher(sanitizer);
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(userId: "alice@example.com");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert — raw value, not hashed
		activity.GetTagItem("user.id").ShouldBe("alice@example.com");
	}

	[Fact]
	public void PassThroughRawTenantIdWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = new HashingTelemetrySanitizer(
			MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true }));

		using var enricher = CreateEnricher(sanitizer);
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(tenantId: "tenant-acme");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("tenant.id").ShouldBe("tenant-acme");
	}

	#endregion

	#region NullTelemetrySanitizer Pass-Through

	[Fact]
	public void PassThroughAllValuesWithNullTelemetrySanitizer()
	{
		// Arrange
		using var enricher = CreateEnricher(NullTelemetrySanitizer.Instance);
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(userId: "alice", tenantId: "acme");

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert — NullTelemetrySanitizer passes all values through
		activity.GetTagItem("user.id").ShouldBe("alice");
		activity.GetTagItem("tenant.id").ShouldBe("acme");
	}

	#endregion

	#region Null/Empty Value Handling

	[Fact]
	public void HandleNullUserIdGracefully()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(userId: null);

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert — null userId returns null from sanitizer, tag should not be set
		activity.GetTagItem("user.id").ShouldBeNull();
	}

	[Fact]
	public void HandleNullTenantIdGracefully()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext(tenantId: null);

		// Act
		enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("tenant.id").ShouldBeNull();
	}

	#endregion

	#region Null Activity Handling

	[Fact]
	public void HandleNullActivityGracefully()
	{
		// Arrange
		using var enricher = CreateEnricher();
		var context = CreateFakeContext();

		// Act — should not throw
		enricher.EnrichActivity(null, context);
	}

	[Fact]
	public void HandleNullContextGracefully()
	{
		// Arrange
		using var enricher = CreateEnricher();
		using var activity = _testSource.StartActivity("test-op");

		// Act — should not throw
		enricher.EnrichActivity(activity, null!);
	}

	#endregion
}
