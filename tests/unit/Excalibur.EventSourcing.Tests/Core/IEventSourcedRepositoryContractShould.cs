using System.Reflection;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Implementation;

namespace Excalibur.EventSourcing.Tests.Core;

/// <summary>
/// Contract guard tests for IEventSourcedRepository after B.1 (QueryAsync/FindAsync removal).
/// Ensures the interface stays at exactly 5 methods (CQRS write-side only).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class IEventSourcedRepositoryContractShould
{
	private static readonly Type GenericInterfaceType = typeof(IEventSourcedRepository<,>);

	[Fact]
	public void Have_Exactly_Five_Methods()
	{
		// Act
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert — CQRS write-side: GetByIdAsync, SaveAsync (2), ExistsAsync, DeleteAsync
		methods.Length.ShouldBe(5, $"IEventSourcedRepository should have exactly 5 methods, but found: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void Contain_GetByIdAsync()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		methods.ShouldContain(m => m.Name == "GetByIdAsync", "GetByIdAsync must be in the interface");
	}

	[Fact]
	public void Contain_Two_SaveAsync_Overloads()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		var saveOverloads = methods.Where(m => m.Name == "SaveAsync").ToArray();
		saveOverloads.Length.ShouldBe(2, "SaveAsync should have 2 overloads (with and without ETag)");
	}

	[Fact]
	public void Contain_ExistsAsync()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		methods.ShouldContain(m => m.Name == "ExistsAsync", "ExistsAsync must be in the interface");
	}

	[Fact]
	public void Contain_DeleteAsync()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		methods.ShouldContain(m => m.Name == "DeleteAsync", "DeleteAsync must be in the interface");
	}

	[Fact]
	public void Not_Contain_QueryAsync()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		methods.ShouldNotContain(m => m.Name == "QueryAsync", "QueryAsync was removed in B.1 — CQRS write-side only");
	}

	[Fact]
	public void Not_Contain_FindAsync()
	{
		var methods = GenericInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		methods.ShouldNotContain(m => m.Name == "FindAsync", "FindAsync was removed in B.1 — CQRS write-side only");
	}

	[Fact]
	public void Concrete_Class_Should_Not_Have_QueryAsync()
	{
		// QueryAsync fully removed from concrete class — CQRS write-side only
		var method = typeof(EventSourcedRepository<,>).GetMethod("QueryAsync", BindingFlags.Public | BindingFlags.Instance);
		method.ShouldBeNull("QueryAsync was fully removed — CQRS write-side only, no query methods");
	}

	[Fact]
	public void Concrete_Class_Should_Not_Have_FindAsync()
	{
		// FindAsync fully removed from concrete class — CQRS write-side only
		var method = typeof(EventSourcedRepository<,>).GetMethod("FindAsync", BindingFlags.Public | BindingFlags.Instance);
		method.ShouldBeNull("FindAsync was fully removed — CQRS write-side only, no query methods");
	}
}
