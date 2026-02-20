// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Data.SqlClient;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcStalePositionDetector"/>.
/// Tests CDC stale position error detection from SQL Server exceptions.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-1710x: CDC Recovery Infrastructure Tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CdcStalePositionDetector")]
public sealed class CdcStalePositionDetectorShould : UnitTestBase
{
	[Fact]
	public void ReturnFalseForNullException()
	{
		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(null);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForNonSqlException()
	{
		// Arrange
		var exception = new InvalidOperationException("Regular exception");

		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Theory]
	[InlineData(22037)] // InvalidFromLsnError
	[InlineData(22029)] // LsnOutOfRangeError
	[InlineData(22911)] // CdcNotEnabledError
	[InlineData(22985)] // CaptureInstanceNotFoundError
	public void DetectStalePositionSqlErrors(int errorNumber)
	{
		// Arrange
		var sqlException = CreateSqlException(errorNumber, "CDC error message");

		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(sqlException);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(1205)]  // Deadlock
	[InlineData(547)]   // FK constraint violation
	[InlineData(2627)]  // PK violation
	[InlineData(8152)]  // String truncation
	public void ReturnFalseForNonStalePositionSqlErrors(int errorNumber)
	{
		// Arrange
		var sqlException = CreateSqlException(errorNumber, "SQL error message");

		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(sqlException);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void DetectStalePositionInNestedInnerException()
	{
		// Arrange
		var sqlException = CreateSqlException(22037, "Stale position");
		var wrapperException = new DataException("Wrapper", sqlException);

		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(wrapperException);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void DetectStalePositionInAggregateException()
	{
		// Arrange
		var sqlException = CreateSqlException(22029, "LSN out of range");
		var regularException = new InvalidOperationException("Other error");
		var aggregateException = new AggregateException(regularException, sqlException);

		// Act
		var result = CdcStalePositionDetector.IsStalePositionException(aggregateException);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnNullErrorNumberForNullException()
	{
		// Act
		var result = CdcStalePositionDetector.GetStalePositionErrorNumber(null);

		// Assert
		result.ShouldBeNull();
	}

	[Theory]
	[InlineData(22037)]
	[InlineData(22029)]
	[InlineData(22911)]
	[InlineData(22985)]
	public void ExtractStalePositionErrorNumber(int expectedErrorNumber)
	{
		// Arrange
		var sqlException = CreateSqlException(expectedErrorNumber, "CDC error");

		// Act
		var result = CdcStalePositionDetector.GetStalePositionErrorNumber(sqlException);

		// Assert
		result.ShouldBe(expectedErrorNumber);
	}

	[Fact]
	public void ReturnNullErrorNumberForNonStalePositionError()
	{
		// Arrange
		var sqlException = CreateSqlException(1205, "Deadlock");

		// Act
		var result = CdcStalePositionDetector.GetStalePositionErrorNumber(sqlException);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void CreateEventArgsWithCorrectProperties()
	{
		// Arrange
		var sqlException = CreateSqlException(22037, "Invalid from_lsn");
		var stalePosition = new byte[] { 0x00, 0x00, 0x01, 0x00 };
		var newPosition = new byte[] { 0x00, 0x00, 0x02, 0x00 };

		// Act
		var eventArgs = CdcStalePositionDetector.CreateEventArgs(
			sqlException,
			"test-processor",
			stalePosition,
			newPosition,
			"dbo_TestTable");

		// Assert
		eventArgs.ProcessorId.ShouldBe("test-processor");
		eventArgs.ProviderType.ShouldBe("SqlServer");
		eventArgs.ReasonCode.ShouldBe(StalePositionReasonCodes.LsnOutOfRange);
		eventArgs.StalePosition.ShouldBe(stalePosition);
		eventArgs.NewPosition.ShouldBe(newPosition);
		eventArgs.CaptureInstance.ShouldBe("dbo_TestTable");
		eventArgs.OriginalException.ShouldBe(sqlException);
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext["SqlErrorNumber"].ShouldBe(22037);
	}

	[Fact]
	public void CreateEventArgsWithUnknownReasonCodeForNonCdcError()
	{
		// Arrange
		var regularException = new InvalidOperationException("Not a CDC error");

		// Act
		var eventArgs = CdcStalePositionDetector.CreateEventArgs(
			regularException,
			"test-processor");

		// Assert
		eventArgs.ReasonCode.ShouldBe(StalePositionReasonCodes.Unknown);
		eventArgs.AdditionalContext.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenCreateEventArgsWithNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			CdcStalePositionDetector.CreateEventArgs(null!, "processor-id"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenCreateEventArgsWithInvalidProcessorId(string? processorId)
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			CdcStalePositionDetector.CreateEventArgs(exception, processorId));
	}

	[Fact]
	public void GetReasonCodeFromSqlException()
	{
		// Arrange
		var sqlException = CreateSqlException(22911, "CDC not enabled");

		// Act
		var reasonCode = CdcStalePositionDetector.GetReasonCode(sqlException);

		// Assert
		reasonCode.ShouldBe(StalePositionReasonCodes.CdcReenabled);
	}

	[Fact]
	public void GetUnknownReasonCodeForNullSqlException()
	{
		// Act
		var reasonCode = CdcStalePositionDetector.GetReasonCode(null);

		// Assert
		reasonCode.ShouldBe(StalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void ExposeStalePositionErrorNumbersSet()
	{
		// Assert
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22037);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22029);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22911);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22985);
		CdcStalePositionDetector.StalePositionErrorNumbers.Count.ShouldBe(4);
	}

	[Fact]
	public void ExposeCorrectErrorConstants()
	{
		// Assert
		CdcStalePositionDetector.InvalidFromLsnError.ShouldBe(22037);
		CdcStalePositionDetector.LsnOutOfRangeError.ShouldBe(22029);
		CdcStalePositionDetector.CdcNotEnabledError.ShouldBe(22911);
		CdcStalePositionDetector.CaptureInstanceNotFoundError.ShouldBe(22985);
	}

	/// <summary>
	/// Creates a SqlException using reflection since it has no public constructor.
	/// Uses multiple strategies to handle different versions of Microsoft.Data.SqlClient.
	/// </summary>
	private static SqlException CreateSqlException(int errorNumber, string message)
	{
		// Strategy: Find and use internal CreateException method
		var createExceptionMethod = typeof(SqlException).GetMethod(
			"CreateException",
			System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
			null,
			[typeof(SqlErrorCollection), typeof(string)],
			null);

		// First, create SqlError using available constructor
		var sqlError = CreateSqlError(errorNumber, message);

		// Create SqlErrorCollection
		var errorCollection = (SqlErrorCollection)Activator.CreateInstance(
			typeof(SqlErrorCollection),
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
			null,
			null,
			null)!;

		// Add error to collection
		var addMethod = typeof(SqlErrorCollection).GetMethod(
			"Add",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

		_ = addMethod!.Invoke(errorCollection, [sqlError]);

		// Try using CreateException static method
		if (createExceptionMethod != null)
		{
			return (SqlException)createExceptionMethod.Invoke(null, [errorCollection, "1.0.0"])!;
		}

		// Fallback: Try direct constructor
		var sqlExceptionCtor = typeof(SqlException).GetConstructors(
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
			.FirstOrDefault();

		if (sqlExceptionCtor != null)
		{
			var ctorParams = sqlExceptionCtor.GetParameters();
			var args = new object?[ctorParams.Length];
			for (int i = 0; i < ctorParams.Length; i++)
			{
				if (ctorParams[i].ParameterType == typeof(SqlErrorCollection))
				{
					args[i] = errorCollection;
				}
				else if (ctorParams[i].ParameterType == typeof(string))
				{
					args[i] = message;
				}
				else if (ctorParams[i].ParameterType == typeof(Exception))
				{
					args[i] = null;
				}
				else if (ctorParams[i].ParameterType == typeof(Guid))
				{
					args[i] = Guid.Empty;
				}
				else if (ctorParams[i].HasDefaultValue)
				{
					args[i] = ctorParams[i].DefaultValue;
				}
				else
				{
					args[i] = null;
				}
			}

			return (SqlException)sqlExceptionCtor.Invoke(args)!;
		}

		throw new InvalidOperationException("Could not find a way to create SqlException via reflection");
	}

	private static SqlError CreateSqlError(int errorNumber, string message)
	{
		// Try different constructor signatures for SqlError
		var ctors = typeof(SqlError).GetConstructors(
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

		foreach (var ctor in ctors)
		{
			var parameters = ctor.GetParameters();

			try
			{
				// Build arguments dynamically based on parameter types
				var args = new object?[parameters.Length];
				for (int i = 0; i < parameters.Length; i++)
				{
					var param = parameters[i];
					if (param.ParameterType == typeof(int))
					{
						// First int is error number, second is line number
						args[i] = (param.Name?.Contains("line", StringComparison.OrdinalIgnoreCase) ?? false) ||
									Array.FindIndex(parameters, 0, i, p => p.ParameterType == typeof(int)) >= 0
							? 1 // line number
							: errorNumber; // error number
					}
					else if (param.ParameterType == typeof(byte))
					{
						args[i] = (byte)0;
					}
					else if (param.ParameterType == typeof(string))
					{
						args[i] = param.Name switch
						{
							"server" => "server",
							"message" or "errorMessage" => message,
							"procedure" or "procName" or "source" => "procedure",
							_ => message
						};
					}
					else if (param.ParameterType == typeof(uint))
					{
						args[i] = (uint)0;
					}
					else if (param.ParameterType == typeof(Exception))
					{
						args[i] = null;
					}
					else if (param.HasDefaultValue)
					{
						args[i] = param.DefaultValue;
					}
					else if (Nullable.GetUnderlyingType(param.ParameterType) != null)
					{
						args[i] = null;
					}
					else
					{
						args[i] = Activator.CreateInstance(param.ParameterType);
					}
				}

				var error = ctor.Invoke(args);
				if (error != null)
				{
					return (SqlError)error;
				}
			}
			catch
			{
				// Try next constructor
			}
		}

		throw new InvalidOperationException("Could not find a way to create SqlError via reflection");
	}
}
