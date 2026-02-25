const lz4 = require('lz4');
const msgpack = require('@msgpack/msgpack');

const base64 = 'ktkkNGY4ZmYyMjUtOGE1ZS00ZDcxLTliMGEtMWQzYTZkNmMxZjBhpTQyLjk1';

// Convert from base64 and decompress using LZ4 BlockArray
const compressed = Buffer.from(base64, 'base64');
const decompressed = lz4.decode(compressed);

// Decode MessagePack bytes to JavaScript object
const message = msgpack.decode(decompressed);
console.log(message);
