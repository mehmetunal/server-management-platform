using ServerManager.Web.Models;

namespace ServerManager.Web.Helpers;

public static class ServerPickerSections
{
    public static readonly ServerPickerSection Docker = new(
        "docker", "Docker", "Docker yönetmek istediğiniz sunucuyu seçin; container'lar, imajlar, volume'lar ve ağlar sunucu sayfasında açılır.",
        "cube", "Docker", "Index", "Docker'ı aç");

    public static readonly ServerPickerSection Images = new(
        "images", "İmajlar", "İmajlarını görmek, çekmek veya temizlemek istediğiniz sunucuyu seçin.",
        "photo", "Docker", "Images", "İmajları aç");

    public static readonly ServerPickerSection Volumes = new(
        "volumes", "Volume'lar", "Docker volume'larını yönetmek istediğiniz sunucuyu seçin.",
        "database", "Docker", "Volumes", "Volume'ları aç");

    public static readonly ServerPickerSection Networks = new(
        "networks", "Ağlar", "Docker ağlarını yönetmek istediğiniz sunucuyu seçin.",
        "globe", "Docker", "Networks", "Ağları aç");

    public static readonly ServerPickerSection Terminal = new(
        "terminal", "Terminal", "SSH terminali açmak istediğiniz sunucuyu seçin; çalıştırılan komutlar kayıt altına alınır.",
        "terminal", "Terminal", "Index", "Terminal aç");

    public static readonly ServerPickerSection Files = new(
        "files", "Dosyalar", "Dosyalarına SFTP ile göz atmak istediğiniz sunucuyu seçin.",
        "folder", "Files", "Index", "Dosyaları aç");

    public static readonly ServerPickerSection Services = new(
        "services", "Sistem Servisleri", "Sistem servislerini (systemd / OpenRC) görmek istediğiniz sunucuyu seçin.",
        "cog", "ServerSystem", "Services", "Sistem servislerini aç");

    public static readonly ServerPickerSection Processes = new(
        "processes", "Process'ler", "Çalışan process'lerini incelemek istediğiniz sunucuyu seçin.",
        "list", "ServerSystem", "Processes", "Process'leri aç");

    public static readonly ServerPickerSection Logs = new(
        "logs", "Loglar", "journald veya /var/log kayıtlarını okumak istediğiniz sunucuyu seçin.",
        "document", "ServerSystem", "Logs", "Logları aç");

    public static readonly ServerPickerSection Metrics = new(
        "metrics", "Metrikler", "CPU, RAM, disk ve ağ grafiklerini görmek istediğiniz sunucuyu seçin. Listede son ölçülen değerler görünür.",
        "chart", "Servers", "Metrics", "Grafikleri aç");
}
