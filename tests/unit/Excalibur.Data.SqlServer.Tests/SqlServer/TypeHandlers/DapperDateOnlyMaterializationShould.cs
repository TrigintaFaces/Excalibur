// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.SqlServer.TypeHandlers;

/// <summary>
/// Binds the type handlers to DAPPER, not to themselves.
/// </summary>
/// <remarks>
/// <para>
/// The sibling suites in this folder call <c>handler.Parse(value)</c> directly. That proves the
/// handler's arithmetic and says nothing about whether Dapper ever reaches it — and Dapper ships
/// BUILT-IN type maps for <c>DateOnly</c> and <c>TimeOnly</c> which take precedence over a registered
/// <c>SqlMapper.TypeHandler&lt;T&gt;</c>. With those in place the handler is dead code: the generated
/// materialiser casts the raw provider value straight to the destination type, so a date arriving as a
/// string throws <c>InvalidCastException</c> while a correct, registered, fully-tested handler sits
/// unused. Nothing in a direct-Parse suite can see that.
/// </para>
/// <para>
/// These arms drive Dapper's own <c>GetTypeDeserializer</c> — the same code path a real query
/// compiles — over a reader that returns the provider value. No database is needed because the
/// question is which conversion Dapper CHOOSES, which is decided before any connection is opened.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
public sealed class DapperDateOnlyMaterializationShould
{
	/// <summary>A row whose date arrives from the provider as a string.</summary>
	private sealed class Row
	{
		/// <summary>Gets or sets a required date.</summary>
		public DateOnly Value { get; set; }
	}

	/// <summary>A row whose optional date arrives from the provider as a string.</summary>
	private sealed class NullableRow
	{
		/// <summary>Gets or sets an optional date.</summary>
		public DateOnly? Value { get; set; }
	}

	/// <summary>
	/// A string-valued date must materialise through the registered handler rather than being cast.
	/// </summary>
	[Fact]
	public void MaterialiseAStringDateThroughTheRegisteredHandler()
	{
		_ = new ServiceCollection().AddExcaliburSqlServices();

		using var reader = new SingleValueReader("2026-02-11");
		var deserialize = SqlMapper.GetTypeDeserializer(typeof(Row), reader);

		var row = (Row)deserialize(reader)!;

		row.Value.ShouldBe(new DateOnly(2026, 2, 11));
	}

	/// <summary>The nullable destination must behave the same way.</summary>
	[Fact]
	public void MaterialiseAStringDateIntoANullableDestination()
	{
		_ = new ServiceCollection().AddExcaliburSqlServices();

		using var reader = new SingleValueReader("2026-02-11");
		var deserialize = SqlMapper.GetTypeDeserializer(typeof(NullableRow), reader);

		var row = (NullableRow)deserialize(reader)!;

		row.Value.ShouldBe(new DateOnly(2026, 2, 11));
	}

	/// <summary>
	/// LIVENESS. A provider that returns a real <see cref="DateTime"/> — the common case — must keep
	/// working, so the fix cannot be "intercept everything and hope".
	/// </summary>
	[Fact]
	public void StillMaterialiseADateTimeValuedDate()
	{
		_ = new ServiceCollection().AddExcaliburSqlServices();

		using var reader = new SingleValueReader(new DateTime(2026, 2, 11, 0, 0, 0, DateTimeKind.Utc));
		var deserialize = SqlMapper.GetTypeDeserializer(typeof(Row), reader);

		var row = (Row)deserialize(reader)!;

		row.Value.ShouldBe(new DateOnly(2026, 2, 11));
	}

	/// <summary>A reader over exactly one column named <c>Value</c>.</summary>
	private sealed class SingleValueReader(object value) : IDataReader
	{
		public int FieldCount => 1;

		public object GetValue(int i) => value;

		public Type GetFieldType(int i) => value.GetType();

		public string GetName(int i) => "Value";

		public bool IsDBNull(int i) => value is DBNull;

		public int GetOrdinal(string name) => 0;

		public object this[int i] => GetValue(i);

		public object this[string name] => GetValue(0);

		public bool Read() => true;

		public void Dispose() { }

		public int Depth => 0;

		public bool IsClosed => false;

		public int RecordsAffected => 0;

		public void Close() { }

		public DataTable? GetSchemaTable() => null;

		public bool NextResult() => false;

		public bool GetBoolean(int i) => throw new NotSupportedException();

		public byte GetByte(int i) => throw new NotSupportedException();

		public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) =>
			throw new NotSupportedException();

		public char GetChar(int i) => throw new NotSupportedException();

		public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) =>
			throw new NotSupportedException();

		public IDataReader GetData(int i) => throw new NotSupportedException();

		public string GetDataTypeName(int i) => value.GetType().Name;

		public DateTime GetDateTime(int i) => (DateTime)value;

		public decimal GetDecimal(int i) => (decimal)value;

		public double GetDouble(int i) => (double)value;

		public float GetFloat(int i) => (float)value;

		public Guid GetGuid(int i) => (Guid)value;

		public short GetInt16(int i) => (short)value;

		public int GetInt32(int i) => (int)value;

		public long GetInt64(int i) => (long)value;

		public string GetString(int i) => (string)value;

		public int GetValues(object[] values) => throw new NotSupportedException();
	}
}
