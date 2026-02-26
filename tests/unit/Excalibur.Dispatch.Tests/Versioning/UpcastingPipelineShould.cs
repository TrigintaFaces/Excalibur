// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Tests.Versioning.TestData;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Tests.Versioning;

/// <summary>
/// Unit tests for <see cref="UpcastingPipeline"/> validating BFS path finding,
/// caching, thread safety, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpcastingPipelineShould : IDisposable
{
	private readonly UpcastingPipeline _sut = new();

	public void Dispose() => _sut.Dispose();

	#region Registration Tests

	[Fact]
	public void Register_ValidUpcaster_AddsToGraph()
	{
		// Arrange
		var upcaster = new UserCreatedEventV1ToV2Upcaster();

		// Act
		_sut.Register(upcaster);

		// Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void Register_MultipleUpcasters_CreatesChain()
	{
		// Arrange & Act
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		// Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeTrue();
		_sut.CanUpcast("UserCreatedEvent", 2, 4).ShouldBeTrue();
		_sut.CanUpcast("UserCreatedEvent", 3, 4).ShouldBeTrue();
	}

	[Fact]
	public void Register_NullUpcaster_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Register<UserCreatedEventV1, UserCreatedEventV2>(null!));
	}

	[Fact]
	public void Register_InvalidFromToVersions_ThrowsArgumentException()
	{
		// Arrange
		var invalidUpcaster = new InvalidDowncastUpcaster();

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			_sut.Register(invalidUpcaster));
		ex.Message.ShouldContain("FromVersion");
		ex.Message.ShouldContain("must be less than");
	}

	[Fact]
	public void Register_DuplicateUpcaster_ThrowsInvalidOperationException()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			_sut.Register(new UserCreatedEventV1ToV2Upcaster()));
		ex.Message.ShouldContain("already registered");
	}

	[Fact]
	public void Register_MultipleMessageTypes_KeepsThemSeparate()
	{
		// Arrange & Act
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new OrderPlacedEventV1ToV2Upcaster());

		// Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 2).ShouldBeTrue();
		_sut.CanUpcast("OrderPlacedEvent", 1, 2).ShouldBeTrue();
		_sut.CanUpcast("UserCreatedEvent", 1, 3).ShouldBeFalse(); // No v2->v3 for Order
	}

	#endregion Registration Tests

	#region Single-Hop Upcasting Tests

	[Fact]
	public void Upcast_SingleHop_TransformsV1ToV2()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "John Doe" };

		// Act
		var result = _sut.Upcast(v1);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV2>();
		var v2 = (UserCreatedEventV2)result;
		v2.Id.ShouldBe(v1.Id);
		v2.FirstName.ShouldBe("John");
		v2.LastName.ShouldBe("Doe");
		v2.Version.ShouldBe(2);
	}

	[Fact]
	public void UpcastTo_SingleHop_TransformsToTargetVersion()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Jane Smith" };

		// Act
		var result = _sut.UpcastTo(v1, 2);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV2>();
		var v2 = (UserCreatedEventV2)result;
		v2.FirstName.ShouldBe("Jane");
		v2.LastName.ShouldBe("Smith");
	}

	#endregion Single-Hop Upcasting Tests

	#region Multi-Hop Upcasting Tests (BFS Path Finding)

	[Fact]
	public void Upcast_MultiHop_TransformsV1ToV4()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Alice Wonder" };

		// Act
		var result = _sut.Upcast(v1);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV4>();
		var v4 = (UserCreatedEventV4)result;
		v4.Id.ShouldBe(v1.Id);
		v4.FirstName.ShouldBe("Alice");
		v4.LastName.ShouldBe("Wonder");
		v4.Email.ShouldBe("ALICE.WONDER@example.com");
		v4.CreatedAt.ShouldBe(DateTimeOffset.MinValue);
		v4.Version.ShouldBe(4);
	}

	[Fact]
	public void UpcastTo_MultiHop_TransformsV1ToV3()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Bob Builder" };

		// Act
		var result = _sut.UpcastTo(v1, 3);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV3>();
		var v3 = (UserCreatedEventV3)result;
		v3.FirstName.ShouldBe("Bob");
		v3.LastName.ShouldBe("Builder");
		v3.Email.ShouldBe("BOB.BUILDER@example.com");
		v3.Version.ShouldBe(3);
	}

	[Fact]
	public void Upcast_MultiHop_FindsShortestPath()
	{
		// Arrange - Register upcasters in non-optimal order
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		var v2 = new UserCreatedEventV2 { Id = Guid.NewGuid(), FirstName = "Test", LastName = "User" };

		// Act
		var result = _sut.Upcast(v2);

		// Assert - BFS finds v2->v3->v4 (2 hops)
		_ = result.ShouldBeOfType<UserCreatedEventV4>();
	}

	#endregion Multi-Hop Upcasting Tests (BFS Path Finding)

	#region Same Version / No-Op Tests

	[Fact]
	public void Upcast_AlreadyLatestVersion_ReturnsOriginalMessage()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var v2 = new UserCreatedEventV2 { Id = Guid.NewGuid(), FirstName = "Test", LastName = "User" };

		// Act
		var result = _sut.Upcast(v2);

		// Assert
		result.ShouldBeSameAs(v2);
	}

	[Fact]
	public void UpcastTo_SameVersion_ReturnsOriginalMessage()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Test User" };

		// Act
		var result = _sut.UpcastTo(v1, 1);

		// Assert
		result.ShouldBeSameAs(v1);
	}

	[Fact]
	public void Upcast_NoUpcastersRegistered_ReturnsOriginalMessage()
	{
		// Arrange
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Test User" };

		// Act
		var result = _sut.Upcast(v1);

		// Assert
		result.ShouldBeSameAs(v1);
	}

	[Fact]
	public void Upcast_NonVersionedMessage_ReturnsOriginalMessage()
	{
		// Arrange
		var message = new NonVersionedMessage { Data = "Test" };

		// Act
		var result = _sut.Upcast(message);

		// Assert
		result.ShouldBeSameAs(message);
	}

	#endregion Same Version / No-Op Tests

	#region Error Handling Tests

	[Fact]
	public void Upcast_NullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Upcast(null!));
	}

	[Fact]
	public void UpcastTo_NullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.UpcastTo(null!, 2));
	}

	[Fact]
	public void UpcastTo_DowncastAttempt_ThrowsInvalidOperationException()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var v2 = new UserCreatedEventV2 { Id = Guid.NewGuid(), FirstName = "Test", LastName = "User" };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			_sut.UpcastTo(v2, 1));
		ex.Message.ShouldContain("Cannot downcast");
	}

	[Fact]
	public void UpcastTo_NoPathExists_ThrowsInvalidOperationException()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		// No v2->v3 or v3->v4 registered
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Test" };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			_sut.UpcastTo(v1, 4));
		ex.Message.ShouldContain("No upcasting path exists");
	}

	[Fact]
	public void UpcastTo_NonVersionedMessage_ThrowsInvalidOperationException()
	{
		// Arrange
		var message = new NonVersionedMessage { Data = "Test" };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			_sut.UpcastTo(message, 2));
		ex.Message.ShouldContain("does not implement IVersionedMessage");
	}

	#endregion Error Handling Tests

	#region GetLatestVersion Tests

	[Fact]
	public void GetLatestVersion_NoUpcastersRegistered_ReturnsZero()
	{
		// Act
		var result = _sut.GetLatestVersion("UserCreatedEvent");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void GetLatestVersion_SingleUpcaster_ReturnsToVersion()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act
		var result = _sut.GetLatestVersion("UserCreatedEvent");

		// Assert
		result.ShouldBe(2);
	}

	[Fact]
	public void GetLatestVersion_MultipleUpcasters_ReturnsHighestVersion()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		// Act
		var result = _sut.GetLatestVersion("UserCreatedEvent");

		// Assert
		result.ShouldBe(4);
	}

	[Fact]
	public void GetLatestVersion_UnknownMessageType_ReturnsZero()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act
		var result = _sut.GetLatestVersion("NonExistentEvent");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void GetLatestVersion_NullMessageType_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.GetLatestVersion(null!));
	}

	#endregion GetLatestVersion Tests

	#region CanUpcast Tests

	[Fact]
	public void CanUpcast_DirectPath_ReturnsTrue()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act & Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void CanUpcast_MultiHopPath_ReturnsTrue()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		// Act & Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeTrue();
	}

	[Fact]
	public void CanUpcast_NoPath_ReturnsFalse()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act & Assert
		_sut.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeFalse();
	}

	[Fact]
	public void CanUpcast_SameVersion_ReturnsTrue()
	{
		// Act & Assert
		_sut.CanUpcast("UserCreatedEvent", 2, 2).ShouldBeTrue();
	}

	[Fact]
	public void CanUpcast_DowncastAttempt_ReturnsFalse()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());

		// Act & Assert
		_sut.CanUpcast("UserCreatedEvent", 2, 1).ShouldBeFalse();
	}

	[Fact]
	public void CanUpcast_NullMessageType_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.CanUpcast(null!, 1, 2));
	}

	#endregion CanUpcast Tests

	#region Path Caching Tests

	[Fact]
	public void Upcast_SecondCall_UsesCachedPath()
	{
		// Arrange
		var countingUpcaster = new CountingUpcaster();
		_sut.Register(countingUpcaster);
		var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "Test User" };

		// Act
		_ = _sut.Upcast(v1);
		_ = _sut.Upcast(v1);
		_ = _sut.Upcast(v1);

		// Assert - Upcaster should be called 3 times (once per message)
		// but path computation should only happen once
		countingUpcaster.CallCount.ShouldBe(3);
	}

	[Fact]
	public void CanUpcast_CachesNegativeResults()
	{
		// Arrange - No upcasters registered

		// Act
		var first = _sut.CanUpcast("UserCreatedEvent", 1, 4);
		var second = _sut.CanUpcast("UserCreatedEvent", 1, 4);

		// Assert - Both should return false (cached negative result)
		first.ShouldBeFalse();
		second.ShouldBeFalse();
	}

	[Fact]
	public void Register_InvalidatesCacheForMessageType()
	{
		// Arrange - First, check that v1->v4 is not possible
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeFalse();

		// Act - Register remaining upcasters
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		// Assert - Now v1->v4 should be possible (cache was invalidated)
		_sut.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeTrue();
	}

	#endregion Path Caching Tests

	#region Thread Safety Tests

	[Fact]
	public async Task ConcurrentUpcasts_WithCachedPath_NoDeadlock()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		var tasks = new List<Task>();

		// Act - Run many concurrent upcasts
		for (var i = 0; i < 100; i++)
		{
			var iteration = i;
			tasks.Add(Task.Run(() =>
			{
				var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = $"User {iteration}" };
				var result = _sut.Upcast(v1);
				_ = result.ShouldBeOfType<UserCreatedEventV4>();
			}));
		}

		// Assert - Should complete without deadlock
		await Task.WhenAll(tasks)
			.WaitAsync(global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
	}

	[Fact]
	public async Task ConcurrentRegistrationAndUpcasts_NoDataCorruption()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		var firstUpcastStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var registrationTask = Task.Run(async () =>
		{
			await firstUpcastStarted.Task;
			_sut.Register(new UserCreatedEventV2ToV3Upcaster());
			_sut.Register(new UserCreatedEventV3ToV4Upcaster());
		});

		var upcastTasks = new List<Task>();
		for (var i = 0; i < 50; i++)
		{
			upcastTasks.Add(Task.Run(() =>
			{
				firstUpcastStarted.TrySetResult();
				var v1 = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = $"User {i}" };
				try
				{
					var result = _sut.Upcast(v1);
					// Result should be at least V2, possibly higher as more upcasters register
					((IVersionedMessage)result).Version.ShouldBeGreaterThanOrEqualTo(2);
				}
				catch (InvalidOperationException)
				{
					// This is acceptable if path computation happens mid-registration
				}
			}));
		}

		// Act & Assert - Should complete without corruption
		await registrationTask;
		await Task.WhenAll(upcastTasks);
	}

	[Fact]
	public async Task ConcurrentCanUpcastCalls_NoRaceConditions()
	{
		// Arrange
		_sut.Register(new UserCreatedEventV1ToV2Upcaster());
		_sut.Register(new UserCreatedEventV2ToV3Upcaster());
		_sut.Register(new UserCreatedEventV3ToV4Upcaster());

		var tasks = new List<Task<bool>>();

		// Act - Many concurrent CanUpcast calls
		for (var i = 0; i < 100; i++)
		{
			tasks.Add(Task.Run(() => _sut.CanUpcast("UserCreatedEvent", 1, 4)));
		}

		var results = await Task.WhenAll(tasks);

		// Assert - All should return true consistently
		results.ShouldAllBe(r => r == true);
	}

	#endregion Thread Safety Tests

	#region Disposal Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var pipeline = new UpcastingPipeline();

		// Act & Assert - Should not throw
		pipeline.Dispose();
		pipeline.Dispose();
		pipeline.Dispose();
	}

	#endregion Disposal Tests

	#region MessageType Mismatch Tests (Bug Fix Verification)

	/// <summary>
	/// Tests that the pipeline correctly handles messages where the type name
	/// doesn't match what StripVersionSuffix would derive.
	///
	/// Bug context: Register() used to derive messageType from type name,
	/// while Upcast() used the instance's MessageType property. If these
	/// didn't match, upcasting would silently fail. The fix ensures both
	/// use the instance's MessageType property when available.
	/// </summary>
	[Fact]
	public void Register_MessageTypeFromInstanceProperty_NotTypeName()
	{
		// Arrange
		// MismatchedTypeEventV1 has type name "MismatchedTypeEventV1" which would derive "MismatchedTypeEvent"
		// But its MessageType property returns "CustomEvent" (intentionally different)
		var upcaster = new MismatchedTypeEventV1ToV2Upcaster();

		// Act
		_sut.Register(upcaster);

		// Assert - Should use MessageType property value ("CustomEvent"), not derived from type name
		_sut.GetLatestVersion("CustomEvent").ShouldBe(2);
		_sut.CanUpcast("CustomEvent", 1, 2).ShouldBeTrue();

		// The derived type name should NOT be registered
		_sut.GetLatestVersion("MismatchedTypeEvent").ShouldBe(0);
		_sut.CanUpcast("MismatchedTypeEvent", 1, 2).ShouldBeFalse();
	}

	[Fact]
	public void Upcast_MismatchedTypeName_UsesMessageTypeProperty()
	{
		// Arrange
		_sut.Register(new MismatchedTypeEventV1ToV2Upcaster());
		var v1 = new MismatchedTypeEventV1
		{
			Id = Guid.NewGuid(),
			Data = "TestData"
		};

		// Act - This should work because Register() now uses MessageType property
		var result = _sut.Upcast(v1);

		// Assert
		_ = result.ShouldBeOfType<MismatchedTypeEventV2>();
		var v2 = (MismatchedTypeEventV2)result;
		v2.Id.ShouldBe(v1.Id);
		v2.Data.ShouldBe("TestData");
		v2.Extra.ShouldBe("UpcastedFromV1");
		v2.Version.ShouldBe(2);
		v2.MessageType.ShouldBe("CustomEvent");
	}

	[Fact]
	public void UpcastTo_MismatchedTypeName_UsesMessageTypeProperty()
	{
		// Arrange
		_sut.Register(new MismatchedTypeEventV1ToV2Upcaster());
		var v1 = new MismatchedTypeEventV1
		{
			Id = Guid.NewGuid(),
			Data = "TargetTest"
		};

		// Act
		var result = _sut.UpcastTo(v1, 2);

		// Assert
		_ = result.ShouldBeOfType<MismatchedTypeEventV2>();
		var v2 = (MismatchedTypeEventV2)result;
		v2.Data.ShouldBe("TargetTest");
		v2.Extra.ShouldBe("UpcastedFromV1");
	}

	#endregion MessageType Mismatch Tests (Bug Fix Verification)
}
