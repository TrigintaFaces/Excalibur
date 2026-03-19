// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcDataChangeExtensions"/> (GetValue, GetOldValue, TryGetValue),
/// <see cref="CdcMappingException"/>, <see cref="ICdcEventMapper{TEvent}"/>, and the
/// <c>MapInsert&lt;TEvent, TMapper&gt;()</c> builder API.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcDataChangeExtensionsShould : UnitTestBase
{
	#region GetValue Tests

	[Fact]
	public void GetValue_ReturnsCorrectIntValue()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var result = changes.GetValue<int>("OrderId");

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void GetValue_ReturnsCorrectStringValue()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var result = changes.GetValue<string>("Name");

		// Assert
		result.ShouldBe("Test");
	}

	[Fact]
	public void GetValue_ReturnsCorrectDecimalValue()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var result = changes.GetValue<decimal>("Amount");

		// Assert
		result.ShouldBe(99.99m);
	}

	[Fact]
	public void GetValue_ThrowsCdcMappingException_WhenColumnNotFound()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act & Assert
		var ex = Should.Throw<CdcMappingException>(() => changes.GetValue<int>("NonExistent"));
		ex.Message.ShouldContain("NonExistent");
		ex.Message.ShouldContain("not found");
	}

	[Fact]
	public void GetValue_IsCaseInsensitiveOnColumnName()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var lower = changes.GetValue<int>("orderid");
		var upper = changes.GetValue<int>("ORDERID");
		var mixed = changes.GetValue<int>("OrderId");

		// Assert
		lower.ShouldBe(42);
		upper.ShouldBe(42);
		mixed.ShouldBe(42);
	}

	#endregion

	#region GetOldValue Tests

	[Fact]
	public void GetOldValue_ReturnsCorrectOldValue()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var result = changes.GetOldValue<string>("Name");

		// Assert
		result.ShouldBe("Old");
	}

	[Fact]
	public void GetOldValue_ReturnsCorrectDecimalOldValue()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var result = changes.GetOldValue<decimal>("Amount");

		// Assert
		result.ShouldBe(50.00m);
	}

	[Fact]
	public void GetOldValue_ThrowsCdcMappingException_WhenColumnNotFound()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act & Assert
		var ex = Should.Throw<CdcMappingException>(() => changes.GetOldValue<int>("Missing"));
		ex.Message.ShouldContain("Missing");
		ex.Message.ShouldContain("not found");
	}

	#endregion

	#region TryGetValue Tests

	[Fact]
	public void TryGetValue_ReturnsTrueAndValue_WhenColumnFound()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var found = changes.TryGetValue<int>("OrderId", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe(42);
	}

	[Fact]
	public void TryGetValue_ReturnsFalseAndDefault_WhenColumnNotFound()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var found = changes.TryGetValue<int>("NonExistent", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBe(default);
	}

	[Fact]
	public void TryGetValue_HandlesNullNewValue()
	{
		// Arrange
		var changes = new List<CdcDataChange>
		{
			new() { ColumnName = "NullableCol", NewValue = null, OldValue = "something", DataType = typeof(string) }
		};

		// Act
		var found = changes.TryGetValue<string>("NullableCol", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryGetValue_IsCaseInsensitive()
	{
		// Arrange
		var changes = CreateTestChanges();

		// Act
		var found = changes.TryGetValue<string>("name", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("Test");
	}

	#endregion

	#region CdcMappingException Tests

	[Fact]
	public void CdcMappingException_DefaultConstructor_CreatesInstance()
	{
		// Act
		var ex = new CdcMappingException();

		// Assert
		ex.ShouldNotBeNull();
		ex.Message.ShouldNotBeNullOrEmpty();
		ex.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CdcMappingException_MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Column 'Foo' not found in change data.";

		// Act
		var ex = new CdcMappingException(message);

		// Assert
		ex.Message.ShouldBe(message);
		ex.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CdcMappingException_MessageAndInnerConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Mapping failed";
		var inner = new InvalidCastException("Cannot cast");

		// Act
		var ex = new CdcMappingException(message, inner);

		// Assert
		ex.Message.ShouldBe(message);
		ex.InnerException.ShouldBe(inner);
		ex.InnerException.ShouldBeOfType<InvalidCastException>();
	}

	[Fact]
	public void CdcMappingException_DerivesFromException()
	{
		// Act
		var ex = new CdcMappingException("test");

		// Assert
		ex.ShouldBeAssignableTo<Exception>();
	}

	#endregion

	#region ICdcEventMapper Tests

	[Fact]
	public void ICdcEventMapper_Implementation_CanMapChangesToEvent()
	{
		// Arrange
		var mapper = new TestMapper();
		var changes = CreateTestChanges();

		// Act
		var result = mapper.Map(changes, CdcChangeType.Insert);

		// Assert
		result.ShouldNotBeNull();
		result.OrderId.ShouldBe(42);
		result.Name.ShouldBe("Test");
	}

	[Fact]
	public void ICdcEventMapper_ReceivesChangeType()
	{
		// Arrange
		var mapper = new ChangeTypeCapturingMapper();
		var changes = CreateTestChanges();

		// Act
		_ = mapper.Map(changes, CdcChangeType.Update);

		// Assert
		mapper.CapturedChangeType.ShouldBe(CdcChangeType.Update);
	}

	#endregion

	#region MapInsert<TEvent, TMapper> Builder API Tests

	/// <remarks>
	/// These tests use AddCdcProcessor WITHOUT UseSqlServer to bypass the
	/// known TryAddEnumerable factory-delegate bug in RegisterAutoMappingCallback
	/// (ServiceDescriptor.Singleton with lambda makes implementationType = interface,
	/// which TryAddEnumerable rejects as indistinguishable).
	/// The provider-agnostic path still exercises ICdcTableBuilder.MapInsert/Update/Delete
	/// and verifies mapper types get registered in DI.
	/// </remarks>
	[Fact]
	public void MapInsert_WithMapper_RegistersEventMappingAndMapperType()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- use provider-agnostic path (no UseSqlServer)
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable("dbo.Orders", t => t.MapInsert<TestEvent, TestMapper>());
		});
		var provider = services.BuildServiceProvider();

		// Assert -- options contain the event mapping
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Insert);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(TestEvent));

		// Assert -- mapper type registered in DI via TryAddScoped
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestMapper) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void MapUpdate_WithMapper_RegistersEventMappingAndMapperType()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable("dbo.Orders", t => t.MapUpdate<TestEvent, TestMapper>());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Update);
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(TestEvent));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestMapper) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void MapDelete_WithMapper_RegistersEventMappingAndMapperType()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable("dbo.Orders", t => t.MapDelete<TestEvent, TestMapper>());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Delete);
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(TestEvent));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestMapper) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void MapInsert_WithoutMapper_StillWorksForBackwardCompat()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- original MapInsert<TEvent>() without mapper
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable("dbo.Orders", t => t.MapInsert<TestEvent>());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Insert);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(TestEvent));
	}

	[Fact]
	public void MapInsert_WithMapper_CanChainAllThreeChangeTypes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable("dbo.Orders", t =>
				t.MapInsert<TestEvent, TestMapper>()
				 .MapUpdate<TestEvent, TestMapper>()
				 .MapDelete<TestEvent, TestMapper>());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.Count.ShouldBe(3);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(TestEvent));
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(TestEvent));
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(TestEvent));
	}

	#endregion

	#region Test Helpers

	private static List<CdcDataChange> CreateTestChanges() =>
	[
		new() { ColumnName = "OrderId", NewValue = 42, OldValue = null, DataType = typeof(int) },
		new() { ColumnName = "Name", NewValue = "Test", OldValue = "Old", DataType = typeof(string) },
		new() { ColumnName = "Amount", NewValue = 99.99m, OldValue = 50.00m, DataType = typeof(decimal) }
	];

	private sealed class TestEvent
	{
		public int OrderId { get; init; }
		public string Name { get; init; } = "";
	}

	private sealed class TestMapper : ICdcEventMapper<TestEvent>
	{
		public TestEvent Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType)
		{
			return new TestEvent
			{
				OrderId = changes.GetValue<int>("OrderId"),
				Name = changes.GetValue<string>("Name")
			};
		}
	}

	private sealed class ChangeTypeCapturingMapper : ICdcEventMapper<TestEvent>
	{
		public CdcChangeType CapturedChangeType { get; private set; }

		public TestEvent Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType)
		{
			CapturedChangeType = changeType;
			return new TestEvent
			{
				OrderId = changes.GetValue<int>("OrderId"),
				Name = changes.GetValue<string>("Name")
			};
		}
	}

	#endregion
}
