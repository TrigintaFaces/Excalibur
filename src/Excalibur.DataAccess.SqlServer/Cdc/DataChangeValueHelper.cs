using System.Globalization;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Provides helper methods for retrieving typed values from data changes in CDC (Change Data Capture) events.
/// </summary>
public static class DataChangeValueHelper
{
	/// <summary>
	///     Retrieves the new value of a specified column from a collection of data changes and converts it to the specified type.
	/// </summary>
	/// <typeparam name="T"> The expected type of the column's new value. </typeparam>
	/// <param name="changes"> The collection of <see cref="DataChange" /> objects representing changes in data. </param>
	/// <param name="columnName"> The name of the column to retrieve the value from. </param>
	/// <param name="defaultValue"> The default value to return if the column is not found or its value is null. </param>
	/// <returns>
	///     The new value of the column converted to type <typeparamref name="T" />, or the <paramref name="defaultValue" /> if the column
	///     is not found or its value is null.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///     Thrown if the column's value is null and the specified type <typeparamref name="T" /> is not nullable, or if the value cannot be
	///     converted to the specified type.
	/// </exception>
	public static T GetNewValue<T>(this IEnumerable<DataChange> changes, string columnName, T defaultValue = default)
	{
		// Get the type of T and check if it's nullable
		var targetType = typeof(T);
		var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
		var isNullable = Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType;

		// Find the change by column name
		var change = changes.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

		// If change is not found or its NewValue is null, handle it
		if (change?.NewValue == null)
		{
			if (!isNullable)
			{
				throw new InvalidOperationException(
					$"Column '{columnName}' was not found or its value is null, and type '{typeof(T)}' is not nullable.");
			}

			// If nullable, return the default value
			return defaultValue;
		}

		try
		{
			switch (change.NewValue)
			{
				// Handle type conversion based on the new value in the change
				case T value:
					return value;

				default:
					{
						var convertedValue = Convert.ChangeType(change.NewValue, underlyingType, CultureInfo.CurrentCulture);
						return (T)convertedValue;
					}
			}
		}
		catch (InvalidCastException)
		{
			throw new InvalidOperationException($"Failed to convert the value of column '{columnName}' to type '{typeof(T)}'.");
		}
		catch (FormatException)
		{
			throw new InvalidOperationException(
				$"Invalid format for the value of column '{columnName}' when converting to type '{typeof(T)}'.");
		}
	}
}
