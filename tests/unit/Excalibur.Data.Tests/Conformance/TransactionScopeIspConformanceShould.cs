// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Conformance;

/// <summary>
/// Structural conformance tests verifying the ITransactionScope ISP split (S569.8)
/// and isolation level handling (S569.10).
/// Sprint 569 -- Task S569.21: Regression guards for S569.8 + S569.10.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Conformance")]
public sealed class TransactionScopeIspConformanceShould
{
	#region S569.8: ITransactionScope ISP split — method count quality gates

	[Fact]
	public void ITransactionScope_HaveAtMostFiveDeclaredMethods()
	{
		// Quality gate counts actual methods only (excludes property getters/setters)
		var methods = typeof(ITransactionScope).GetMethods(
				BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ITransactionScope has {methods.Length} declared methods, exceeding the 5-method quality gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ITransactionScopeAdvanced_HaveAtMostFiveDeclaredMethods()
	{
		var methods = typeof(ITransactionScopeAdvanced).GetMethods(
				BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ITransactionScopeAdvanced has {methods.Length} declared methods, exceeding the 5-method quality gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ITransactionScopeCallbacks_HaveAtMostFiveDeclaredMethods()
	{
		var methods = typeof(ITransactionScopeCallbacks).GetMethods(
				BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ITransactionScopeCallbacks has {methods.Length} declared methods, exceeding the 5-method quality gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	#endregion

	#region S569.8: ISP sub-interfaces are separate from core

	[Fact]
	public void ITransactionScope_NotInheritFromAdvanced()
	{
		// Core interface should NOT inherit from Advanced — consumers opt in via pattern matching
		typeof(ITransactionScopeAdvanced).IsAssignableFrom(typeof(ITransactionScope)).ShouldBeFalse(
			"ITransactionScope must not inherit from ITransactionScopeAdvanced.");
	}

	[Fact]
	public void ITransactionScope_NotInheritFromCallbacks()
	{
		// Core interface should NOT inherit from Callbacks — consumers opt in via pattern matching
		typeof(ITransactionScopeCallbacks).IsAssignableFrom(typeof(ITransactionScope)).ShouldBeFalse(
			"ITransactionScope must not inherit from ITransactionScopeCallbacks.");
	}

	[Fact]
	public void ITransactionScopeAdvanced_NotInheritFromCore()
	{
		// Advanced is a separate ISP sub-interface, not derived from core
		typeof(ITransactionScope).IsAssignableFrom(typeof(ITransactionScopeAdvanced)).ShouldBeFalse(
			"ITransactionScopeAdvanced must not inherit from ITransactionScope.");
	}

	[Fact]
	public void ITransactionScopeCallbacks_NotInheritFromCore()
	{
		// Callbacks is a separate ISP sub-interface, not derived from core
		typeof(ITransactionScope).IsAssignableFrom(typeof(ITransactionScopeCallbacks)).ShouldBeFalse(
			"ITransactionScopeCallbacks must not inherit from ITransactionScope.");
	}

	#endregion

	#region S569.8: ITransactionScopeAdvanced expected members

	[Fact]
	public void ITransactionScopeAdvanced_HaveCreateSavepointAsync()
	{
		var method = typeof(ITransactionScopeAdvanced).GetMethod("CreateSavepointAsync");
		method.ShouldNotBeNull("ITransactionScopeAdvanced must expose CreateSavepointAsync.");
	}

	[Fact]
	public void ITransactionScopeAdvanced_HaveRollbackToSavepointAsync()
	{
		var method = typeof(ITransactionScopeAdvanced).GetMethod("RollbackToSavepointAsync");
		method.ShouldNotBeNull("ITransactionScopeAdvanced must expose RollbackToSavepointAsync.");
	}

	[Fact]
	public void ITransactionScopeAdvanced_HaveReleaseSavepointAsync()
	{
		var method = typeof(ITransactionScopeAdvanced).GetMethod("ReleaseSavepointAsync");
		method.ShouldNotBeNull("ITransactionScopeAdvanced must expose ReleaseSavepointAsync.");
	}

	[Fact]
	public void ITransactionScopeAdvanced_HaveCreateNestedScope()
	{
		var method = typeof(ITransactionScopeAdvanced).GetMethod("CreateNestedScope");
		method.ShouldNotBeNull("ITransactionScopeAdvanced must expose CreateNestedScope.");
	}

	#endregion

	#region S569.8: ITransactionScopeCallbacks expected members

	[Fact]
	public void ITransactionScopeCallbacks_HaveOnCommit()
	{
		var method = typeof(ITransactionScopeCallbacks).GetMethod("OnCommit");
		method.ShouldNotBeNull("ITransactionScopeCallbacks must expose OnCommit.");
	}

	[Fact]
	public void ITransactionScopeCallbacks_HaveOnRollback()
	{
		var method = typeof(ITransactionScopeCallbacks).GetMethod("OnRollback");
		method.ShouldNotBeNull("ITransactionScopeCallbacks must expose OnRollback.");
	}

	[Fact]
	public void ITransactionScopeCallbacks_HaveOnComplete()
	{
		var method = typeof(ITransactionScopeCallbacks).GetMethod("OnComplete");
		method.ShouldNotBeNull("ITransactionScopeCallbacks must expose OnComplete.");
	}

	#endregion

	#region S569.8: TransactionScopeBase implements all three interfaces

	[Fact]
	public void TransactionScopeBase_ImplementITransactionScope()
	{
		typeof(ITransactionScope).IsAssignableFrom(typeof(TransactionScopeBase)).ShouldBeTrue(
			"TransactionScopeBase must implement ITransactionScope.");
	}

	[Fact]
	public void TransactionScopeBase_NotImplementITransactionScopeAdvanced()
	{
		// TransactionScopeBase intentionally does NOT implement ITransactionScopeAdvanced —
		// only providers that support savepoints (SqlServer, Postgres) implement it themselves
		typeof(ITransactionScopeAdvanced).IsAssignableFrom(typeof(TransactionScopeBase)).ShouldBeFalse(
			"TransactionScopeBase should not implement ITransactionScopeAdvanced (opt-in per provider).");
	}

	[Fact]
	public void TransactionScopeBase_ImplementITransactionScopeCallbacks()
	{
		typeof(ITransactionScopeCallbacks).IsAssignableFrom(typeof(TransactionScopeBase)).ShouldBeTrue(
			"TransactionScopeBase must implement ITransactionScopeCallbacks.");
	}

	#endregion

	#region S569.10: Isolation level — no silent fallback

	[Fact]
	public void ITransactionScope_ExposeIsolationLevelProperty()
	{
		// The IsolationLevel property must exist so consumers can inspect the actual level
		var prop = typeof(ITransactionScope).GetProperty("IsolationLevel");
		prop.ShouldNotBeNull("ITransactionScope must expose IsolationLevel property.");
		prop.PropertyType.ShouldBe(typeof(IsolationLevel));
	}

	#endregion
}
