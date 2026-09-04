// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace EnterpriseOrderProcessing.Domain.Events;

[MessageName("Contoso.Customers.CustomerRegistered")]
public sealed record CustomerRegistered(
	Guid CustomerId,
	string Name,
	string Email) : DomainEvent;

[MessageName("Contoso.Customers.CustomerAddressUpdated")]
public sealed record CustomerAddressUpdated(
	Guid CustomerId,
	string Street,
	string City,
	string PostalCode,
	string Country) : DomainEvent;

[MessageName("Contoso.Customers.CustomerDeactivated")]
public sealed record CustomerDeactivated(
	Guid CustomerId,
	string Reason) : DomainEvent;
