#!/bin/bash
# E2E sunucusu: parolaları ayarlar, host anahtarlarını üretir, dockerd ve sshd'yi başlatır.
set -euo pipefail

: "${E2E_SSH_PASSWORD:?E2E_SSH_PASSWORD tanimlanmali}"

echo "smtest:${E2E_SSH_PASSWORD}" | chpasswd
echo "smnopw:${E2E_SSH_PASSWORD}" | chpasswd

ssh-keygen -A >/dev/null

# Bazı çekirdeklerde (ör. eski host'lar) nftables yoktur; E2E_IPTABLES_LEGACY=1 ile legacy arka uca geçilir.
if [ "${E2E_IPTABLES_LEGACY:-0}" = "1" ]; then
    update-alternatives --set iptables /usr/sbin/iptables-legacy >/dev/null
    update-alternatives --set ip6tables /usr/sbin/ip6tables-legacy >/dev/null
fi

# cgroup v2: dockerd'nin alt cgroup'lara controller dağıtabilmesi için kök süreçleri ayrı bir gruba taşınır
# (docker:dind imajındaki dind betiğiyle aynı yaklaşım).
if [ -f /sys/fs/cgroup/cgroup.controllers ]; then
    mkdir -p /sys/fs/cgroup/init
    xargs -rn1 < /sys/fs/cgroup/cgroup.procs > /sys/fs/cgroup/init/cgroup.procs 2>/dev/null || true
    sed -e 's/ / +/g' -e 's/^/+/' < /sys/fs/cgroup/cgroup.controllers > /sys/fs/cgroup/cgroup.subtree_control 2>/dev/null || true
fi

rm -f /var/run/docker.pid
dockerd --host=unix:///var/run/docker.sock >/var/log/dockerd.log 2>&1 &
DOCKERD_PID=$!

for _ in $(seq 1 60); do
    if docker info >/dev/null 2>&1; then
        break
    fi
    if ! kill -0 "$DOCKERD_PID" 2>/dev/null; then
        echo "dockerd başlatılamadı:" >&2
        tail -n 50 /var/log/dockerd.log >&2
        exit 1
    fi
    sleep 1
done
docker info --format 'dockerd hazır: {{.ServerVersion}} ({{.Driver}})'

# sshd ön planda; konteyner durdurulunca dockerd de sonlandırılır.
trap 'kill -TERM "$DOCKERD_PID" 2>/dev/null; wait "$DOCKERD_PID" 2>/dev/null; exit 0' TERM INT
/usr/sbin/sshd -D -e &
SSHD_PID=$!
wait "$SSHD_PID"
