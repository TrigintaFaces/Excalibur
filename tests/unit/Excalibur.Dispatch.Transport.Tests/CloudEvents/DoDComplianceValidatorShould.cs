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
	/// <summary>
	/// The transport under test, as a SERIALIZABLE discriminator.
	/// </summary>
	/// <remarks>
	/// This carried the registration delegate itself (<c>Action&lt;IServiceCollection&gt;</c>), which xUnit
	/// cannot serialize — so Test Explorer could not enumerate the individual rows (xUnit1044) and the five
	/// transports collapsed into one opaque theory. The string key is serializable; the delegate is looked
	/// up from it inside the test body, which keeps all 5 transports × 5 scenarios intact.
	/// </remarks>
	public static TheoryData<string> Transports() =>
	[
		"AWS",
		"Azure",
		"Google",
		"Kafka",
		"RabbitMq",
	];

	private static Action<IServiceCollection> RegistrationFor(string transport) => transport switch
	{
		"AWS" => static s => s.AddAwsCloudEventValidation(),
		"Azure" => static s => s.AddAzureCloudEventValidation(),
		"Google" => static s => s.AddGoogleCloudEventValidation(),
		"Kafka" => static s => s.AddKafkaCloudEventValidation(),
		"RabbitMq" => static s => s.AddRabbitMqCloudEventValidation(),
		_ => throw new ArgumentOutOfRangeException(nameof(transport), transport, "unknown transport"),
	};

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Accept_WhenAllRequiredFieldsPresent(string transport)
	{
		var validator = ResolveValidator(transport);

		var result = await validator(MakeEvent(correlationId: true, userId: true, traceParent: true), CancellationToken.None);

		result.ShouldBeTrue($"{transport}: an envelope with correlationId + userId + traceParent is DoD-compliant");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenCorrelationIdMissing(string transport)
	{
		var validator = ResolveValidator(transport);

		var result = await validator(MakeEvent(correlationId: false, userId: true, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: correlationId is mandatory for DoD compliance");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenUserIdMissing(string transport)
	{
		// The gap: userId was never checked (OR of traceParent/correlationId). An envelope missing userId
		// but carrying the other two previously passed.
		var validator = ResolveValidator(transport);

		var result = await validator(MakeEvent(correlationId: true, userId: false, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: userId is mandatory for DoD compliance (was silently unchecked)");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenTraceParentMissing(string transport)
	{
		var validator = ResolveValidator(transport);

		var result = await validator(MakeEvent(correlationId: true, userId: true, traceParent: false), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: traceParent is mandatory for DoD compliance");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task Reject_WhenOnlyTraceParentPresent(string transport)
	{
		// Directly targets the old OR behavior: traceParent alone previously satisfied the validator.
		var validator = ResolveValidator(transport);

		var result = await validator(MakeEvent(correlationId: false, userId: false, traceParent: true), CancellationToken.None);

		result.ShouldBeFalse($"{transport}: a lone traceParent is NOT DoD-compliant (OR→AND regression)");
	}

	private static Func<CloudEvent, CancellationToken, Task<bool>> ResolveValidator(string transport)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		RegistrationFor(transport)(services);
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
