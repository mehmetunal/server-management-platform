# Mag Server Manager — çok aşamalı imaj.
#   docker build -t mag-server-manager .
#   docker compose --profile app up -d        (veritabanı + uygulama)
#
# Ön yüz varlıkları (wwwroot/lib, derlenmiş CSS, eklenti Content/ klasörleri) depoda hazır olduğundan
# imaj derlemesi Node.js gerektirmez; stilleri değiştirdiyseniz önce yerelde `npm run build` çalıştırın.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Bağımlılık katmanı: proje dosyaları değişmedikçe restore önbellekten gelir.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ServerManager.slnx ./
COPY src/ServerManager.Domain/ServerManager.Domain.csproj src/ServerManager.Domain/
COPY src/ServerManager.Application/ServerManager.Application.csproj src/ServerManager.Application/
COPY src/ServerManager.Infrastructure/ServerManager.Infrastructure.csproj src/ServerManager.Infrastructure/
COPY src/ServerManager.Web.Framework/ServerManager.Web.Framework.csproj src/ServerManager.Web.Framework/
COPY src/ServerManager.Web/ServerManager.Web.csproj src/ServerManager.Web/
COPY src/Plugins/Directory.Build.props src/Plugins/Directory.Build.targets src/Plugins/
COPY src/Plugins/ServerManager.Plugin.Cloud.DigitalOcean/ServerManager.Plugin.Cloud.DigitalOcean.csproj src/Plugins/ServerManager.Plugin.Cloud.DigitalOcean/
COPY src/Plugins/ServerManager.Plugin.Cloud.Hetzner/ServerManager.Plugin.Cloud.Hetzner.csproj src/Plugins/ServerManager.Plugin.Cloud.Hetzner/
COPY src/Plugins/ServerManager.Plugin.Cloud.Linode/ServerManager.Plugin.Cloud.Linode.csproj src/Plugins/ServerManager.Plugin.Cloud.Linode/
COPY src/Plugins/ServerManager.Plugin.Cloud.Scaleway/ServerManager.Plugin.Cloud.Scaleway.csproj src/Plugins/ServerManager.Plugin.Cloud.Scaleway/
COPY src/Plugins/ServerManager.Plugin.Cloud.Vultr/ServerManager.Plugin.Cloud.Vultr.csproj src/Plugins/ServerManager.Plugin.Cloud.Vultr/
COPY src/Plugins/ServerManager.Plugin.DevOps.Dokku/ServerManager.Plugin.DevOps.Dokku.csproj src/Plugins/ServerManager.Plugin.DevOps.Dokku/
COPY src/Plugins/ServerManager.Plugin.DevOps.Dokploy/ServerManager.Plugin.DevOps.Dokploy.csproj src/Plugins/ServerManager.Plugin.DevOps.Dokploy/
COPY src/Plugins/ServerManager.Plugin.Git.GitHub/ServerManager.Plugin.Git.GitHub.csproj src/Plugins/ServerManager.Plugin.Git.GitHub/
COPY src/Plugins/ServerManager.Plugin.Notifications.Discord/ServerManager.Plugin.Notifications.Discord.csproj src/Plugins/ServerManager.Plugin.Notifications.Discord/
COPY src/Plugins/ServerManager.Plugin.Notifications.Email/ServerManager.Plugin.Notifications.Email.csproj src/Plugins/ServerManager.Plugin.Notifications.Email/
COPY src/Plugins/ServerManager.Plugin.Notifications.Telegram/ServerManager.Plugin.Notifications.Telegram.csproj src/Plugins/ServerManager.Plugin.Notifications.Telegram/
COPY src/Plugins/ServerManager.Plugin.Storage.AzureBlob/ServerManager.Plugin.Storage.AzureBlob.csproj src/Plugins/ServerManager.Plugin.Storage.AzureBlob/
COPY src/Plugins/ServerManager.Plugin.Storage.S3/ServerManager.Plugin.Storage.S3.csproj src/Plugins/ServerManager.Plugin.Storage.S3/
RUN dotnet restore src/ServerManager.Web/ServerManager.Web.csproj

COPY src/ src/

# Web projesinin derlemesi eklentileri de derler (ProjectReference, ReferenceOutputAssembly=false);
# eklentiler src/ServerManager.Web/Plugins/{SystemName}/ altına yazılır ve PublishPlugins hedefi
# bunları yayın klasöründeki Plugins/ altına kopyalar.
RUN dotnet publish src/ServerManager.Web/ServerManager.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:UseAppHost=false \
    && test -n "$(ls -A /app/publish/Plugins)" \
    || (echo "Yayın çıktısında Plugins/ klasörü boş" && exit 1)

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# tzdata: Backup:TimeZone (Europe/Istanbul) gibi IANA saat dilimleri için; curl: HEALTHCHECK için.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

# Yazılabilir klasörler: yerel yedekler (App_Data/backups) ve Serilog dosya günlükleri (logs/).
RUN mkdir -p /app/App_Data /app/logs \
    && chown -R "$APP_UID":"$APP_UID" /app/App_Data /app/logs

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Europe/Istanbul

# Resmi imajlardaki ayrıcalıksız "app" kullanıcısı (UID 1654).
USER $APP_UID

EXPOSE 8080
VOLUME ["/app/App_Data", "/app/logs"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ServerManager.Web.dll"]
