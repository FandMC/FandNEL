function rotateLeft(value: number, shift: number): number {
  return (value << shift) | (value >>> (32 - shift));
}

function add(left: number, right: number): number {
  return (left + right) | 0;
}

function combine(
  value: number,
  accumulator: number,
  next: number,
  word: number,
  shift: number,
  constant: number,
): number {
  return add(rotateLeft(add(add(accumulator, value), add(word, constant)), shift), next);
}

function ff(a: number, b: number, c: number, d: number, word: number, shift: number, constant: number): number {
  return combine((b & c) | (~b & d), a, b, word, shift, constant);
}

function gg(a: number, b: number, c: number, d: number, word: number, shift: number, constant: number): number {
  return combine((b & d) | (c & ~d), a, b, word, shift, constant);
}

function hh(a: number, b: number, c: number, d: number, word: number, shift: number, constant: number): number {
  return combine(b ^ c ^ d, a, b, word, shift, constant);
}

function ii(a: number, b: number, c: number, d: number, word: number, shift: number, constant: number): number {
  return combine(c ^ (b | ~d), a, b, word, shift, constant);
}

export function md5(value: string): string {
  const bytes = new TextEncoder().encode(value);
  const wordCount = Math.ceil((bytes.length + 9) / 64) * 16;
  const words = new Int32Array(wordCount);

  bytes.forEach((byte, index) => {
    words[index >>> 2] |= byte << ((index % 4) * 8);
  });
  words[bytes.length >>> 2] |= 0x80 << ((bytes.length % 4) * 8);
  words[wordCount - 2] = bytes.length * 8;

  let a = 0x67452301;
  let b = -0x10325477;
  let c = -0x67452302;
  let d = 0x10325476;

  for (let offset = 0; offset < words.length; offset += 16) {
    const originalA = a;
    const originalB = b;
    const originalC = c;
    const originalD = d;

    a = ff(a, b, c, d, words[offset], 7, -0x28955b88);
    d = ff(d, a, b, c, words[offset + 1], 12, -0x173848aa);
    c = ff(c, d, a, b, words[offset + 2], 17, 0x242070db);
    b = ff(b, c, d, a, words[offset + 3], 22, -0x3e423112);
    a = ff(a, b, c, d, words[offset + 4], 7, -0x0a83f051);
    d = ff(d, a, b, c, words[offset + 5], 12, 0x4787c62a);
    c = ff(c, d, a, b, words[offset + 6], 17, -0x57cfb9ed);
    b = ff(b, c, d, a, words[offset + 7], 22, -0x2b96aff);
    a = ff(a, b, c, d, words[offset + 8], 7, 0x698098d8);
    d = ff(d, a, b, c, words[offset + 9], 12, -0x74bb0851);
    c = ff(c, d, a, b, words[offset + 10], 17, -42063);
    b = ff(b, c, d, a, words[offset + 11], 22, -0x76a32842);
    a = ff(a, b, c, d, words[offset + 12], 7, 0x6b901122);
    d = ff(d, a, b, c, words[offset + 13], 12, -0x2678e6d);
    c = ff(c, d, a, b, words[offset + 14], 17, -0x5986bc72);
    b = ff(b, c, d, a, words[offset + 15], 22, 0x49b40821);

    a = gg(a, b, c, d, words[offset + 1], 5, -0x09e1da9e);
    d = gg(d, a, b, c, words[offset + 6], 9, -0x3fbf4cc0);
    c = gg(c, d, a, b, words[offset + 11], 14, 0x265e5a51);
    b = gg(b, c, d, a, words[offset], 20, -0x16493856);
    a = gg(a, b, c, d, words[offset + 5], 5, -0x29d0efa3);
    d = gg(d, a, b, c, words[offset + 10], 9, 0x02441453);
    c = gg(c, d, a, b, words[offset + 15], 14, -0x275e197f);
    b = gg(b, c, d, a, words[offset + 4], 20, -0x182c0438);
    a = gg(a, b, c, d, words[offset + 9], 5, 0x21e1cde6);
    d = gg(d, a, b, c, words[offset + 14], 9, -0x3cc8f82a);
    c = gg(c, d, a, b, words[offset + 3], 14, -0x0b2af279);
    b = gg(b, c, d, a, words[offset + 8], 20, 0x455a14ed);
    a = gg(a, b, c, d, words[offset + 13], 5, -0x561c16fb);
    d = gg(d, a, b, c, words[offset + 2], 9, -0x03105c08);
    c = gg(c, d, a, b, words[offset + 7], 14, 0x676f02d9);
    b = gg(b, c, d, a, words[offset + 12], 20, -0x72d5b376);

    a = hh(a, b, c, d, words[offset + 5], 4, -378558);
    d = hh(d, a, b, c, words[offset + 8], 11, -0x788e097f);
    c = hh(c, d, a, b, words[offset + 11], 16, 0x6d9d6122);
    b = hh(b, c, d, a, words[offset + 14], 23, -0x021ac7f4);
    a = hh(a, b, c, d, words[offset + 1], 4, -0x5b4115bc);
    d = hh(d, a, b, c, words[offset + 4], 11, 0x4bdecfa9);
    c = hh(c, d, a, b, words[offset + 7], 16, -0x0944b4a0);
    b = hh(b, c, d, a, words[offset + 10], 23, -0x41404390);
    a = hh(a, b, c, d, words[offset + 13], 4, 0x289b7ec6);
    d = hh(d, a, b, c, words[offset], 11, -0x155ed806);
    c = hh(c, d, a, b, words[offset + 3], 16, -0x2b10cf7b);
    b = hh(b, c, d, a, words[offset + 6], 23, 0x04881d05);
    a = hh(a, b, c, d, words[offset + 9], 4, -0x262b2fc7);
    d = hh(d, a, b, c, words[offset + 12], 11, -0x1924661b);
    c = hh(c, d, a, b, words[offset + 15], 16, 0x1fa27cf8);
    b = hh(b, c, d, a, words[offset + 2], 23, -0x3b53a99b);

    a = ii(a, b, c, d, words[offset], 6, -0x0bd6ddbc);
    d = ii(d, a, b, c, words[offset + 7], 10, 0x432aff97);
    c = ii(c, d, a, b, words[offset + 14], 15, -0x546bdc59);
    b = ii(b, c, d, a, words[offset + 5], 21, -0x036c5fc7);
    a = ii(a, b, c, d, words[offset + 12], 6, 0x655b59c3);
    d = ii(d, a, b, c, words[offset + 3], 10, -0x70f3336e);
    c = ii(c, d, a, b, words[offset + 10], 15, -1051523);
    b = ii(b, c, d, a, words[offset + 1], 21, -0x7a7ba22f);
    a = ii(a, b, c, d, words[offset + 8], 6, 0x6fa87e4f);
    d = ii(d, a, b, c, words[offset + 15], 10, -0x01d31920);
    c = ii(c, d, a, b, words[offset + 6], 15, -0x5cfebcec);
    b = ii(b, c, d, a, words[offset + 13], 21, 0x4e0811a1);
    a = ii(a, b, c, d, words[offset + 4], 6, -0x08ac817e);
    d = ii(d, a, b, c, words[offset + 11], 10, -0x42c50dcb);
    c = ii(c, d, a, b, words[offset + 2], 15, 0x2ad7d2bb);
    b = ii(b, c, d, a, words[offset + 9], 21, -0x14792c6f);

    a = add(a, originalA);
    b = add(b, originalB);
    c = add(c, originalC);
    d = add(d, originalD);
  }

  return [a, b, c, d]
    .flatMap((word) => [0, 8, 16, 24].map((shift) => (word >>> shift) & 0xff))
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}
