// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Regression locks for the DoD-compliance CloudEvent envelope validator wired via
/// <c>AddXCloudEventValidation(enableDoDCompliance: true)</c> across all five transports
/// (AWS, Azure, Google, Kafka, RabbitMQ).
/// </summary>
/// <remarks>
/// The documented DoD contract requires <c>correlationid</c>, <c>userid</c>, and <c>traceparent</c> to
/// be <b>all mandatory</b>. The validator previously returned <c>hasTraceParent || hasCorrelationId</c> —
/// an OR that never checked <c>userid</c>, so a non-compliant envelope (missing userId, or having only a
/// traceparent) passed. These failure-path locks assert each required field is enforced with AND
/// semantics on every transport.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class DoDComplianceValidatorShould
{
	public static TheoryData<string, Action<IServiceCollection>> Transports() => new()
	{
		{ "AWS", s => s.AddAwsCloudEventValidation() },
		{ "Azure", s => s.AddAzureCloudEventValidation() },
		{ "Google", s => s.AddGoogleCloudEventValidation() },
		{ "Kafka", s => s.AddKafkaCloudEventValidation() },
		{ "RabbitMq", s => s.AddRabbitMqCloudEventValidation() },
	};

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Accept_WhenAllRequiredFieldsPresent(string transport, Action<IServiceCollection> addValidation)
	{
		var validator = ResolveValidator(addValidation);

		var result = await validator(MakeEvent(correlationId: true, userId: true, traceParent: true), CancellationToken.None);

		result.ShouldBeTrue($"{transport}: an envelope with correlationId + userId + traceParent is DoD-compliant");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenCorrelationIdMissing(string transport, Action<IServiceCollection> addValidation)
	{
		var validator = ResolveValidator(addValidation);

		var result = await validator(MakeEvent(correlationId: false, userId: true, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: correlationId is mandatory for DoD compliance");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenUserIdMissing(string transport, Action<IServiceCollection> addValidation)
	{
		// The gap: userId was never checked (OR of traceParent/correlationId). An envelope missing userId
		// but carrying the other two previously passed.
		var validator = ResolveValidator(addValidation);

		var result = await validator(MakeEvent(correlationId: true, userId: false, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: userId is mandatory for DoD compliance (was silently unchecked)");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenTraceParentMissing(string transport, Action<IServiceCollection> addValidation)
	{
		var validator = ResolveValidator(addValidation);

		var result = await validator(MakeEvent(correlationId: true, userId: true, traceParent: false), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: traceParent is mandatory for DoD compliance");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenOnlyTraceParentPresent(string transport, Action<IServiceCollection> addValidation)
	{
		// Directly targets the old OR behavior: traceParent alone previously satisfied the validator.
		var validator = ResolveValidator(addValidation);

		var result = await validator(MakeEvent(correlationId: false, userId: false, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: a lone traceParent is NOT DoD-compliant (OR→AND regression)");
	}

	private static Func<CloudEvent, CancellationToken, Task<bool>> ResolveValidator(Action<IServiceCollection> addValidation)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		addValidation(services);
		var provider = services.BuildServiceProvider();

		var validator = provider.GetRequiredService<IOptions<CloudEventOptions>>().Value.Schema.CustomValidator;
		return validator.ShouldNotBeNull("DoD compliance must register a CustomValidator");
	}

	private static CloudEvent MakeEvent(bool correlationId, bool userId, bool traceParent)
	{
		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("test://dod-compliance"),
			Type = "com.excalibur.test",
			Time = DateTimeOffset.UtcNow,
		};

		if (correlationId)
		{
			cloudEvent["correlationid"] = "corr-1";
		}

		if (userId)
		{
			cloudEvent["userid"] = "user-1";
		}

		if (traceParent)
		{
			cloudEvent["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
		}

		return cloudEvent;
	}
}
