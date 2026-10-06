# Yalnızca uçtan uca (E2E) testler içindir: gerçek bir Ubuntu 24.04 sunucusunu taklit eden, SSH erişimli,
# kendi Docker motoru olan (Docker-in-Docker, privileged) izole konteyner. Host Docker soketine bağlanmaz.
#
#   docker compose -p sm-e2e --profile e2e up -d --build sm-e2e-ubuntu
#
# Kullanıcılar (parola E2E_SSH_PASSWORD):
#   smtest  — sudo parolalı (sudo grubu), docker grubunda
#   smnopw  — sudo parolasız (NOPASSWD), docker grubunda
#
# Eksikler: systemd PID 1 olarak çalışmaz (journald/systemctl yok); servisler entrypoint tarafından doğrudan başlatılır.
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive \
    TZ=Europe/Istanbul

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg openssh-server sudo git iptables iproute2 procps psmisc \
        python3 tzdata tar gzip xz-utils less nano lsb-release kmod \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc \
    && chmod a+r /etc/apt/keyrings/docker.asc \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
        > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash -G sudo,docker smtest \
    && useradd -m -s /bin/bash -G docker smnopw \
    && echo "smnopw ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/smnopw \
    && chmod 0440 /etc/sudoers.d/smnopw \
    && mkdir -p /run/sshd /srv \
    && sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication yes/' /etc/ssh/sshd_config \
    && sed -i 's/^#\?KbdInteractiveAuthentication.*/KbdInteractiveAuthentication no/' /etc/ssh/sshd_config \
    && sed -i 's/^#\?MaxSessions.*/MaxSessions 50/' /etc/ssh/sshd_config \
    && sed -i 's/^#\?MaxStartups.*/MaxStartups 50:30:100/' /etc/ssh/sshd_config \
    && rm -f /etc/ssh/ssh_host_*

COPY entrypoint.sh /usr/local/bin/e2e-entrypoint.sh
RUN chmod +x /usr/local/bin/e2e-entrypoint.sh

EXPOSE 22
VOLUME ["/var/lib/docker"]
ENTRYPOINT ["/usr/local/bin/e2e-entrypoint.sh"]
