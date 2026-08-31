# Excalibur.Dispatch.Transport.IbmMq

IBM MQ transport for Excalibur. Queue-based enterprise messaging with native request/reply over the IBM MQ
managed .NET client (`IBMMQDotnetClient`).

## What this package provides (W2 scaffold)

- `IbmMqOptions` — queue manager, host/port, server-connection channel, queue and reply-to-queue names,
  and receive tuning, validated at startup (`ValidateOnStart`).
- `IIbmMqConnectionProvider` — connects managed-client queue managers from the configured options.
- `AddIbmMqTransport(...)` — registers the connection provider and validated options.

The dispatch sender/receiver that carry messages over IBM MQ build on `IIbmMqConnectionProvider`.

## Usage

```csharp
services.AddIbmMqTransport("ibmmq", o =>
{
    o.QueueManager = "QM1";
    o.Host = "localhost";
    o.Port = 1414;
    o.Channel = "DEV.APP.SVRCONN";
    o.QueueName = "DEV.QUEUE.1";
    // o.ReplyToQueue = "DEV.REPLY.1"; // native request/reply
});
```

Credentials (`UserId`/`Password`) must come from configuration or a secret manager — never commit values.

## Driver license

This package depends on `IBMMQDotnetClient`, IBM's own MQ classes for .NET. It is **not** distributed
under an OSI-approved open-source license. The package declares no SPDX license expression; it sets
`requireLicenseAcceptance` and points at
[IBM's license terms](https://www.ibm.com/support/customer/csol/terms/?id=L-MKDD-7KHY2Q), and it ships
IBM's terms inside the package under `licenses/`. Those terms open:

> IMPORTANT: READ CAREFULLY
>
> Two license agreements are presented below.
>
> 1. IBM International License Agreement for Evaluation of Programs
> 2. IBM International Program License Agreement
>
> If Licensee is obtaining the Program for purposes of productive use (other than evaluation, testing,
> trial "try or buy," or demonstration): By clicking on the "Accept" button below, Licensee accepts the
> IBM International Program License Agreement, without modification.

The evaluation agreement carries a 90-day evaluation period; the International Program License
Agreement is the one the terms name for productive use.

Excalibur redistributes no IBM software and asserts nothing about your entitlement on your behalf.
Referencing this package makes NuGet install the driver into your application, so the obligations are
yours. Read the terms shipped in the driver package, and the terms at the URL above, and confirm your
deployment is covered before you ship.

If those terms do not suit you, the other Excalibur transports carry OSI-approved driver licenses --
see `THIRD-PARTY-NOTICES.md` in the repository for every dependency's license.

