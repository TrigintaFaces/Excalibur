// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace ProtobufSample.Messages;

/// <summary>
/// Event published when an order is placed.
/// Implements Google.Protobuf.IMessage for Protobuf serialization.
/// </summary>
/// <remarks>
/// <para>
/// In production, you would typically use protoc to generate these classes from .proto files.
/// This manual implementation demonstrates the structure for educational purposes.
/// </para>
/// <para>
/// Wire format field tags:
/// - Tag 10 (field 1, wire type 2): Name
/// - Tag 16 (field 2, wire type 0): OrderId (as int for demo)
/// - Tag 26 (field 3, wire type 2): CustomerId
/// - Tag 37 (field 4, wire type 5): TotalAmount (float)
/// </para>
/// </remarks>
public sealed class OrderPlacedEvent : IMessage<OrderPlacedEvent>, IDispatchEvent
{
	private static readonly MessageParser<OrderPlacedEvent> _parser = new(() => new OrderPlacedEvent());

	/// <summary>
	/// Gets the message parser for deserialization.
	/// </summary>
	public static MessageParser<OrderPlacedEvent> Parser => _parser;

	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the unique order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the total order amount.
	/// </summary>
	public float TotalAmount { get; set; }

	/// <summary>
	/// Gets or sets the product name (for simplicity).
	/// </summary>
	public string ProductName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the quantity ordered.
	/// </summary>
	public int Quantity { get; set; }

	/// <inheritdoc/>
	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	public MessageDescriptor Descriptor => throw new NotSupportedException("Sample does not require descriptor metadata");

	/// <inheritdoc/>
	public int CalculateSize()
	{
		int size = 0;

		if (!string.IsNullOrEmpty(EventId))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(EventId);
		}

		if (!string.IsNullOrEmpty(OrderId))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(OrderId);
		}

		if (!string.IsNullOrEmpty(CustomerId))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(CustomerId);
		}

		if (TotalAmount != 0)
		{
			size += 1 + 4; // Fixed32
		}

		if (!string.IsNullOrEmpty(ProductName))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(ProductName);
		}

		if (Quantity != 0)
		{
			size += 1 + CodedOutputStream.ComputeInt32Size(Quantity);
		}

		return size;
	}

	/// <inheritdoc/>
	public void WriteTo(CodedOutputStream output)
	{
		// Field 1: EventId (string)
		if (!string.IsNullOrEmpty(EventId))
		{
			output.WriteRawTag(10); // Field 1, wire type 2 (length-delimited)
			output.WriteString(EventId);
		}

		// Field 2: OrderId (string)
		if (!string.IsNullOrEmpty(OrderId))
		{
			output.WriteRawTag(18); // Field 2, wire type 2
			output.WriteString(OrderId);
		}

		// Field 3: CustomerId (string)
		if (!string.IsNullOrEmpty(CustomerId))
		{
			output.WriteRawTag(26); // Field 3, wire type 2
			output.WriteString(CustomerId);
		}

		// Field 4: TotalAmount (float)
		if (TotalAmount != 0)
		{
			output.WriteRawTag(37); // Field 4, wire type 5 (32-bit)
			output.WriteFloat(TotalAmount);
		}

		// Field 5: ProductName (string)
		if (!string.IsNullOrEmpty(ProductName))
		{
			output.WriteRawTag(42); // Field 5, wire type 2
			output.WriteString(ProductName);
		}

		// Field 6: Quantity (int32)
		if (Quantity != 0)
		{
			output.WriteRawTag(48); // Field 6, wire type 0 (varint)
			output.WriteInt32(Quantity);
		}
	}

	/// <inheritdoc/>
	public void MergeFrom(OrderPlacedEvent other)
	{
		if (other == null)
		{
			return;
		}

		if (!string.IsNullOrEmpty(other.EventId))
		{
			EventId = other.EventId;
		}

		if (!string.IsNullOrEmpty(other.OrderId))
		{
			OrderId = other.OrderId;
		}

		if (!string.IsNullOrEmpty(other.CustomerId))
		{
			CustomerId = other.CustomerId;
		}

		if (other.TotalAmount != 0)
		{
			TotalAmount = other.TotalAmount;
		}

		if (!string.IsNullOrEmpty(other.ProductName))
		{
			ProductName = other.ProductName;
		}

		if (other.Quantity != 0)
		{
			Quantity = other.Quantity;
		}
	}

	/// <inheritdoc/>
	public void MergeFrom(CodedInputStream input)
	{
		uint tag;
		while ((tag = input.ReadTag()) != 0)
		{
			switch (tag)
			{
				case 10:
					EventId = input.ReadString();
					break;
				case 18:
					OrderId = input.ReadString();
					break;
				case 26:
					CustomerId = input.ReadString();
					break;
				case 37:
					TotalAmount = input.ReadFloat();
					break;
				case 42:
					ProductName = input.ReadString();
					break;
				case 48:
					Quantity = input.ReadInt32();
					break;
				default:
					input.SkipLastField();
					break;
			}
		}
	}

	/// <inheritdoc/>
	public OrderPlacedEvent Clone()
	{
		return new OrderPlacedEvent
		{
			EventId = EventId,
			OrderId = OrderId,
			CustomerId = CustomerId,
			TotalAmount = TotalAmount,
			ProductName = ProductName,
			Quantity = Quantity,
		};
	}

	/// <inheritdoc/>
	public bool Equals(OrderPlacedEvent? other)
	{
		if (other == null)
		{
			return false;
		}

		return EventId == other.EventId &&
			   OrderId == other.OrderId &&
			   CustomerId == other.CustomerId &&
			   Math.Abs(TotalAmount - other.TotalAmount) < 0.001f &&
			   ProductName == other.ProductName &&
			   Quantity == other.Quantity;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => Equals(obj as OrderPlacedEvent);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(EventId, OrderId, CustomerId, TotalAmount, ProductName, Quantity);

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"{{ \"eventId\": \"{EventId}\", \"orderId\": \"{OrderId}\", \"customerId\": \"{CustomerId}\", " +
			   $"\"totalAmount\": {TotalAmount}, \"productName\": \"{ProductName}\", \"quantity\": {Quantity} }}";
	}
}

/// <summary>
/// Event published when an order is cancelled.
/// </summary>
public sealed class OrderCancelledEvent : IMessage<OrderCancelledEvent>, IDispatchEvent
{
	private static readonly MessageParser<OrderCancelledEvent> _parser = new(() => new OrderCancelledEvent());

	/// <summary>
	/// Gets the message parser for deserialization.
	/// </summary>
	public static MessageParser<OrderCancelledEvent> Parser => _parser;

	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cancellation reason.
	/// </summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets who cancelled the order.
	/// </summary>
	public string CancelledBy { get; set; } = string.Empty;

	/// <inheritdoc/>
	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	public MessageDescriptor Descriptor => throw new NotSupportedException("Sample does not require descriptor metadata");

	/// <inheritdoc/>
	public int CalculateSize()
	{
		int size = 0;

		if (!string.IsNullOrEmpty(EventId))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(EventId);
		}

		if (!string.IsNullOrEmpty(OrderId))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(OrderId);
		}

		if (!string.IsNullOrEmpty(Reason))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(Reason);
		}

		if (!string.IsNullOrEmpty(CancelledBy))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(CancelledBy);
		}

		return size;
	}

	/// <inheritdoc/>
	public void WriteTo(CodedOutputStream output)
	{
		if (!string.IsNullOrEmpty(EventId))
		{
			output.WriteRawTag(10);
			output.WriteString(EventId);
		}

		if (!string.IsNullOrEmpty(OrderId))
		{
			output.WriteRawTag(18);
			output.WriteString(OrderId);
		}

		if (!string.IsNullOrEmpty(Reason))
		{
			output.WriteRawTag(26);
			output.WriteString(Reason);
		}

		if (!string.IsNullOrEmpty(CancelledBy))
		{
			output.WriteRawTag(34);
			output.WriteString(CancelledBy);
		}
	}

	/// <inheritdoc/>
	public void MergeFrom(OrderCancelledEvent other)
	{
		if (other == null)
		{
			return;
		}

		if (!string.IsNullOrEmpty(other.EventId))
		{
			EventId = other.EventId;
		}

		if (!string.IsNullOrEmpty(other.OrderId))
		{
			OrderId = other.OrderId;
		}

		if (!string.IsNullOrEmpty(other.Reason))
		{
			Reason = other.Reason;
		}

		if (!string.IsNullOrEmpty(other.CancelledBy))
		{
			CancelledBy = other.CancelledBy;
		}
	}

	/// <inheritdoc/>
	public void MergeFrom(CodedInputStream input)
	{
		uint tag;
		while ((tag = input.ReadTag()) != 0)
		{
			switch (tag)
			{
				case 10:
					EventId = input.ReadString();
					break;
				case 18:
					OrderId = input.ReadString();
					break;
				case 26:
					Reason = input.ReadString();
					break;
				case 34:
					CancelledBy = input.ReadString();
					break;
				default:
					input.SkipLastField();
					break;
			}
		}
	}

	/// <inheritdoc/>
	public OrderCancelledEvent Clone()
	{
		return new OrderCancelledEvent
		{
			EventId = EventId,
			OrderId = OrderId,
			Reason = Reason,
			CancelledBy = CancelledBy,
		};
	}

	/// <inheritdoc/>
	public bool Equals(OrderCancelledEvent? other)
	{
		if (other == null)
		{
			return false;
		}

		return EventId == other.EventId &&
			   OrderId == other.OrderId &&
			   Reason == other.Reason &&
			   CancelledBy == other.CancelledBy;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => Equals(obj as OrderCancelledEvent);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(EventId, OrderId, Reason, CancelledBy);

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"{{ \"eventId\": \"{EventId}\", \"orderId\": \"{OrderId}\", \"reason\": \"{Reason}\", \"cancelledBy\": \"{CancelledBy}\" }}";
	}
}
