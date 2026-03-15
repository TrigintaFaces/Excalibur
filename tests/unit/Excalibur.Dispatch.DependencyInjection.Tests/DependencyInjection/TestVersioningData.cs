// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Versioning.TestData;

public sealed record UserCreatedEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;

	public int Version => 1;
	public string MessageType => "UserCreatedEvent";
}

public sealed record UserCreatedEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;

	public int Version => 2;
	public string MessageType => "UserCreatedEvent";
}

public sealed record UserCreatedEventV3 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;

	public int Version => 3;
	public string MessageType => "UserCreatedEvent";
}

public sealed record UserCreatedEventV4 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public DateTimeOffset CreatedAt { get; init; }

	public int Version => 4;
	public string MessageType => "UserCreatedEvent";
}

public sealed record OrderPlacedEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid OrderId { get; init; }
	public decimal Total { get; init; }

	public int Version => 1;
	public string MessageType => "OrderPlacedEvent";
}

public sealed record OrderPlacedEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid OrderId { get; init; }
	public decimal Total { get; init; }
	public string Currency { get; init; } = "USD";

	public int Version => 2;
	public string MessageType => "OrderPlacedEvent";
}

public sealed record MismatchedTypeEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Data { get; init; } = string.Empty;

	public int Version => 1;
	public string MessageType => "CustomEvent";
}

public sealed record MismatchedTypeEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Data { get; init; } = string.Empty;
	public string Extra { get; init; } = string.Empty;

	public int Version => 2;
	public string MessageType => "CustomEvent";
}

public sealed class UserCreatedEventV1ToV2Upcaster : IMessageUpcaster<UserCreatedEventV1, UserCreatedEventV2>
{
	public int FromVersion => 1;
	public int ToVersion => 2;

	public UserCreatedEventV2 Upcast(UserCreatedEventV1 oldMessage)
	{
		var nameParts = oldMessage.Name.Split(' ', 2);
		return new UserCreatedEventV2
		{
			Id = oldMessage.Id,
			FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
			LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty
		};
	}
}

public sealed class UserCreatedEventV2ToV3Upcaster : IMessageUpcaster<UserCreatedEventV2, UserCreatedEventV3>
{
	public int FromVersion => 2;
	public int ToVersion => 3;

	public UserCreatedEventV3 Upcast(UserCreatedEventV2 oldMessage)
	{
		return new UserCreatedEventV3
		{
			Id = oldMessage.Id,
			FirstName = oldMessage.FirstName,
			LastName = oldMessage.LastName,
			Email = $"{oldMessage.FirstName.ToUpperInvariant()}.{oldMessage.LastName.ToUpperInvariant()}@example.com"
		};
	}
}

public sealed class UserCreatedEventV3ToV4Upcaster : IMessageUpcaster<UserCreatedEventV3, UserCreatedEventV4>
{
	public int FromVersion => 3;
	public int ToVersion => 4;

	public UserCreatedEventV4 Upcast(UserCreatedEventV3 oldMessage)
	{
		return new UserCreatedEventV4
		{
			Id = oldMessage.Id,
			FirstName = oldMessage.FirstName,
			LastName = oldMessage.LastName,
			Email = oldMessage.Email,
			CreatedAt = DateTimeOffset.MinValue
		};
	}
}

public sealed class OrderPlacedEventV1ToV2Upcaster : IMessageUpcaster<OrderPlacedEventV1, OrderPlacedEventV2>
{
	public int FromVersion => 1;
	public int ToVersion => 2;

	public OrderPlacedEventV2 Upcast(OrderPlacedEventV1 oldMessage)
	{
		return new OrderPlacedEventV2
		{
			OrderId = oldMessage.OrderId,
			Total = oldMessage.Total,
			Currency = "USD"
		};
	}
}

public sealed class InvalidDowncastUpcaster : IMessageUpcaster<UserCreatedEventV2, UserCreatedEventV1>
{
	public int FromVersion => 2;
	public int ToVersion => 1;

	public UserCreatedEventV1 Upcast(UserCreatedEventV2 oldMessage)
	{
		return new UserCreatedEventV1
		{
			Id = oldMessage.Id,
			Name = $"{oldMessage.FirstName} {oldMessage.LastName}"
		};
	}
}

public sealed class CountingUpcaster : IMessageUpcaster<UserCreatedEventV1, UserCreatedEventV2>
{
	private int _callCount;

	public int FromVersion => 1;
	public int ToVersion => 2;
	public int CallCount => _callCount;

	public UserCreatedEventV2 Upcast(UserCreatedEventV1 oldMessage)
	{
		_ = Interlocked.Increment(ref _callCount);
		var nameParts = oldMessage.Name.Split(' ', 2);
		return new UserCreatedEventV2
		{
			Id = oldMessage.Id,
			FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
			LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty
		};
	}
}

public sealed class MismatchedTypeEventV1ToV2Upcaster : IMessageUpcaster<MismatchedTypeEventV1, MismatchedTypeEventV2>
{
	public int FromVersion => 1;
	public int ToVersion => 2;

	public MismatchedTypeEventV2 Upcast(MismatchedTypeEventV1 oldMessage)
	{
		return new MismatchedTypeEventV2
		{
			Id = oldMessage.Id,
			Data = oldMessage.Data,
			Extra = "UpcastedFromV1"
		};
	}
}
