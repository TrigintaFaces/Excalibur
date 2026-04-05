// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class CompositeKeyShould
{
	#region Two-Part Keys

	[Fact]
	public void CreateTwoPartKey()
	{
		var key = CompositeKey.Create("ClientNo", "C100", "AccountNo", "A200");

		key.ShouldBe("CLIENTNO=C100|ACCOUNTNO=A200");
	}

	[Fact]
	public void NormalizeNamesToUppercase()
	{
		var key = CompositeKey.Create("clientNo", "C100", "accountNo", "A200");

		key.ShouldContain("CLIENTNO=");
		key.ShouldContain("ACCOUNTNO=");
	}

	[Fact]
	public void TrimWhitespaceFromNamesAndValues()
	{
		var key = CompositeKey.Create("  ClientNo  ", "  C100  ", "AccountNo", "A200");

		key.ShouldBe("CLIENTNO=C100|ACCOUNTNO=A200");
	}

	#endregion

	#region Three-Part Keys

	[Fact]
	public void CreateThreePartKey()
	{
		var key = CompositeKey.Create("Branch", "B1", "Dept", "D2", "EmpId", "E3");

		key.ShouldBe("BRANCH=B1|DEPT=D2|EMPID=E3");
	}

	#endregion

	#region Params Overload

	[Fact]
	public void CreateKeyFromParams()
	{
		var key = CompositeKey.Create(("A", "1"), ("B", "2"), ("C", "3"), ("D", "4"));

		key.ShouldBe("A=1|B=2|C=3|D=4");
	}

	[Fact]
	public void ThrowOnEmptyParts()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create(ReadOnlySpan<(string, string)>.Empty));
	}

	#endregion

	#region Pipe Escaping

	[Fact]
	public void EscapePipeInValues()
	{
		var key = CompositeKey.Create("Name", "foo|bar", "Id", "123");

		key.ShouldBe("NAME=foo||bar|ID=123");
	}

	#endregion

	#region Validation

	[Fact]
	public void ThrowOnNullName()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create(null!, "value", "Name2", "value2"));
	}

	[Fact]
	public void ThrowOnWhitespaceName()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("  ", "value", "Name2", "value2"));
	}

	[Fact]
	public void ThrowOnNullValue()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("Name1", null!, "Name2", "value2"));
	}

	[Fact]
	public void ThrowOnWhitespaceValue()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("Name1", "  ", "Name2", "value2"));
	}

	#endregion

	#region Name Validation (Reserved Characters)

	[Fact]
	public void ThrowOnPipeInName()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("Name|Bad", "value", "Name2", "value2"));
	}

	[Fact]
	public void ThrowOnEqualsInName()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("Name=Bad", "value", "Name2", "value2"));
	}

	[Fact]
	public void ThrowOnPipeInName_ThreePartOverload()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create("A", "1", "B|C", "2", "D", "3"));
	}

	[Fact]
	public void ThrowOnEqualsInName_ParamsOverload()
	{
		Should.Throw<ArgumentException>(() =>
			CompositeKey.Create(("Name=Bad", "value"), ("Name2", "value2")));
	}

	#endregion

	#region Determinism

	[Fact]
	public void ProduceDeterministicKeys()
	{
		var key1 = CompositeKey.Create("A", "1", "B", "2");
		var key2 = CompositeKey.Create("A", "1", "B", "2");

		key1.ShouldBe(key2);
	}

	[Fact]
	public void ProduceDifferentKeys_ForDifferentOrder()
	{
		var key1 = CompositeKey.Create("A", "1", "B", "2");
		var key2 = CompositeKey.Create("B", "2", "A", "1");

		key1.ShouldNotBe(key2);
	}

	#endregion
}
