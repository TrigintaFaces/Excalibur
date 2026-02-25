// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Versioning.TestData;

#region UserCreatedEvent Upcasters

/// <summary>
/// Upcasts UserCreatedEventV1 to V2: Split Name into FirstName/LastName.
/// </summary>
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

/// <summary>
/// Upcasts UserCreatedEventV2 to V3: Add default Email.
/// </summary>
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

/// <summary>
/// Upcasts UserCreatedEventV3 to V4: Add default CreatedAt.
/// </summary>
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
			CreatedAt = DateTimeOffset.MinValue // Historical events use epoch
		};
	}
}

#endregion UserCreatedEvent Upcasters

#region OrderPlacedEvent Upcasters

/// <summary>
/// Upcasts OrderPlacedEventV1 to V2: Add default Currency.
/// </summary>
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
			Currency = "USD" // Legacy orders were all USD
		};
	}
}

#endregion OrderPlacedEvent Upcasters

#region Test Helpers

/// <summary>
/// A "bad" upcaster with FromVersion >= ToVersion (for error testing).
/// </summary>
public sealed class InvalidDowncastUpcaster : IMessageUpcaster<UserCreatedEventV2, UserCreatedEventV1>
{
	public int FromVersion => 2;
	public int ToVersion => 1; // Invalid: attempting downcast

	public UserCreatedEventV1 Upcast(UserCreatedEventV2 oldMessage)
	{
		return new UserCreatedEventV1
		{
			Id = oldMessage.Id,
			Name = $"{oldMessage.FirstName} {oldMessage.LastName}"
		};
	}
}

/// <summary>
/// An upcaster that tracks call count (for caching tests).
/// </summary>
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

/// <summary>
/// Upcaster for MismatchedTypeEvent - tests that MessageType property is used
/// correctly even when type name would derive a different value.
/// </summary>
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

#endregion Test Helpers
