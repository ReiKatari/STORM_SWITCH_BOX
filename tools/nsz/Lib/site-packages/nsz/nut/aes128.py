from typing import List, Optional, Tuple
from nsz.nut import Print

try:
    from Crypto.Cipher import AES
    from Crypto.Util import Counter
except ModuleNotFoundError as e:
    try:
        # System package managers like apt may install pycryptodome as "Cryptodome" instead of "Crypto"
        from Cryptodome.Cipher import AES
        from Cryptodome.Util import Counter
    except ModuleNotFoundError:
        Print.info("Error: Unable to find 'Crypto' or 'Cryptodome' modules. Please install pycryptodome pip package.")
        raise e

BLOCK_SIZE = 0x10


def sxor(s1: bytes, s2: bytes) -> bytes:
    if len(s1) != len(s2):
        raise AssertionError("sxor operands must have the same length")
    return bytes(x ^ y for x, y in zip(s1, s2))


def _check_aes128_key(key: bytes) -> None:
    if len(key) != BLOCK_SIZE:
        raise ValueError("Key must be of size %X!" % BLOCK_SIZE)


def _check_block_aligned(data: bytes) -> None:
    if len(data) % BLOCK_SIZE:
        raise ValueError("Data is not aligned to block size!")


def _pad_partial_block(block: bytes) -> bytes:
    if len(block) > BLOCK_SIZE:
        raise AssertionError("block is larger than AES block size")
    num_pad = BLOCK_SIZE - len(block)
    return block + bytes([num_pad]) * num_pad


def _xor_block_int(block: bytes, tweak_int: int) -> bytes:
    return (int.from_bytes(block, "big") ^ tweak_int).to_bytes(BLOCK_SIZE, "big")


def _mul_alpha_le(tweak_bytes: bytes) -> bytes:
    t = int.from_bytes(tweak_bytes[::-1], "big") << 1
    if t & (1 << 128):
        t ^= (1 << 128) | 0x87
    return t.to_bytes(BLOCK_SIZE, "big")[::-1]


class AESECB:
    block_size = BLOCK_SIZE
    num_rounds = 10

    def __init__(self, key: bytes):
        _check_aes128_key(key)
        self.key = key
        self._cipher = AES.new(key, AES.MODE_ECB)

    def encrypt(self, data: bytes) -> bytes:
        if not data:
            return b""
        full_len = len(data) & ~(BLOCK_SIZE - 1)
        out = bytearray()
        if full_len:
            out.extend(self._cipher.encrypt(data[:full_len]))
        if full_len != len(data):
            out.extend(self.encrypt_block_ecb(data[full_len:]))
        return bytes(out)

    def decrypt(self, data: bytes) -> bytes:
        _check_block_aligned(data)
        if not data:
            return b""
        return self._cipher.decrypt(data)

    def encrypt_block_ecb(self, block: bytes) -> bytes:
        if len(block) < BLOCK_SIZE:
            block = _pad_partial_block(block)
        elif len(block) != BLOCK_SIZE:
            raise AssertionError("block must be at most one AES block")
        return self._cipher.encrypt(block)

    def decrypt_block_ecb(self, block: bytes) -> bytes:
        if len(block) != BLOCK_SIZE:
            raise AssertionError("block must be exactly one AES block")
        return self._cipher.decrypt(block)


class AESCBC:
    def __init__(self, key: bytes, iv: bytes):
        _check_aes128_key(key)
        if len(iv) != BLOCK_SIZE:
            raise ValueError("IV must be of size %X!" % BLOCK_SIZE)
        self.key = key
        self.aes = AESECB(key)
        self.iv = iv

    def encrypt(self, data: bytes, iv: Optional[bytes] = None) -> bytes:
        """Encrypt block-aligned data in CBC mode."""
        _check_block_aligned(data)
        if iv is None:
            iv = self.iv
        if len(iv) != BLOCK_SIZE:
            raise ValueError("IV must be of size %X!" % BLOCK_SIZE)
        return AES.new(self.key, AES.MODE_CBC, iv=iv).encrypt(data)

    def decrypt(self, data: bytes, iv: Optional[bytes] = None) -> bytes:
        _check_block_aligned(data)
        if iv is None:
            iv = self.iv
        if len(iv) != BLOCK_SIZE:
            raise ValueError("IV must be of size %X!" % BLOCK_SIZE)
        return AES.new(self.key, AES.MODE_CBC, iv=iv).decrypt(data)

    def set_iv(self, iv: bytes) -> None:
        if len(iv) != BLOCK_SIZE:
            raise ValueError("IV must be of size %X!" % BLOCK_SIZE)
        self.iv = iv


class AESCTR:
    def __init__(self, key: bytes, nonce: bytes, offset: int = 0):
        _check_aes128_key(key)
        self.key = key
        self.nonce = nonce
        self.seek(offset)

    def encrypt(self, data: bytes, ctr=None) -> bytes:
        return self.aes.encrypt(data)

    def decrypt(self, data: bytes, ctr=None) -> bytes:
        return self.encrypt(data, ctr)

    def _new_ctr_cipher(self, prefix: bytes, offset: int):
        ctr = Counter.new(64, prefix=prefix, initial_value=(offset >> 4))
        cipher = AES.new(self.key, AES.MODE_CTR, counter=ctr)
        skip = offset & 0xF
        if skip:
            cipher.encrypt(b"\x00" * skip)
        return ctr, cipher

    def seek(self, offset: int) -> None:
        self.ctr, self.aes = self._new_ctr_cipher(self.nonce[0:8], offset)

    def bktrPrefix(self, ctr_val: int) -> bytes:
        return self.nonce[0:4] + ctr_val.to_bytes(4, "big")

    def bktrSeek(self, offset: int, ctr_val: int, virtualOffset: int = 0) -> None:
        self.ctr, self.aes = self._new_ctr_cipher(
            self.bktrPrefix(ctr_val), offset + virtualOffset
        )


class _XTSBase:
    block_size = BLOCK_SIZE

    def _init_xts(
        self, key1: bytes, key2: bytes, sector_size: int, sector: int
    ) -> None:
        _check_aes128_key(key1)
        _check_aes128_key(key2)
        self.K1 = AESECB(key1)
        self.K2 = AESECB(key2)
        self._k1_cipher = AES.new(key1, AES.MODE_ECB)
        self._k2_cipher = AES.new(key2, AES.MODE_ECB)
        self.sector = sector
        self.sector_size = sector_size

    def encrypt(self, data: bytes, sector: Optional[int] = None) -> bytes:
        if sector is None:
            sector = self.sector
        _check_block_aligned(data)
        out = bytearray()
        for pos in range(0, len(data), self.sector_size):
            tweak = self.get_tweak(sector)
            out.extend(self.encrypt_sector(data[pos : pos + self.sector_size], tweak))
            sector += 1
        return bytes(out)

    def decrypt(self, data: bytes, sector: Optional[int] = None) -> bytes:
        if sector is None:
            sector = self.sector
        _check_block_aligned(data)
        out = bytearray()
        for pos in range(0, len(data), self.sector_size):
            tweak = self.get_tweak(sector)
            out.extend(self.decrypt_sector(data[pos : pos + self.sector_size], tweak))
            sector += 1
        return bytes(out)

    def _crypt_sector(self, data: bytes, tweak: int, decrypt: bool) -> bytes:
        _check_block_aligned(data)
        tweak_bytes = self._k2_cipher.encrypt(tweak.to_bytes(BLOCK_SIZE, "big"))

        xored = bytearray(len(data))
        tweaks: List[bytes] = []
        for pos in range(0, len(data), BLOCK_SIZE):
            tweaks.append(tweak_bytes)
            xored[pos : pos + BLOCK_SIZE] = sxor(
                data[pos : pos + BLOCK_SIZE], tweak_bytes
            )
            tweak_bytes = _mul_alpha_le(tweak_bytes)

        crypted = (
            self._k1_cipher.decrypt(bytes(xored))
            if decrypt
            else self._k1_cipher.encrypt(bytes(xored))
        )

        out = bytearray(len(data))
        for i, pos in enumerate(range(0, len(data), BLOCK_SIZE)):
            out[pos : pos + BLOCK_SIZE] = sxor(
                crypted[pos : pos + BLOCK_SIZE], tweaks[i]
            )
        return bytes(out)

    def encrypt_sector(self, data: bytes, tweak: int) -> bytes:
        return self._crypt_sector(data, tweak, decrypt=False)

    def decrypt_sector(self, data: bytes, tweak: int) -> bytes:
        return self._crypt_sector(data, tweak, decrypt=True)

    def get_tweak(self, sector: Optional[int] = None) -> int:
        if sector is None:
            sector = self.sector
        tweak = 0
        for i in range(BLOCK_SIZE):
            tweak |= (sector & 0xFF) << (i * 8)
            sector >>= 8
        return tweak

    def set_sector(self, sector: int) -> None:
        self.sector = sector


class AESXTS(_XTSBase):
    def __init__(self, keys: bytes, sector: int = 0):
        if len(keys) != 32:
            raise ValueError(
                "XTS mode requires a 32-byte key made of two AES-128 keys."
            )
        self.keys = keys[:16], keys[16:]
        self._init_xts(self.keys[0], self.keys[1], sector_size=0x200, sector=sector)


class AESXTSN(_XTSBase):
    """Class for performing Nintendo AES XTS cipher operations"""

    def __init__(
        self, keys: Tuple[bytes, bytes], sector_size: int = 0x200, sector: int = 0
    ):
        if not (isinstance(keys, tuple) and len(keys) == 2):
            raise TypeError("XTS mode requires a tuple of two keys.")
        self.keys = keys
        self._init_xts(keys[0], keys[1], sector_size=sector_size, sector=sector)

    def set_sector_size(self, sector_size: int) -> None:
        self.sector_size = sector_size
