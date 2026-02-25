// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Excalibur.Dispatch.Benchmarks.Serialization;

#region UserCreatedEvent Upcasters

/// <summary>
/// Upcasts BenchmarkUserCreatedEventV1 to V2: Split Name into FirstName/LastName.
/// </summary>
public sealed class BenchmarkUserCreatedEventV1ToV2Upcaster
	: IMessageUpcaster<BenchmarkUserCreatedEventV1, BenchmarkUserCreatedEventV2>
{
	/// <inheritdoc/>
	public int FromVersion => 1;

	/// <inheritdoc/>
	public int ToVersion => 2;

	/// <inheritdoc/>
	public BenchmarkUserCreatedEventV2 Upcast(BenchmarkUserCreatedEventV1 oldMessage)
	{
		var nameParts = oldMessage.Name.Split(' ', 2);
		return new BenchmarkUserCreatedEventV2
		{
			Id = oldMessage.Id,
			FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
			LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty
		};
	}
}

/// <summary>
/// Upcasts BenchmarkUserCreatedEventV2 to V3: Add default Email.
/// </summary>
public sealed class BenchmarkUserCreatedEventV2ToV3Upcaster
	: IMessageUpcaster<BenchmarkUserCreatedEventV2, BenchmarkUserCreatedEventV3>
{
	/// <inheritdoc/>
	public int FromVersion => 2;

	/// <inheritdoc/>
	public int ToVersion => 3;

	/// <inheritdoc/>
	public BenchmarkUserCreatedEventV3 Upcast(BenchmarkUserCreatedEventV2 oldMessage)
	{
		return new BenchmarkUserCreatedEventV3
		{
			Id = oldMessage.Id,
			FirstName = oldMessage.FirstName,
			LastName = oldMessage.LastName,
			Email = $"{oldMessage.FirstName.ToUpperInvariant()}.{oldMessage.LastName.ToUpperInvariant()}@example.com"
		};
	}
}

/// <summary>
/// Upcasts BenchmarkUserCreatedEventV3 to V4: Add default CreatedAt.
/// </summary>
public sealed class BenchmarkUserCreatedEventV3ToV4Upcaster
	: IMessageUpcaster<BenchmarkUserCreatedEventV3, BenchmarkUserCreatedEventV4>
{
	/// <inheritdoc/>
	public int FromVersion => 3;

	/// <inheritdoc/>
	public int ToVersion => 4;

	/// <inheritdoc/>
	public BenchmarkUserCreatedEventV4 Upcast(BenchmarkUserCreatedEventV3 oldMessage)
	{
		return new BenchmarkUserCreatedEventV4
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
/// Upcasts BenchmarkOrderPlacedEventV1 to V2: Add default Currency.
/// </summary>
public sealed class BenchmarkOrderPlacedEventV1ToV2Upcaster
	: IMessageUpcaster<BenchmarkOrderPlacedEventV1, BenchmarkOrderPlacedEventV2>
{
	/// <inheritdoc/>
	public int FromVersion => 1;

	/// <inheritdoc/>
	public int ToVersion => 2;

	/// <inheritdoc/>
	public BenchmarkOrderPlacedEventV2 Upcast(BenchmarkOrderPlacedEventV1 oldMessage)
	{
		return new BenchmarkOrderPlacedEventV2
		{
			OrderId = oldMessage.OrderId,
			Total = oldMessage.Total,
			Currency = "USD" // Legacy orders were all USD
		};
	}
}

#endregion OrderPlacedEvent Upcasters
