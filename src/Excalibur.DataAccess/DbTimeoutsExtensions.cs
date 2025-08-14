using System.Data;

namespace Excalibur.DataAccess;

/// <summary>
///     Provides extension methods for IDbCommand to apply standard timeout settings.
/// </summary>
public static class DbTimeoutsExtensions
{
	/// <summary>
	///     Applies the regular timeout setting to the command.
	/// </summary>
	/// <param name="command">The database command to configure.</param>
	/// <returns>The configured command with the timeout applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the command is null.</exception>
	public static IDbCommand WithRegularTimeout(this IDbCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
		return command;
	}

	/// <summary>
	///     Applies the long-running timeout setting to the command.
	/// </summary>
	/// <param name="command">The database command to configure.</param>
	/// <returns>The configured command with the timeout applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the command is null.</exception>
	public static IDbCommand WithLongRunningTimeout(this IDbCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		command.CommandTimeout = DbTimeouts.LongRunningTimeoutSeconds;
		return command;
	}

	/// <summary>
	///     Applies the extra long-running timeout setting to the command.
	/// </summary>
	/// <param name="command">The database command to configure.</param>
	/// <returns>The configured command with the timeout applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the command is null.</exception>
	public static IDbCommand WithExtraLongRunningTimeout(this IDbCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		command.CommandTimeout = DbTimeouts.ExtraLongRunningTimeoutSeconds;
		return command;
	}

	/// <summary>
	///     Applies a custom timeout setting to the command.
	/// </summary>
	/// <param name="command">The database command to configure.</param>
	/// <param name="timeoutSeconds">The timeout in seconds to apply.</param>
	/// <returns>The configured command with the timeout applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the command is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the timeout value is less than 0.</exception>
	public static IDbCommand WithTimeout(this IDbCommand command, int timeoutSeconds)
	{
		ArgumentNullException.ThrowIfNull(command);

		if (timeoutSeconds < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds,
				"Timeout value must be greater than or equal to 0.");
		}

		command.CommandTimeout = timeoutSeconds;
		return command;
	}
}
