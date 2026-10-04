#!/usr/bin/env python3
"""Server Manager şifreli yedek (.smbk, SMBK v1) çözücü.

Panel veya Security:MasterKey olmadan, yalnızca yedek işinin şifreleme parolasıyla çalışır.

Kurulum : python3 -m pip install cryptography
Kullanım: python3 backup-decrypt.py yedek.tar.gz.smbk yedek.tar.gz
          (parola sorulur; betiklerde SM_BACKUP_PASSPHRASE ortam değişkeni de kullanılabilir)

Biçim (src/ServerManager.Application/Backups/BackupEncryption.cs ile aynı):
  başlık (36 bayt): "SMBK" | sürüm=1 | bayrak=1 | 2 bayt boş | PBKDF2 tur (uint32 BE) | salt (16) | nonce öneki (8)
  anahtar         : PBKDF2-HMAC-SHA256(parola, salt, tur, 32 bayt)
  parça (tekrar)  : son mu (1) | düz metin uzunluğu (uint32 BE, <= 1 MiB) | şifreli metin | GCM etiketi (16)
  nonce           : nonce öneki || parça sırası (uint32 BE);  AAD: başlık || parça sırası (uint32 BE) || son mu
"""

import getpass
import hashlib
import os
import struct
import sys

try:
    from cryptography.exceptions import InvalidTag
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM
except ImportError:
    sys.exit("cryptography paketi gerekli: python3 -m pip install cryptography")

HEADER_LENGTH = 36
CHUNK_SIZE = 1024 * 1024
TAG_LENGTH = 16
MIN_ITERATIONS = 100_000
MAX_ITERATIONS = 10_000_000


class BackupFormatError(Exception):
    pass


def read_exact(stream, length):
    data = bytearray()
    while len(data) < length:
        chunk = stream.read(length - len(data))
        if not chunk:
            break
        data.extend(chunk)
    return bytes(data)


def decrypt(source, target, passphrase):
    header = read_exact(source, HEADER_LENGTH)
    if len(header) < HEADER_LENGTH or header[:4] != b"SMBK":
        raise BackupFormatError("Dosya şifreli bir Server Manager yedeği değil.")
    if header[4] != 1 or header[5] != 1:
        raise BackupFormatError("Yedek dosyası sürümü desteklenmiyor.")

    (iterations,) = struct.unpack(">I", header[8:12])
    if not MIN_ITERATIONS <= iterations <= MAX_ITERATIONS:
        raise BackupFormatError("Yedek dosyasının başlığı geçersiz.")

    key = hashlib.pbkdf2_hmac("sha256", passphrase.encode("utf-8"), header[12:28], iterations, 32)
    aes = AESGCM(key)
    nonce_prefix = header[28:36]

    counter = 0
    while True:
        record = read_exact(source, 5)
        if len(record) < 5:
            raise BackupFormatError("Yedek dosyası eksik (yarım kalmış).")
        if record[0] not in (0, 1):
            raise BackupFormatError("Yedek dosyası bozuk.")
        is_final = record[0] == 1
        (length,) = struct.unpack(">I", record[1:5])
        if length > CHUNK_SIZE:
            raise BackupFormatError("Yedek dosyası bozuk.")

        sealed = read_exact(source, length + TAG_LENGTH)
        if len(sealed) < length + TAG_LENGTH:
            raise BackupFormatError("Yedek dosyası eksik (yarım kalmış).")

        index = struct.pack(">I", counter)
        aad = header + index + (b"\x01" if is_final else b"\x00")
        try:
            plain = aes.decrypt(nonce_prefix + index, sealed, aad)
        except InvalidTag:
            if counter == 0:
                raise BackupFormatError("Şifre çözülemedi: parola yanlış veya dosya bozuk.")
            raise BackupFormatError("Yedek dosyası bozuk (doğrulama başarısız).")

        target.write(plain)
        if is_final:
            if source.read(1):
                raise BackupFormatError("Yedek dosyasının sonunda beklenmeyen veri var.")
            return
        counter += 1
        if counter > 0xFFFFFFFF:
            raise BackupFormatError("Yedek dosyası bozuk.")


def main():
    if len(sys.argv) != 3:
        sys.exit("Kullanım: python3 backup-decrypt.py <girdi.smbk> <çıktı | ->")

    source_path, target_path = sys.argv[1], sys.argv[2]
    passphrase = os.environ.get("SM_BACKUP_PASSPHRASE") or getpass.getpass("Şifreleme parolası: ")
    partial = None if target_path == "-" else target_path + ".partial"

    try:
        with open(source_path, "rb") as source:
            if partial is None:
                decrypt(source, sys.stdout.buffer, passphrase)
            else:
                with open(partial, "xb") as target:
                    decrypt(source, target, passphrase)
                os.replace(partial, target_path)
    except BackupFormatError as error:
        if partial and os.path.exists(partial):
            os.remove(partial)
        sys.exit(f"Hata: {error}")
    except FileExistsError:
        sys.exit(f"Hata: {partial} zaten var; silip tekrar deneyin.")

    if partial is not None:
        print(f"Çözüldü: {target_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
