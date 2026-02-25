// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Tests.Data.ElasticSearch.Security;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.16 (bd-hv7oy):
/// ES Security async void callbacks -> ConcurrentBag&lt;Task&gt; tracking + IAsyncDisposable.
/// Covers SecurityAuditor, FieldEncryptor, and AzureKeyVaultProvider.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SecurityAsyncVoidShould
{
	// ========================= SecurityAuditor =========================

	[Fact]
	public void SecurityAuditor_ImplementsIAsyncDisposable()
	{
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(SecurityAuditor)).ShouldBeTrue(
			"SecurityAuditor should implement IAsyncDisposable for safe timer disposal");
	}

	[Fact]
	public void SecurityAuditor_ImplementsIDisposable()
	{
		typeof(IDisposable).IsAssignableFrom(typeof(SecurityAuditor)).ShouldBeTrue(
			"SecurityAuditor should implement IDisposable");
	}

	[Fact]
	public void SecurityAuditor_HasTrackedTasksField()
	{
		var field = typeof(SecurityAuditor)
			.GetField("_trackedTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("SecurityAuditor should have _trackedTasks field");
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentBag<>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(Task));
	}

	[Fact]
	public void SecurityAuditor_HasVolatileDisposedField()
	{
		var field = typeof(SecurityAuditor)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("SecurityAuditor should have _disposed field");

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_disposed should be volatile for thread-safe disposal (S542.16)");
	}

	// ========================= FieldEncryptor =========================

	[Fact]
	public void FieldEncryptor_ImplementsIAsyncDisposable()
	{
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(FieldEncryptor)).ShouldBeTrue(
			"FieldEncryptor should implement IAsyncDisposable for safe timer disposal");
	}

	[Fact]
	public void FieldEncryptor_ImplementsIDisposable()
	{
		typeof(IDisposable).IsAssignableFrom(typeof(FieldEncryptor)).ShouldBeTrue(
			"FieldEncryptor should implement IDisposable");
	}

	[Fact]
	public void FieldEncryptor_HasTrackedTasksField()
	{
		var field = typeof(FieldEncryptor)
			.GetField("_trackedTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("FieldEncryptor should have _trackedTasks field");
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentBag<>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(Task));
	}

	[Fact]
	public void FieldEncryptor_HasVolatileDisposedField()
	{
		var field = typeof(FieldEncryptor)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("FieldEncryptor should have _disposed field");

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_disposed should be volatile for thread-safe disposal (S542.16)");
	}

	// ========================= AzureKeyVaultProvider =========================

	[Fact]
	public void AzureKeyVaultProvider_ImplementsIAsyncDisposable()
	{
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(AzureKeyVaultProvider)).ShouldBeTrue(
			"AzureKeyVaultProvider should implement IAsyncDisposable for safe timer disposal");
	}

	[Fact]
	public void AzureKeyVaultProvider_ImplementsIDisposable()
	{
		typeof(IDisposable).IsAssignableFrom(typeof(AzureKeyVaultProvider)).ShouldBeTrue(
			"AzureKeyVaultProvider should implement IDisposable");
	}

	[Fact]
	public void AzureKeyVaultProvider_HasTrackedTasksField()
	{
		var field = typeof(AzureKeyVaultProvider)
			.GetField("_trackedTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("AzureKeyVaultProvider should have _trackedTasks field");
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentBag<>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(Task));
	}

	[Fact]
	public void AzureKeyVaultProvider_HasVolatileDisposedField()
	{
		var field = typeof(AzureKeyVaultProvider)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("AzureKeyVaultProvider should have _disposed field");

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_disposed should be volatile for thread-safe disposal (S542.16)");
	}
}
