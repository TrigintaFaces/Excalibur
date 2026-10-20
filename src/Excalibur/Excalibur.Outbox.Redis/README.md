# Excalibur.Outbox.Redis

Redis implementation of the outbox pattern for Excalibur.

## Lease ownership

When `RedisOutboxOptions.ProcessorId` is omitted, empty or whitespace, each store generates
one immutable owner identifier. Claims and owner-checked completions, including dead-lettering,
use that same identifier. If you configure an identifier explicitly, give each independent
processor sharing the key prefix a different value. Sharing a configured identifier makes
those processors indistinguishable to ownership checks.

Ownership checks protect active leases from a different owner. The identifier is not a
fencing token or a per-acquisition generation; it does not provide exactly-once delivery.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
