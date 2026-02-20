// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authentication;

namespace Excalibur.A3.Audit;

/// <summary>
/// Represents information about the user or entity that initiated an action.
/// </summary>
public record RaisedBy
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RaisedBy"/> class.
	/// Initializes a new instance of the <see cref="RaisedBy" /> record with a full name and login.
	/// </summary>
	/// <param name="fullName"> The full name of the user or entity. </param>
	/// <param name="login"> The login identifier of the user or entity. </param>
	public RaisedBy(string fullName, string login)
	{
		FullName = fullName;
		Login = login;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RaisedBy"/> class.
	/// Initializes a new instance of the <see cref="RaisedBy" /> record using an authentication token.
	/// </summary>
	/// <param name="authToken"> The authentication token containing user information. </param>
	public RaisedBy(IAuthenticationToken? authToken)
	{
		FirstName = authToken?.FirstName;
		LastName = authToken?.LastName;
		UserId = authToken?.UserId;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RaisedBy"/> class.
	/// Initializes a new instance of the <see cref="RaisedBy" /> record with default values.
	/// </summary>
	public RaisedBy()
	{
	}

	/// <summary>
	/// Gets or initializes the full name of the user or entity.
	/// </summary>
	/// <value>The full name, or <see langword="null"/> if not set.</value>
	public string? FullName { get; init; }

	/// <summary>
	/// Gets or initializes the login identifier of the user or entity.
	/// </summary>
	/// <value>The login identifier, or <see langword="null"/> if not set.</value>
	public string? Login { get; init; }

	/// <summary>
	/// Gets or initializes the first name of the user or entity.
	/// </summary>
	/// <value>The first name, or <see langword="null"/> if not set.</value>
	public string? FirstName { get; init; }

	/// <summary>
	/// Gets or initializes the last name of the user or entity.
	/// </summary>
	/// <value>The last name, or <see langword="null"/> if not set.</value>
	public string? LastName { get; init; }

	/// <summary>
	/// Gets or initializes the user identifier of the user or entity.
	/// </summary>
	/// <value>The user identifier, or <see langword="null"/> if not set.</value>
	public string? UserId { get; init; }
}
