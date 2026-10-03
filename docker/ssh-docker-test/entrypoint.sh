#!/bin/sh
set -e

: "${SSH_TEST_PASSWORD:?SSH_TEST_PASSWORD tanimlanmali}"

echo "deploy:${SSH_TEST_PASSWORD}" | chpasswd
echo "ops:${SSH_TEST_PASSWORD}" | chpasswd

ssh-keygen -A >/dev/null
/usr/sbin/sshd -e

exec dockerd-entrypoint.sh "$@"
