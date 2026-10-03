# Yalnızca yerel geliştirme/test içindir: SSH erişimli, izole Docker-in-Docker sunucusu.
# Host Docker soketine bağlanmaz; içindeki container'lar host'tan tamamen ayrıdır.
FROM docker:28-dind

RUN apk add --no-cache openssh bash sudo \
    && (addgroup -S docker 2>/dev/null || true) \
    && adduser -D -s /bin/bash deploy \
    && addgroup deploy docker \
    && adduser -D -s /bin/bash ops \
    && echo "ops ALL=(root) /usr/local/bin/docker" > /etc/sudoers.d/ops \
    && chmod 0440 /etc/sudoers.d/ops \
    && sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication yes/' /etc/ssh/sshd_config \
    && sed -i 's/^#\?AllowTcpForwarding.*/AllowTcpForwarding no/' /etc/ssh/sshd_config

COPY entrypoint.sh /usr/local/bin/ssh-docker-entrypoint.sh
RUN chmod +x /usr/local/bin/ssh-docker-entrypoint.sh

EXPOSE 22
ENTRYPOINT ["/usr/local/bin/ssh-docker-entrypoint.sh"]
CMD ["dockerd", "--host=unix:///var/run/docker.sock"]
