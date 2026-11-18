# Excalibur.LeaderElection.MongoDB

MongoDB implementation of leader election for Excalibur.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Fencing authority consistency

The strict fencing profile uses primary linearizable reads and majority-acknowledged counter generation. It requires a replica-set service; a single-node replica set is sufficient. True standalone MongoDB rejects linearizable reads: configure a replica set for fencing, or explicitly use the existing WithoutFencingTokens opt-out for ordinary standalone leader election. There is no automatic downgrade to local/secondary reads. This does not claim standalone atomic operations are inherently unsafe. Preserve the separate counter collection/history; restart/restore durability is UNVERIFIED here.

Validation I/O/consistency faults and cancellation propagate without invoking downstream middleware. Discard a pinned token on leadership loss; in-flight writes still need atomic resource-side fencing. These bounds are not real-provider Native or full-release certification.
