using System.Globalization;

namespace ServerManager.Application.Agent;

/// <summary>Sunucuya agent'ı kuran/kaldıran POSIX sh betiği. Token betiğe gömülmez; kurulum komutunda ortam değişkeniyle verilir.</summary>
public static class AgentInstallScript
{
    public const string AgentPath = "/usr/local/bin/server-manager-agent";
    public const string EnvironmentFilePath = "/etc/server-manager-agent.env";

    private const string Template = """
        #!/bin/sh
        # Server Manager agent kurulumu
        #   Kurulum: curl -fsSL <panel>/api/agent/install.sh | sudo SM_URL=<panel> SM_TOKEN=<token> sh
        #   Kaldırma: curl -fsSL <panel>/api/agent/install.sh | sudo sh -s uninstall
        set -eu

        NAME=server-manager-agent
        BIN=__AGENT_PATH__
        ENV_FILE=__ENV_PATH__
        UNIT_DIR=/etc/systemd/system

        fail() { echo "Hata: $1" >&2; exit 1; }
        has_systemd() { command -v systemctl >/dev/null 2>&1 && [ -d /run/systemd/system ]; }
        remove_cron() {
          if command -v crontab >/dev/null 2>&1; then
            (crontab -l 2>/dev/null | grep -v "$BIN" || true) | crontab - 2>/dev/null || true
          fi
        }

        [ "$(id -u)" -eq 0 ] || fail "root olarak çalıştırın (sudo)."

        if [ "${1:-}" = "uninstall" ]; then
          if has_systemd; then
            systemctl disable --now "$NAME.timer" >/dev/null 2>&1 || true
            rm -f "$UNIT_DIR/$NAME.service" "$UNIT_DIR/$NAME.timer"
            systemctl daemon-reload
          fi
          remove_cron
          rm -f "$BIN" "$ENV_FILE"
          echo "Server Manager agent kaldırıldı."
          exit 0
        fi

        SM_URL="${SM_URL:-}"
        SM_TOKEN="${SM_TOKEN:-}"
        SM_URL="${SM_URL%/}"
        echo "$SM_URL" | grep -Eq '^https?://[A-Za-z0-9.:/_-]+$' || fail "SM_URL geçersiz (ör. https://panel.example.com)."
        echo "$SM_TOKEN" | grep -Eq '^sma_[A-Za-z0-9_-]{43}$' || fail "SM_TOKEN geçersiz."
        command -v curl >/dev/null 2>&1 || fail "curl kurulu değil."

        umask 077
        printf 'SM_URL=%s\nSM_TOKEN=%s\n' "$SM_URL" "$SM_TOKEN" > "$ENV_FILE"
        chmod 600 "$ENV_FILE"

        cat > "$BIN" <<'AGENT'
        #!/bin/sh
        # Server Manager agent __VERSION__
        [ -r __ENV_PATH__ ] || exit 1
        . __ENV_PATH__
        collect() {
        __COLLECT__
        }
        collect 2>/dev/null | curl -fsS --max-time 30 -X POST \
          -H "Authorization: Bearer $SM_TOKEN" \
          -H "X-Agent-Version: __VERSION__" \
          -H "Content-Type: text/plain" \
          --data-binary @- -o /dev/null "$SM_URL/api/agent/report"
        AGENT
        chmod 700 "$BIN"

        if has_systemd; then
          remove_cron
          cat > "$UNIT_DIR/$NAME.service" <<UNIT
        [Unit]
        Description=Server Manager agent raporu
        After=network-online.target
        Wants=network-online.target

        [Service]
        Type=oneshot
        ExecStart=$BIN
        UNIT
          cat > "$UNIT_DIR/$NAME.timer" <<UNIT
        [Unit]
        Description=Server Manager agent zamanlayıcısı

        [Timer]
        OnBootSec=30
        OnUnitActiveSec=__INTERVAL__
        AccuracySec=5

        [Install]
        WantedBy=timers.target
        UNIT
          chmod 644 "$UNIT_DIR/$NAME.service" "$UNIT_DIR/$NAME.timer"
          systemctl daemon-reload
          systemctl enable --now "$NAME.timer" >/dev/null 2>&1
          SCHEDULER="systemd zamanlayıcısı"
        elif command -v crontab >/dev/null 2>&1; then
          (crontab -l 2>/dev/null | grep -v "$BIN" || true; echo "* * * * * $BIN") | crontab -
          SCHEDULER="cron, dakikada bir"
        else
          fail "systemd veya cron bulunamadı."
        fi

        if "$BIN"; then
          echo "Server Manager agent kuruldu ($SCHEDULER); ilk rapor gönderildi."
        else
          echo "Agent kuruldu ($SCHEDULER) ancak ilk rapor gönderilemedi. Panel adresini, token'ı ve ağ erişimini kontrol edin." >&2
          exit 1
        fi

        """;

    public static string Build(string collectionScript) =>
        Template
            .Replace("__COLLECT__", collectionScript.Replace("\r\n", "\n").TrimEnd())
            .Replace("__AGENT_PATH__", AgentPath)
            .Replace("__ENV_PATH__", EnvironmentFilePath)
            .Replace("__VERSION__", AgentRules.CurrentVersion)
            .Replace("__INTERVAL__", AgentRules.ReportIntervalSeconds.ToString(CultureInfo.InvariantCulture))
            .Replace("\r\n", "\n");
}
