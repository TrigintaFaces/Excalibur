// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

// Application business state, not a replacement messaging store.
internal enum PublicationState { Ready, InFlight, RetryableUncertain, Accepted, NeedsReconciliation }
internal sealed record OrderSubmission(OrderSubmissionPlan Plan, PublicationState Publication, long Version,
	Guid Operation, int StageInvocations, string? LastError);

internal sealed class OrderSubmissionPlan
{
	private readonly byte[] _payload;
	private readonly OrderRecord _order;
	public string Id => _order.OrderId;
	public string Fingerprint { get; }
	public string IntentId => $"order-confirmation-{Id}";
	public OrderRecord Order => _order with { };

	public OrderSubmissionPlan(OrderCreated input)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentException.ThrowIfNullOrWhiteSpace(input.OrderId);
		ArgumentException.ThrowIfNullOrWhiteSpace(input.CustomerId);
		ArgumentException.ThrowIfNullOrWhiteSpace(input.ProductId);
		ArgumentException.ThrowIfNullOrWhiteSpace(input.ProductName);
		_order = OrderProcessingService.BuildOrderRecord(input);
		// OrderCreated has immutable strings/scalars; timestamps introduced by processing are excluded.
		Fingerprint = FingerprintFor(input);
		var mail = new EmailNotification
		{
			ToEmail = _order.CustomerId, Subject = $"Order {Id} confirmed",
			Body = string.Create(CultureInfo.InvariantCulture,
				$"Thank you. Order {Id} for {_order.Quantity} x {_order.ProductName} totalling {_order.FinalAmount:F2} is confirmed."),
			NotificationType = "OrderConfirmation",
		};
		_payload = JsonSerializer.SerializeToUtf8Bytes(mail, CommerceJsonContext.Default.EmailNotification);
	}

	internal static string FingerprintFor(OrderCreated input)
	{
		ArgumentNullException.ThrowIfNull(input);
		var canonical = JsonSerializer.SerializeToUtf8Bytes(new CommerceOrderIdentity(1, input), CommerceJsonContext.Default.CommerceOrderIdentity);
		return Convert.ToHexString(SHA256.HashData(canonical));
	}

	public OutboundMessage CreateMessage() => new(nameof(EmailNotification), (byte[])_payload.Clone(),
		NotificationService.Destination, new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["orderId"] = Id, ["emailTo"] = _order.CustomerId, ["emailType"] = "OrderConfirmation",
			["businessFingerprint"] = Fingerprint,
		}) { Id = IntentId };
}

internal sealed record CommerceOrderIdentity(int FormatVersion, OrderCreated Order);

[JsonSerializable(typeof(CommerceOrderIdentity))]
[JsonSerializable(typeof(OrderCreated))]
[JsonSerializable(typeof(OrderRecord))]
[JsonSerializable(typeof(EmailNotification))]
internal sealed partial class CommerceJsonContext : JsonSerializerContext;
