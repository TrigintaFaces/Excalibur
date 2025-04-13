using System.Text.RegularExpressions;

using Excalibur.Core.Extensions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Extensions;

public class Uuid7ExtensionsShould
{
	[Fact]
	public void GenerateStringShouldReturn25CharacterString()
	{
		// Act
		var uuidString = Uuid7Extensions.GenerateString();

		// Assert
		uuidString.ShouldNotBeNullOrEmpty();
		uuidString.Length.ShouldBe(25);
		Regex.IsMatch(uuidString, "^[a-zA-Z0-9]+$").ShouldBeTrue(); // Ensure it's alphanumeric
	}

	[Fact]
	public void GenerateStringShouldReturnUniqueValues()
	{
		// Act
		var uuid1 = Uuid7Extensions.GenerateString();
		var uuid2 = Uuid7Extensions.GenerateString();

		// Assert
		uuid1.ShouldNotBe(uuid2);
		uuid1.Length.ShouldBe(25);
	}

	[Fact]
	public void GenerateGuidShouldReturnValidGuid()
	{
		// Act
		var uuidGuid = Uuid7Extensions.GenerateGuid();

		// Assert
		_ = uuidGuid.ShouldBeOfType<Guid>();
		uuidGuid.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateGuidShouldReturnUniqueGuids()
	{
		// Act
		var uuid1 = Uuid7Extensions.GenerateGuid();
		var uuid2 = Uuid7Extensions.GenerateGuid();

		// Assert
		uuid1.ShouldNotBe(uuid2);
	}

	[Fact]
	public void GenerateGuidShouldRespectEndiannessParameter()
	{
		// Act
		var uuidWithDefaultEndianness = Uuid7Extensions.GenerateGuid();
		var uuidWithOppositeEndianness = Uuid7Extensions.GenerateGuid(false);

		// Assert
		uuidWithDefaultEndianness.ShouldNotBe(Guid.Empty);
		uuidWithOppositeEndianness.ShouldNotBe(Guid.Empty);
		uuidWithDefaultEndianness.ShouldNotBe(uuidWithOppositeEndianness);
	}
}
