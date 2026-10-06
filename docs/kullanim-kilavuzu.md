# Mag Server Manager — Kullanma kılavuzu

Bu kılavuz, hesabı açık bir operatör veya yöneticinin panelde işi nasıl yapacağını anlatır. Kurulum, veritabanı ve yapılandırma anahtarları [teknik dokümanda](teknik-dokuman.md) durur. Ekranların sade anlatımı [son kullanıcı kılavuzundadır](son-kullanici.md).

Kılavuz panelin içinde de açılır: soldaki menünün en altındaki **Yardım → Kullanım Kılavuzu**. Sayfa başlıklarındaki **?** simgesi kılavuzun o sayfayla ilgili bölümünü yeni sekmede açar.

Panel adresi kurulumunuza göredir. Yerel geliştirmede `http://localhost:5180` veya `https://localhost:7180` kullanılır.

## 1. Oturum ve hesap güvenliği {#oturum}

1. E-posta ve parolayla giriş yapın.
2. İki adımlı doğrulama açıksa authenticator kodunu girin. Telefonunuz yanınızda değilse **Kurtarma kodu kullan**.
3. Hatalı denemelerde önce bekleme başlar: aynı hesap ve IP için üç denemeden sonra her yeni hatada bekleme süresi ikiye katlanır (en çok 5 dakika). 10 hatalı denemede hesap 15 dakika kilitlenir.
4. Oturum, işlem yokken varsayılan olarak 60 dakika sonra düşer. Arka planda yenilenen listeler oturumu uzatmaz.

Pasif hesapla giriş yapılamaz: "Hesabınız pasif durumda. Yöneticinizle iletişime geçin."

### Parola değiştirme {#parola-degistirme}

Sağ üstteki hesap menüsünden **Hesabım ve güvenlik** sayfasını açın. **Parola değiştir** bölümünde mevcut parolayı ve iki kez yeni parolayı yazın. Yeni parola en az 10 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir. Kaydedince diğer cihazlardaki oturumlar kapanır.

### İki adımlı doğrulama (2FA) {#iki-adimli-dogrulama}

Nasıl açılır:

1. **Hesabım ve güvenlik → İki adımlı doğrulama → Kurulumu başlat**.
2. Authenticator uygulamasında (Google Authenticator, Microsoft Authenticator, 1Password …) yeni hesap ekleyip QR kodu okutun. Okutamıyorsanız anahtarı elle girin (zaman tabanlı, 6 hane). Uygulamada hesap adı "Mag Server Manager" görünür.
3. Uygulamanın gösterdiği kodu **Doğrulama kodu** alanına yazıp **Doğrula ve aç**.
4. Ekranda 10 **kurtarma kodu** bir kez gösterilir. Güvenli bir yere kaydedin; her kod bir kez kullanılır.

Kod kabul edilmiyorsa telefonun saatinin doğru olduğundan emin olun. **Kalan kurtarma kodu** üç veya daha aza inince uyarı rengine döner; **Kurtarma kodlarını yenile** (parola ister) eski kodları geçersiz kılar. 2FA'yı **Kapat** düğmesiyle kapatırsınız (parola ister). Girişte "Bu tarayıcıda 30 gün boyunca sorma" işaretlendiyse **Bu tarayıcıyı unut** bu hatırlamayı kaldırır.

Yönetici `TwoFactor:Required` ayarını açtıysa 2FA'sı olmayan kullanıcı her girişte Hesabım sayfasına yönlendirilir ve kurmadan panele geçemez. Kodunu kaybeden kullanıcının 2FA'sını sistem yöneticisi **Kullanıcılar** sayfasından sıfırlar.

## 2. Sunucular {#sunucular}

### İlk sunucuyu ekleme {#sunucu-ekleme}

1. **Sunucular → Sunucu ekle**.
2. Ad, hostname, IP adresi ve ortamı girin. İşletim sistemi ilk başarılı testte kendiliğinden dolar.
3. **SSH bağlantısı:** kullanıcı adı, port (çoğu sunucuda 22) ve kimlik doğrulama türü. Kimlik parola veya private key olabilir; key passphrase istiyorsa onu da yazın.
4. SSH kullanıcısı `docker` grubunda veya root değilse **Sudo kullanıcı** kutusunu işaretleyin. Sudo parola istiyorsa **Sudo parolası** alanını doldurun (NOPASSWD ise boş bırakın). Dosya yedeği, servis kurulumu ve birçok sistem işi bunu ister.
5. Provider, lokasyon, grup, etiket ve aylık maliyeti (USD, EUR, TRY, GBP) isteğe bağlı doldurun.
6. Kaydedin, sunucuyu açın, **Bağlantıyı Test Et**.

Parola ve anahtarlar şifreli saklanır. İlk başarılı test, sunucunun host key parmak izini (SHA256) kaydeder. Sonraki bağlantılarda anahtar değişirse panel bağlanmaz: "Host key fingerprint kayıtlı değerle uyuşmuyor…". Parmak izi yalnızca IP, hostname veya SSH portu değişince sıfırlanır; testi yeniden yapın.

**Otomatik izleme** açıksa, parmak izi kaydından sonra ölçümler kendiliğinden gelir. Kapalıysa yalnızca **Şimdi topla** çalışır.

Sunucuyu silmek için adını birebir yazmanız istenir. Kayıt gizlenir; denetim geçmişi ve eski yedekler durur.

### Gruplar ve maliyet {#gruplar-ve-maliyet}

**Gruplar** sayfasında grup adı, renk ve açıklama verip sunucuları işaretleyin. Grubu silmek sunucuları silmez. **Maliyet** sayfası aylık giderleri para birimine göre toplar; dövizi çevirmez. Buluttan eşitlenen sunucuların fiyatı eşitlemede güncellenir (vergisiz).

## 3. Sunucu sayfası {#sunucu-sayfasi}

Üstteki sekmeler yetkinize göre görünür:

| Sekme | İş |
| --- | --- |
| Overview | Kimlik, son bağlantı, son ölçüm |
| Metrics | İşlemci, bellek, disk, ağ grafikleri; agent paneli |
| Docker | Container, imaj, volume, ağ |
| Terminal | Tarayıcıdan SSH kabuğu |
| Files | SFTP dosya yöneticisi |
| Servisler | Bu sunucuya tek tıkla kurulan veritabanı ve uygulama servisleri |
| Deployments | Bu sunucuya giden dağıtımlar |
| Security | Güvenlik taraması |
| Servisler | Bu sunucuya tek tıkla kurulan veritabanı ve uygulama servisleri |
| Backups, Alerts | Bu sunucunun yedek işleri ve alarmları |
| Sistem Servisleri, Process'ler, Loglar | systemd/OpenRC, süreç listesi, journal ve `/var/log` |
| Kaynak Kullanımı | "Neden yavaş?" bulguları, en çok kaynak kullananlar, en büyük klasörler |
| Temizlik | Kullanılmayan Docker kaynakları, önbellek, eski log ve geçici dosyalar |
| Activity | Bu sunucuyla ilgili denetim kayıtları |
| Dokploy, Dokku | Eklenti açıksa kurulum ve durum |

Soldaki Docker, Terminal, Dosyalar, Sistem Servisleri, Process'ler, Loglar, Metrikler, Kaynak Kullanımı ve Temizlik önce sunucu seçtirir, sonra aynı sekmeye gider.

## 4. İzleme {#izleme}

**Metrics** sekmesinde 1 saat ile 30 gün arası seçin. Kısa aralık ham ölçümden, 7 ve 30 gün saatlik özettendir. Ham ölçümler varsayılan olarak 48 saat, saatlik özet 90 gün saklanır (**Ayarlar → İzleme**).

Durum: eşik aşılmazsa Sağlıklı, uyarı eşiğinde Uyarı, kritik eşikte Kritik. Üst üste bağlantı koparsa Çevrimdışı. Bakımda görünen sunucunun durumu otomatik değişmez ve alarm açmaz.

### Agent kurulumu {#agent-kurulumu}

Panelin SSH ile düzenli ölçüm alamadığı sunucularda (ör. NAT arkası) agent kullanılır. Agent her 60 saniyede panele rapor yollar; agent etkinken aynı sunucu SSH ile ayrıca ölçülmez.

1. Sunucunun **Metrics** sekmesinde agent panelini bulun (sunucu düzenleme yetkisi gerekir).
2. **Agent kur → Oluştur**. "Agent kurulum komutu" penceresi açılır.
3. Komutu kopyalayıp sunucuda root yetkisiyle çalıştırın. Biçimi: `curl -fsSL <panel>/api/agent/install.sh | sudo SM_URL=<panel> SM_TOKEN=<token> sh`. Sunucuda `curl` ve systemd veya cron olmalıdır.
4. Durum birkaç dakika içinde "İlk rapor bekleniyor"dan "Aktif"e geçer.

Token yalnızca o pencerede gösterilir ve panelde saklanmaz; kaybederseniz **Yeni token** üretin (eski token hemen geçersiz olur, komutu yeniden çalıştırın). **Token'ı iptal et** raporları reddeder; dosyalar sunucuda kalır, panelde gösterilen **Sunucudan kaldırma** komutuyla (`… | sudo sh -s uninstall`) silinir. Sunucuda otomatik izleme kapalıysa agent raporları kabul edilmez. Panel HTTPS ile yayınlanmıyorsa ekranda uyarı çıkar.

### Uptime ve SSL {#uptime-ve-ssl}

**Uptime** bir adresin veya TCP portunun açık olduğunu düzenli kontrol eder.

1. **Uptime → Kontrol ekle**. Tür: **HTTP(S)** (GET, en çok 5 yönlendirme; geçersiz sertifika başarısız sayılır; kabul edilen durum kodları ör. `200-399`) veya **TCP port**.
2. **Kontrol aralığı** 10–86400 saniye; **zaman aşımı** 1–60 saniye ve aralıktan kısa olmalı.
3. **Şimdi kontrol et** sonucu hemen gösterir. Durumlar: Erişilebilir, Erişilemiyor, Bekleniyor.

**SSL** sayfası sertifika bitişine kalan günü izler: **Alan adı ekle**, alan adı ve port girin. Durumlar: Geçerli, Süresi yaklaşıyor, Süresi dolmuş, Geçersiz, Okunamadı. İkisi de alarm kuralına bağlanabilir.

## 5. Alarmlar {#alarmlar}

Üç sekme: **Alarmlar**, **Kurallar**, **Bildirim kanalları** (kanallar alarm yönetimi yetkisi ister).

- Alarm sorun sürdükçe **Aktif** kalır ve düzelince kendiliğinden **Kapandı** olur; elle kapatma yoktur. Zil simgesi açık alarmları gösterir.
- **Üstlen**, "buna bakıyorum" demektir; alarm kapanana kadar hatırlatmalar durur.

### Kural ekleme

1. **Kurallar → Kural ekle**. Ad, tür ve önem (Uyarı/Kritik) seçin.
2. Türler: CPU, RAM, disk kullanımı; sunucu erişilemiyor; uptime kontrolü başarısız; SSL sertifikası bitiyor; deployment başarısız; yedekleme başarısız; kritik güvenlik bulgusu; servis çalışmıyor; container yeniden başlama döngüsü; temizlenebilir alan.
3. Doluluk türlerinde **Eşik (%)** ve **Süre (dakika)** (0 = hemen) girin. SSL'de **Kalan gün eşiği** girilir; hatırlatma 30/15/7/3/1 gün kala gelir. Tür değiştirince yeni kuralda önerilen eşik ve süre kendiliğinden yazılır.
4. **Kapsam:** tüm sunucular veya tek sunucu. **Kanallar** seçin; isterseniz **Hatırlatma aralığı** ve **Düzelince bildir**.

### Servis ve container kuralları {#servis-alarmlari}

| Tür | Ne zaman açılır | Önerilen değer |
| --- | --- | --- |
| Servis çalışmıyor | Yönetilen servisin container'ı durmuş, sürekli yeniden başlıyor veya healthcheck'i "unhealthy". **Süre** kadar kesintisiz sürmelidir. | Süre 5 dk |
| Container yeniden başlama döngüsü | Container, **Süre** (pencere) içinde en az **Yeniden başlama sayısı** kadar yeniden başladı (Docker'ın RestartCount değeri). | 30 dk içinde 3 |
| Temizlenebilir alan | Otomatik temizlik taramasında silinebilir öğelerin toplamı **eşiği (GB)** aştı. Tarama hiçbir şey silmez; temizliği **Temizlik** sekmesinden siz yaparsınız. | 10 GB |

- Servis türlerinde **Servis** listesinden tek bir servis seçebilirsiniz; boş bırakılırsa kapsamdaki tüm servisler (yeniden başlama döngüsünde sunucudaki tüm container'lar) değerlendirilir.
- Servis kuralları **kaynak geçmişi** toplayıcısının container örneklerini kullanır (varsayılan 5 dakikada bir). Toplama kapalıysa veya sunucuya erişilemiyorsa durum "bilinmiyor" sayılır ve açık alarm kapanmaz.
- Servis kurulurken, güncellenirken veya sunucu bakım modundayken alarm açılmaz.
- Disk doluluğu için ayrı tür vardır: **Disk kullanımı** (en dolu bölümün yüzdesi).
- Servis sayfasının **Genel** sekmesinde servise ait açık alarmlar listelenir; **Kural ekle** bu servis için "Servis çalışmıyor" kuralını hazır açar.

### Bildirim kanalı

1. **Bildirim kanalları** sekmesinde türün kartına tıklayın:
   - **E-posta (SMTP):** SMTP sunucusu, port (587), bağlantı güvenliği (STARTTLS, SSL/TLS, şifrelemesiz), gönderen adresi, alıcılar (virgülle, en çok 10).
   - **Telegram:** bot anahtarı (@BotFather) ve sohbet kimliği (grup için `-100…` veya `@kanaladi`; bot sohbete eklenmiş olmalı).
   - **Discord:** webhook adresi.
2. **En düşük önem** ile yalnızca kritik alarmları da yollatabilirsiniz.
3. **Test** düğmesi deneme mesajı gönderir.

Kanal sırrı kayıttan sonra ekranda görünmez. Boş bırakıp kaydetmek eski sırrı korur.

## 6. Docker {#docker}

Sunucuda Docker kurulu olmalı ve SSH kullanıcısı ya `docker` grubunda olmalı ya da sudo işaretli olmalıdır. Aksi hâlde panelde "Docker bilgisi alınamadı" ve nedeni görünür (bkz. [Sorun giderme](#sorun-giderme)).

Yapabilecekleriniz, izninize göre:

- Container başlat, durdur, yeniden başlat, duraklat, sonlandır (kill), yeniden adlandır, sil
- Log ara, canlı izle, indir
- Container içine terminal aç
- İmaj çek, sil, kullanılmayanları temizle
- Volume ve ağ oluştur veya sil

Container ve volume silme, ad yazılarak onaylanır; volume silinince içindeki veri geri gelmez. Container silmek bağlı named volume'ları silmez. **Image'lar → Temizle** etiketsiz imajları siler; "Kullanılmayan tüm image'ları sil" işaretlenirse hiçbir container'ın kullanmadığı tüm imajlar gider. Onay için sunucu adını yazarsınız. Yeni bir Docker ağı, sunucunun kendi ağıyla veya panelin bağlandığı adresle çakışırsa reddedilir. Böylece SSH yolunu yanlışlıkla kesmezsiniz.

## 7. Terminal ve dosyalar {#terminal-ve-dosyalar}

### Terminal {#terminal}

1. **Terminal** menüsünden sunucuyu seçin; **+ (Yeni oturum)** sekme açar. Kullanıcı başına en çok 5 oturum açılır.
2. Sayfa yenilense veya bağlantı kopsa da oturum sunucuda açık kalır, yeniden bağlanılır. Oturum başka bir pencerede açıldıysa **Buraya al**.
3. Araç çubuğu: çıktıda arama (Enter/Shift+Enter), kopyala (Ctrl+Shift+C), yapıştır (Ctrl+Shift+V), **Çıktıyı indir**, **Komut geçmişi** (tıklanan komut yazılır, Enter'a basmadan çalışmaz), **Tam ekran**.
4. 30 dakika işlem yapılmayan oturum kapanır.

`rm -r`, `mkfs`, `dd`, kapatma/yeniden başlatma, güvenlik duvarı (`iptables`, `ufw`, `nft`), `userdel` gibi komutlar sunucuya gitmeden ikinci onay ("Tehlikeli komut") bekler. Onay gelmezse komut iptal olur. Yönetici bu davranışı `Terminal:DangerousCommandMode` ile uyarıya çevirebilir, kapatabilir veya tamamen engelleyebilir. Bu denetim yalnızca terminal içindir; **Toplu komut** için geçerli değildir.

Oturum açılışı, kapanışı ve yazılan komutlar IP ve kullanıcıyla kayda geçer; ekran çıktısı saklanmaz, parola istemlerine yazılanlar kaydedilmez. Kayıtlar **Ayarlar → Kayıt saklama → Terminal oturumları** süresince tutulur.

### Dosya yöneticisi {#dosya-yoneticisi}

**Dosyalar** klasörlerde gezinir. Araç çubuğunda üst klasör, ev klasörü, yol yazıp **Git**, gizli dosyalar, **Yeni klasör**, **Yeni dosya** ve **Yükle** vardır (dosyaları sayfaya sürükleyip bırakabilirsiniz; klasör yüklenmez, dosya başına en çok 100 MB).

Satır işlemleri: Düzenle, İndir, Yeniden adlandır / taşı, Kopyala, İzinler ve sahiplik (chmod/chown, isteğe bağlı alt klasörlere de), Sil. Klasör silerken adını yazarsınız. `/etc`, `/usr`, `/var` gibi sistem yolları korunur; panelden silinemez, taşınamaz.

Metin dosyası editörde açılır (en çok 1 MB, UTF-8): **Kaydet** (Ctrl+S), **Ara** (Ctrl+F), **Sunucudan yeniden yükle**. Dosya siz düzenlerken değiştiyse panel uyarır. Daha büyük dosyaları indirerek düzenleyin. Her işlem ayrı yetki ister (görüntüleme, oluşturma, düzenleme, silme, izin, yükleme, indirme).

## 8. Sistem servisleri, process'ler ve loglar {#sistem-servisleri}

- **Sistem Servisleri:** systemd veya OpenRC servislerini listeler; ara, duruma göre süz, başlat, yeniden başlat, durdur. Durdurmada servis adını yazarsınız. `sshd` veya ağ servisini durdurmak sunucuya erişimi kesebilir.
- **Process'ler:** komut, kullanıcı veya PID ile arayın; **Sonlandır** SIGTERM yollar, "Zorla sonlandır (SIGKILL)" işaretlenebilir.
- **Loglar:** journald (isteğe bağlı servis adı ve seviye) veya `/var/log` altındaki bir dosya; son 100–2000 satır. **Satırlarda ara** ekranda süzer.

Başlatma, durdurma ve sonlandırma sistem yönetimi yetkisi ister.

## 9. Temizlik {#temizlik}

Disk dolduğunda sunucu sayfasındaki **Temizlik** sekmesini açın (soldaki menüden de sunucu seçerek gelinir). Silinebilecekler gruplanır ve tahmini boyutla gösterilir:

- **Docker:** durmuş container'lar, kullanılmayan ve sahipsiz (dangling) imajlar, kullanılmayan ağlar ve volume'lar, build önbelleği. Panelin kendi ağları (`sm-proxy`, `sm-services`) ve Docker'ın varsayılan ağları hiç listelenmez.
- **Sistem:** paket önbelleği (apt / dnf / yum), journal (seçtiğiniz boyuta küçültülür), `/var/log` içindeki eski döndürülmüş loglar, `/tmp` içindeki eski dosyalar, kullanılmayan snap sürümleri. Eski çekirdekler yalnızca bilgi için gösterilir, silinmez.

**Güvenle silinebilir** öğeler önceden seçilir. **Dikkat** öğeleri (panel projesine veya servisine ait container'lar, geri dönüş imajları, servis verisi ve diğer adlandırılmış volume'lar, `/tmp` dosyaları) gerekçesiyle gösterilir ve yalnızca siz seçerseniz silinir; seçtiyseniz onay için `temizle` yazmanız istenir.

Nasıl yapılır:

1. Gün sayılarını ve journal hedef boyutunu seçip **Tara**'ya basın.
2. **Önizle** hiçbir şey silmeden neyin silineceğini gösterir.
3. **Seçilenleri temizle** ile çalıştırın. Panel sunucuyu yeniden tarar ve yalnızca hâlâ var olan öğelere dokunur. Günlük canlı akar; sonunda kazanılan alan yazılır ve işlem denetim kaydına geçer.

Silinen volume'lardaki veri geri gelmez; emin değilseniz önce yedek alın. Sunucu temizliği yetkisi (`server.cleanup`, Admin ve Operator) gerekir.

## 10. Kaynak kullanımı {#kaynak-kullanimi}

Sunucu yavaşladığında **Kaynak Kullanımı** sekmesini açın:

- **Neden yavaş?** kutusu yükü çekirdek sayısıyla karşılaştırır; düşük boş bellek, swap kullanımı, disk bekleme (iowait), %90 üstü disk veya inode doluluğu, işlemciyi tek başına tüketen bir process, son 24 saatteki bellek yetersizliği (OOM) kayıtları ve yoğun container'lar için bulgu üretir ve ilgili bölüme veya **Temizlik** sekmesine bağlantı verir.
- Yük, işlemci dağılımı, bellek ve swap özeti.
- En çok CPU ve RAM kullanan 10 process. Sistem yönetimi yetkiniz varsa buradan sonlandırabilirsiniz. Process'in swap kullanımı ve (sunucuda `pidstat` varsa) disk G/Ç'si de görünür. Buradaki CPU yüzdesi anlık değil, process'in ömrü boyunca ortalamasıdır.
- Container'ların CPU, bellek, ağ ve disk kullanımı (Docker görüntüleme yetkisiyle). Satır, container sayfasına ve ait olduğu panel projesine veya servise bağlanır.
- Dosya sistemleri ve inode doluluğu. **En büyük klasörler ve dosyalar** için bir klasör yazıp **Tara**'ya basın; tarama düşük öncelikte en fazla bir dakika sürer.

**Otomatik yenile** açıkken sayfa kendini düzenli günceller. Sayfa sistem bilgisi görüntüleme yetkisi ister.

### Geçmiş: dün gece ne yavaşlattı? {#kaynak-gecmisi}

Panel, izlenen sunuculardan varsayılan olarak 5 dakikada bir container kullanımını (CPU, bellek, ağ, disk G/Ç, durum, yeniden başlama sayısı) ve en çok CPU ile bellek kullanan process'leri kaydeder. Sayfanın **Geçmiş** bölümünde:

1. Aralığı seçin: **1 saat**, **6 saat**, **24 saat**, **7 gün** veya **Özel** (başlangıç ve bitiş girip **Göster**).
2. Grafikler, aralıkta en çok CPU ve en çok bellek kullanan beş container'ı çizer. 48 saatten uzun aralıklarda saatlik ortalamalar kullanılır.
3. Grafikte bir noktaya tıklayın: o ana en yakın **process anlık görüntüsü** (PID, kullanıcı, komut, CPU, RAM) açılır.
4. **Bu aralıkta en çok kaynak kullananlar** tablolarında container'ların ortalama ve en yüksek CPU/RAM'i, ağ ve disk G/Ç artışı, yeniden başlama sayısı; process'lerin CPU'ya ve belleğe göre sıralaması görünür.

Buradaki process CPU'su anlık ölçümdür (iki okuma arasındaki gerçek kullanım; çok iş parçacıklı process'lerde %100'ü aşabilir). Toplama aralığı ve saklama süreleri **Ayarlar**'dadır (İzleme → Kaynak geçmişi aralığı, 0 kapatır; Kayıt saklama → Container örnekleri, saatlik özetler, process anlık görüntüleri). Toplama, ana metrik ölçümünden ayrı çalışır ve onu yavaşlatmaz.

## 11. Yedekleme {#yedekleme}

Üç sekme: **Yedekleme işleri**, **Geçmiş**, **Depolama** (depolama yedek yönetimi yetkisi ister).

### Depolama {#yedek-depolama}

1. **Depolama** sekmesinde hedef türünün kartına tıklayın, bilgileri girin.
2. Kaydettikten sonra **Test** ile hedefin yazılabilir olduğunu doğrulayın (küçük bir dosya yazılır, okunur, silinir).

| Hedef | Ne zaman |
| --- | --- |
| Yerel disk | Yedek dosyası panelin kendi diskinde kalsın (`Backup:LocalRootPath` altında) |
| S3 uyumlu | Amazon S3, Cloudflare R2 (bölge `auto`), MinIO (adres biçimi: yol biçimi), Backblaze B2 |
| Azure Blob | Azure veya Azurite. Azurite adresi `http://127.0.0.1:10000/devstoreaccount1` biçimindedir |

Anahtar ve hesap parolası bir daha gösterilmez. İşlerin kullandığı depolama silinemez.

### Yedekleme işi {#yedek-isi}

1. **Yeni iş**. Ad, sunucu ve depolama hedefi seçin.
2. **Ne yedeklenecek?**
   - **Dosya / klasör:** satır başına bir mutlak yol (`/`, `/proc`, `/sys`, `/dev` olmaz). Hariç tutmak için `*.log` gibi desen yazabilirsiniz. Okuma için çoğu zaman sudo gerekir.
   - **Docker volume:** volume adı. Çalışan uygulamayı durdurmaz; sunucunun Docker Hub'dan `alpine:3` imajını çekebilmesi gerekir. İçinde veritabanı dosyası varsa tutarlı kopya için veritabanı türünü kullanın.
   - **Veritabanı:** aşağıya bakın.
3. **Şifreleme:** "Yedeği şifrele (AES-256-GCM)" varsayılan açıktır; en az 12 karakterlik parola girin. Panel veritabanı kaybolursa yedekler yalnızca bu parola ve `tools/backup-decrypt.py` ile açılır; parolayı ayrıca saklayın.
4. **Zamanlama:** yalnızca elle, saatlik (1–168 saatte bir), günlük veya haftalık (gün ve saat). Saatler **Ayarlar → Yedekleme → Saat dilimi**ne göredir.
5. **Saklama:** saklanacak yedek sayısı (varsayılan 7) ve isteğe bağlı "son kaç günü sakla". Bir dosya ancak en yeni N kopyanın dışında **ve** (gün verildiyse) o günden eskiyse silinir. En yeni başarılı kopya hiç silinmez; geçmiş kaydı kalır.

**Şimdi yedek al** ilerlemeyi işin sayfasında gösterir. Aynı iş için ikinci işlem, ilki bitene kadar başlamaz. Kapalı (etkin olmayan) iş zamanlamayla çalışmaz ama elle yedek alınabilir.

### Veritabanı yedekleri {#veritabani-yedekleri}

**Container** alanı doluysa araç container içinde (`docker exec`) çalışır; boşsa sunucuda kurulu istemci kullanılır. Parola komut satırına yazılmaz.

| Motor | Yedek | Geri yükleme | Not |
| --- | --- | --- | --- |
| PostgreSQL | `pg_dump` (`.sql.gz`) | `psql`; yedekteki tablolar silinip yeniden kurulur, diğer tablolar kalır | Kullanıcı ve veritabanı adı zorunlu |
| MySQL / MariaDB | `mariadb-dump` / `mysqldump` (`.sql.gz`) | `mariadb` / `mysql` | Kullanıcı ve veritabanı adı zorunlu |
| MongoDB | `mongodump --archive` (`.archive.gz`) | `mongorestore`; "Önce sil (--drop)" varsayılan açık | Ad boşsa tüm veritabanları; MongoDB Database Tools 100.3+ |
| Redis | `BGSAVE` sonrası RDB dosyası (`.rdb.gz`) | Panelden yapılmaz, elle (aşağıda) | Redis bu sunucuda çalışmalı, `CONFIG` açık olmalı |
| SQL Server | `BACKUP DATABASE … COPY_ONLY` (`.bak.gz`) | `RESTORE … WITH REPLACE` | Bu sunucuda çalışmalı; `/var/opt/mssql/backup` altında veritabanı boyutu kadar boş yer |

Redis ve SQL Server yedeği veritabanının kendi diskinden okunur; adres alanını boş bırakın veya `localhost` / `127.0.0.1` yazın.

#### Redis yedeğini elle geri yükleme {#redis-geri-yukleme}

1. Yedeği **Geçmiş** sayfasından indirin ve açın (`gunzip`).
2. Redis'i durdurun.
3. RDB dosyasını Redis'in veri klasörüne (`CONFIG GET dir`, ör. `/data`) `dbfilename` adıyla (ör. `dump.rdb`) kopyalayın.
4. AOF açıksa (`appendonly yes`) Redis RDB'yi değil AOF'u yükler: önce `appendonly no` ile başlatıp verinin geldiğini kontrol edin, sonra `CONFIG SET appendonly yes` ile AOF'u yeniden oluşturun.
5. Redis'i başlatın.

### Yedeği indirme {#yedek-indirme}

Yedek dosyasını, yedeğin göründüğü her yerdeki **İndir** düğmesiyle alırsınız: Geçmiş, işin son yedekleri, yedek ayrıntısı, geri yükleme sayfası, sunucunun Backups sekmesi, İşler listesi ve Servisler'deki **Yedekleme** kartı ("Son yedek"). Dosya, volume ve veritabanı yedeklerinde aynıdır. Düğme yalnızca başarılı ve dosyası silinmemiş yedekte çalışır; pasifse üzerine gelince nedeni yazar.

Şifreli yedek varsayılan olarak `.smbk` dosyası olarak iner. Çözülmüş (`.tar.gz`, `.sql.gz` …) dosya isterseniz yedek ayrıntısında **Parolayla çözerek indir** bölümüne işin şifreleme parolasını girin; parola yanlışsa indirme başlamadan uyarı çıkar. Panel olmadan çözmek için aynı bölümde gösterilen `tools/backup-decrypt.py` komutunu kullanın.

İndirme yedek dosyası indirme yetkisi (`backup.download`, Admin ve Operator'da varsayılan açık) ister ve her deneme denetim kaydına yazılır. "Bu yedeğin dosyası yok" yazıyorsa yedek başarısızdır, silinmiştir veya saklama kuralıyla temizlenmiştir. İş silinmiş olsa da geçmişteki bağlantı açılır; yeni yedek ve düzenleme kapalıdır.

### Geri yükleme {#geri-yukleme}

1. Çalıştırma sayfasında **Geri yükle**.
2. Hedef sunucuyu ve türüne göre açılacak klasörü (varsayılan `/`), hedef volume'u veya container / hedef veritabanını seçin.
3. "Hedefteki verinin üzerine yazılacağını anladım." kutusunu işaretleyip başlatın.

Şifreli yedek panelde çözülür ve SHA-256 özeti doğrulanır. Geri yükleme yetkisi ayrıdır.

## 12. Projeler ve deployment {#projeler}

### Proje ekleme

1. **Projeler → Proje ekle**. Ad, hedef sunucu ve açıklama girin. Docker adları `sm-<ad>` olur ve sonradan değişmez.
2. **Git deposu:** sağlayıcı (GitHub, GitLab, Bitbucket, diğer), depo adresi, dal. Özel depo için kullanıcı adı ve erişim anahtarı (token) girin. GitHub App eklentisi açıksa depoyu anahtar yazmadan seçebilirsiniz.
3. **Build ve deploy:** sunucudaki klasör (yoksa oluşturulur; doluysa yalnızca git deposuysa kullanılır) ve build türü:
   - **Docker Compose:** `docker compose build` ve `up -d`. Compose dosyasını belirtin.
   - **Dockerfile:** imaj build edilir, tek container çalışır. Port eşlemesi `[ip:]sunucu:container[/tcp|udp]` biçimindedir.
   - **Komutlar:** build ve deploy komutu sunucuda çalışır (isteğe bağlı sudo). Bu türde container'ları panel yönetmez; ortam değişkeni uygulama, çalışma logları ve domain yoktur.
4. İsterseniz ilk `.env` içeriğini yapıştırın ve kaydedin.

Build komutu hedef sunucuda çalışır. Bu yetkiyi yalnızca sunucuya komut yazmasına güvendiğiniz role verin.

### Deploy etme

1. Proje sayfasında **Deployment başlat** kartına gidin. İsterseniz dalda bulunan bir commit SHA'sı yazın; boşsa dalın son hali alınır.
2. **Deploy et**. Adımlar (Hazırlık → Kaynak kod → Build → Deploy → Tamamlandı) ve canlı log deployment sayfasında görünür. **Depoyu kontrol et** erişimi önceden dener.
3. Süren deployment **İptal et** ile durdurulur; biten deployment **Yeniden deploy** ile tekrarlanır. **Log indir** kaydı verir.

### Ortam değişkenleri {#ortam-degiskenleri}

Proje sayfasının **Ortam değişkenleri** sekmesi:

1. **Değişken ekle** ile anahtar ve değer girin. Anahtar harf veya `_` ile başlar, harf, rakam ve `_` içerir. Değer tek satırdır ve boşlukla bitemez.
2. **İçe aktar** ile `.env` metni yapıştırın veya dosya seçin (`ANAHTAR=değer`, `#` yorum satırı). Mod: "Yalnızca yenileri ekle", "Var olanların değerini değiştir" veya "Tümünü değiştir" (listede olmayan anahtarları siler, onay ister).
3. Değerler maskeli görünür. **Göster** ve **.env indir** gizli değer yetkisi ister ve denetim kaydına yazılır.

Sınırlar: toplam 64 KB, en çok 500 değişken, değer başına 32 KB.

Değişiklik kaydedilir ama çalışan uygulamaya hemen yansımaz. Bir sonraki deploy'da ya da **Uygula / Yeniden başlat** ile sunucudaki proje klasörüne `.env` olarak yazılır. Compose projesinde `.env` tüm servislere `env_file` olarak verilir; bir servisin kendi `environment:` değerleri önceliklidir. Dockerfile projesinde container `--env-file` ile başlatılır.

**Bağlı servisler** listesi, **Servisler** modülünden bu projeye bağlanan veritabanlarını gösterir (bkz. [Projeye bağla](#projeye-bagla)).

### Uygula / Yeniden başlat {#uygula-yeniden-baslat}

**Uygula / Yeniden başlat** kodu çekmez ve build yapmaz: `.env`'i yazar, container'ları mevcut imajla yeniden oluşturur. Projenin en az bir başarılı deployment'ı olmalıdır. Komutlar türündeki projede bu düğme yoktur.

### Çalışma logları {#calisma-loglari}

**Çalışma logları** sekmesinde container seçin; son N satırı görün, arayın, "Yalnızca hata/uyarı" veya "Yalnızca stderr" ile süzün, **Canlı takip** ile akışı izleyin, **İndir** ile kaydedin. Docker görüntüleme yetkisi de gerekir.

### Push ile otomatik deploy (webhook) {#otomatik-deploy}

1. Proje sayfasının **Otomatik deploy** sekmesinde **Aç**.
2. Gösterilen webhook adresini (`…/api/webhooks/projects/<id>`) ve gizli anahtarı kopyalayın. Anahtar yalnızca bir kez gösterilir; kaybederseniz **Anahtarı yenile**.
3. **GitHub:** depo → *Settings → Webhooks → Add webhook*. Payload URL = webhook adresi, Content type = `application/json`, Secret = gizli anahtar, "Just the push event".
4. **GitLab:** proje → *Settings → Webhooks*. URL = webhook adresi, Secret token = gizli anahtar, tetikleyici "Push events".
5. Bir push gönderin; **Son teslimat** alanında sonucu görün.

Yalnızca projenin dalına gelen push deploy edilir; diğer dallar yok sayılır. Bir deployment sürerken gelen push kuyruğa alınır ve bitince dalın son hali deploy edilir. Kuyruk veritabanında tutulur: panel bu arada yeniden başlasa da bekleyen deploy kaybolmaz, açılışta (ve dakikada bir) yeniden denenir. Proje başına tek bekleyen deploy olur; art arda gelen push'lar onu günceller. GitHub'ın "ping" isteği "Ping alındı" olarak görünür. İmza tutmazsa istek 401 ile reddedilir. Panel ters vekil arkasındaysa ve Git sağlayıcısı panele başka bir adresle ulaşıyorsa yönetici `Deployment:PublicBaseUrl` değerini ayarlamalıdır.

### Önceki sürüme geri dönme {#geri-alma}

Deployment listesinde veya ayrıntısında **Bu sürüme geri dön**, seçilen başarılı deployment'ın commit'ini yeniden çalıştırır. Ortam değişkenleri ve domainler projenin güncel ayarından gelir. Dockerfile projesinde o commit'in imajı (`sm-<ad>:<kısa-sha>`) sunucuda duruyorsa build yapılmaz; aksi hâlde commit yeniden build edilir. Saklanan imaj sayısı `Deployment:KeepImageCount` (varsayılan 5) ile sınırlıdır.

### Domainler, Traefik ve SSL {#domainler}

1. İlk domainden önce proje sayfasındaki **Domainler** kartında **Vekil kur**. Bu, sunucuda `sm-traefik` adlı Traefik'i 80 ve 443'e alır. Port doluysa kurulum durur.
2. **Domain ekle:** host, isteğe bağlı yol, container portu (varsayılan 80), Compose projesinde servis adı ve sertifika türü:
   - **Cloudflare:** kilit Cloudflare'de kalır, sunucu yalnızca 80 dinler. Turuncu bulut (proxy) açıksa bunu seçin.
   - **Let's Encrypt:** domain'in A kaydı bu sunucuya doğrudan bakmalıdır.
   - **Özel sertifika:** PEM sertifika ve özel anahtarı yapıştırın.
3. DNS kaydını siz açarsınız. Yönlendirme ilk başarılı deployment'tan sonra uygulanır; **Yönlendir** yeniden uygular.

Proje başına en çok 20 domain eklenir. Komutlar türündeki projeye domain bağlanmaz. Domain silmek DNS kaydına dokunmaz.

### Projeyi silme

Proje silinirken adını yazarsınız. Kutu işaretlenmezse yalnızca panel kaydı kapanır. Kutu işaretlenirse sunucudaki klasör, container, imaj, volume ve domain yönlendirmesi de silinir; klasör yalnızca panel tarafından oluşturulmuşsa (`.sm-managed` işareti) silinir. Deployment geçmişi kalır.

## 13. Servisler {#servisler}

Menüde **Servisler** veya sunucu sayfasındaki **Servisler** sekmesi; tek tıkla PostgreSQL, MySQL, MariaDB, Redis, MongoDB, SQL Server (yalnızca x86_64), MinIO, RabbitMQ, Adminer, pgAdmin, Uptime Kuma ve n8n kurar. Her servis bir Docker container'ıdır (`sm-svc-<ad>`). (systemd servisleri **Sistem Servisleri** adıyla durur.)

### Servis kurma {#servis-kurulumu}

1. **Servis ekle** → şablonu seçin.
2. **Genel:** sunucu, servis adı, sürüm (listeden veya herhangi bir Docker Hub etiketi) ve veri saklama (Docker volume veya sunucu klasörü). Form sunucuda Docker'ın kurulu ve erişilebilir olduğunu baştan kontrol eder.
3. **Kimlik bilgileri:** parola otomatik üretilir (en az 12 karakter; boşluk, tırnak ve ters bölü olmaz). SQL Server parolası büyük harf, küçük harf, rakam ve sembolden en az üçünü içermelidir.
4. **Ağ ve portlar** (aşağıda), **Kaynak sınırları** (bellek MB, CPU çekirdek; boş = sınırsız), **Ek ortam değişkenleri**.
5. Veritabanı şablonlarında **Otomatik yedek** (aşağıda).
6. **Kur**. Adımlar (Docker → Port kontrolü → İmaj → Ağ ve volume → Container → Sağlık → Bağlantı testi) canlı görünür; sayfayı kapatsanız da kurulum sürer.

### Portlar, "Dışarıya aç" ve IP listesi {#servis-portlari}

Port yayınlanırsa varsayılan olarak yalnızca sunucudan (`127.0.0.1`) erişilir. Yayınlanmayan servise yalnızca iç ağdan (`sm-services`) ulaşılır. 1024 altındaki sunucu portları seçilemez.

**Dışarıya aç (0.0.0.0)** portu internete açar. Bu durumda **İzin verilen IP / ağlar** listesine satır başına bir IP veya CIDR yazın (en çok 50; `/0` kabul edilmez). Liste boşsa port herkese açıktır.

Docker'ın yayınladığı portlar UFW / firewalld kurallarını atlar. Panel kısıtlamayı `DOCKER-USER` zincirine iptables kurallarıyla yazar ve sunucu açılışında `sm-services-firewall` systemd birimiyle geri yükler. Kural uygulanamazsa port açık kalmasın diye container başlatılmaz. Kurallar kaybolursa Genel sekmesinde **Kuralları yeniden uygula** çıkar.

### Servis sayfası {#servis-sayfasi}

- **Genel:** durum, iç/dış adres, maskeli bağlantı adresi. **Parolayı göster** gizli değer yetkisi ister ve kayda geçer. **Bağlı projeler** ve **Yedekleme** kartları buradadır.
- **Loglar:** son N satır, arama, "Yalnızca hata/uyarı", canlı takip, indir.
- **İşlemler:** kurulum, yeniden oluşturma, yükseltme ve kaldırma geçmişi.
- **Ayarlar:** değişken, port, IP listesi, sınırlar. **Kaydet ve yeniden oluştur** container'ı veri korunarak yeniden kurar; kısa bir kesinti olur.
- **Sürüm:** **Yeni sürüm** seçip **Yükselt**. Önce yedek alın. Ana sürüm değişiyorsa "Ana sürüm değişiyor" onayı çıkar. MongoDB ana sürümleri tek tek yükseltilmelidir (6.0 → 7.0 → 8.0). İmaj çekilemezse mevcut container'a dokunulmaz.
- **Konsol:** psql, mysql, redis-cli, mongosh, sqlcmd … (konsol yetkisi; açılış ve komutlar kaydedilir).
- **Tehlikeli bölge:** **Servisi kaldır**, adı yazarak. Varsayılan olarak veri kalır; "Veriyi de sil" geri alınamaz. Sunucu klasörü yalnızca panel oluşturduysa silinir.

### Projeye bağla {#projeye-bagla}

1. Servisin **Genel** sekmesinde **Projeye bağla**.
2. Aynı sunucudaki Compose veya Dockerfile projesini seçin.
3. Eklenecek değişkenleri işaretleyin, gerekirse adlarını değiştirin (ör. `DATABASE_URL`).
4. Var olan anahtarların değiştirilip değiştirilmeyeceğini seçin. **Hemen uygula** projeyi build etmeden yeniden başlatır; işaretlemezseniz değişiklik sonraki deploy'da veya **Uygula / Yeniden başlat** ile gelir.

Proje container'ları `sm-services` ağına katılır ve servise container adıyla (`sm-svc-…`) bağlanır. **Bağı kaldır** değişkenleri silmez; kutu işaretlenirse yalnızca bağlarken eklenenleri siler. Projeye bağlamak için hem servis yönetimi hem proje düzenleme yetkisi gerekir.

### Otomatik yedek {#servis-yedekleme}

Veritabanı şablonlarında **Kurulum başarılı olunca otomatik yedekleme işi oluştur** kutusunu işaretleyip depolama hedefi, zamanlama (her gün belirli saatte veya birkaç saatte bir), saklanacak yedek sayısı ve isteğe bağlı şifreleme parolası seçin. İş, kurulum başarıyla bitince oluşur. Ayarlar (parola dahil, şifreli olarak) kurulum kaydında saklanır; panel kurulum sırasında yeniden başlarsa iş açılışta oluşturulur. Kurulum başarısız olursa iş oluşturulmaz ve ayarlar silinir. Sonradan eklemek için Genel sekmesindeki **Yedekleme** kartında **Yedek işi oluştur**. Yedek işi için yedek yönetimi yetkisi gerekir.

### Eklenti şablonları {#servis-eklenti-sablonlari}

Eklentiler **Servis ekle** sayfasına yeni servis türleri ekleyebilir. Örneğin **Ek Servis Şablonları** eklentisi Meilisearch, ClickHouse ve Keycloak getirir. Eklenti etkinken kartları kendi grubunda ve **Eklenti** etiketiyle görünür; kurulum, portlar, kimlik bilgileri ve loglar yerleşik servislerle aynıdır. Otomatik yedek yalnızca yerleşik veritabanı şablonlarında vardır.

Şablonu sağlayan eklenti devre dışı bırakılırsa ya da kaldırılırsa servis listede **Şablon eklentisi devre dışı** (veya **bulunamadı**) etiketiyle kalır. Container çalışmaya devam eder; loglar, başlat/durdur/yeniden başlat ve kaldırma kullanılabilir. **Ayarlar** ve **Sürüm** sekmeleri, eklenti yeniden etkinleştirilene kadar gizlenir. Hatalı veya çakışan şablonlar yüklenmez; nedeni **Eklentiler** sayfasında eklenti kartında uyarı olarak gösterilir. Kendi şablon eklentinizi yazmak için depodaki `docs/plugin-gelistirme.md` dosyasına bakın.

## 14. Toplu komut ve şablon {#toplu-komut}

**Toplu komut** seçilen sunucularda aynı betiği çalıştırır (`sh -c`, en çok 100 sunucu, aynı anda 5). Zaman aşımı 5–900 saniye. Onay penceresi komutu ve sunucu listesini gösterir. Beş veya daha fazla sunucuda sayıyı yazmanız istenir. Her sunucunun çıktısı ve çıkış kodu ayrı saklanır.

**sudo ile çalıştır** yalnızca sudo açık sunuculara gider. Diğerleri başarısız sayılır, yetkisiz çalıştırılmaz. Terminaldeki tehlikeli komut onayı burada yoktur; komutu dikkatle kontrol edin.

**Şablonlar** iki türlüdür:

- Kabuk betiği, toplu komut formuna kopyalanır. Çalıştırmadan önce düzenleyebilirsiniz.
- cloud-init, bulutta yeni makine açılırken ilk kurulum metni olarak gider. `#cloud-config` veya `#!` ile başlamalıdır.

Şablona parola veya API anahtarı yazmayın. Metin şifrelenmeden durur.

## 15. Bulut sağlayıcıları {#bulut}

**Sağlayıcılar** Hetzner Cloud, DigitalOcean, Vultr, Linode ve Scaleway hesaplarını tutar. Eklenti kapalıysa hesap listede kalır, eşitleme ve yeni makine çalışmaz.

1. Sağlayıcıda API anahtarı üretin (listelemek için okuma, sunucu açmak için yazma yetkisi). Panel anahtarı sağlayıcıda dener, sonra şifreli saklar.
2. Listeden **Panele ekle** sunucu formunu ad, IP ve fiyatla doldurur. SSH kullanıcı ve parola veya key hâlâ sizin girmeniz gerekir.
3. **Eşitle** bağlı sunucuların aylık fiyatını günceller.
4. **Sunucu oluştur** bölge, tip ve imaj seçtirir; isterseniz cloud-init şablonu ve SSH genel anahtarı ekleyin. Onayda sunucu adını yazarsınız. Hetzner, Vultr ve Linode, anahtar vermezseniz root parolasını yalnızca o pencerede bir kez gösterir. Bu parola panele yazılmaz. DigitalOcean parolayı e-postanıza gönderir. Linode adı harfle başlamalıdır; nokta kullanılamaz.

## 16. Güvenlik taraması {#guvenlik}

**Güvenlik** sayfası veya sunucunun Security sekmesi salt okunur komutlarla şunları denetler: SSH ayarları (root girişi, parola ile giriş …), riskli açık portlar, güvenlik duvarı (ufw, firewalld, nftables, iptables), Docker API ve yayınlanan portlar, bekleyen güncellemeler, kullanıcı hesapları ve disk şifreleme.

1. Önce sunucuda **Bağlantıyı Test Et** ile host key'i doğrulayın.
2. **Şimdi tara**. Sonuç 100 üzerinden puan, kritik ve uyarı sayısıyla gelir (her kritik −25, her uyarı −10).
3. Bulguların yanında önerilen düzeltme komutu vardır; kopyalayıp kendiniz çalıştırın. Panel hiçbir değişikliği kendiliğinden uygulamaz.

Root olmayan kullanıcıyla yapılan tarama "Kısıtlı" etiketi alır. Otomatik tarama aralığı **Ayarlar → Güvenlik taraması**ndadır (varsayılan 24 saat, 0 = kapalı).

## 17. Kullanıcılar, roller ve yetkiler {#kullanicilar}

**Kullanıcılar** sayfası yalnızca kullanıcı yönetimi yetkisi (`user.manage`) olan yöneticidedir.

1. **Kullanıcı ekle:** e-posta, ad soyad, bir veya daha fazla rol ve iki kez parola.
2. Düzenlemede rolleri işaretleyin, **Aktif** kutusu ve isteğe bağlı yeni parola vardır. Bir kullanıcının birden fazla rolü olabilir; izinleri rollerin birleşimidir. Pasif kullanıcının açık oturumları en geç 1 dakika içinde kapanır; rol değişikliğinde kullanıcının oturumu yenilenir ve açık terminalleri kapanır.
3. Satırdan **Kullanıcıyı kilitle / Kilidi aç** ve **İki adımlı doğrulamayı sıfırla**.

Kendi rolünüzü değiştiremez, kendinizi pasifleştiremez veya kilitleyemezsiniz. Sistemde en az bir aktif SuperAdmin kalmalıdır. SuperAdmin rolünü yalnızca bir SuperAdmin atayabilir. Bir değişiklik, kullanıcı veya rol yönetimi yetkisini son aktif sahibinden de alacaksa reddedilir.

Panelle gelen roller:

| Rol | Kısaca |
| --- | --- |
| SuperAdmin | Her şey; değiştirilemez, yeni eklenen her izni de otomatik alır |
| Admin | Kullanıcı ve eklenti yönetimi dışında her şey; rolleri yönetebilir |
| Operator | Docker, terminal, dosya, sistem servisleri, servis yönetimi, elle yedek alma, alarm üstlenme. Yedek geri yükleme, ayarlar, denetim kaydı yok |
| Developer | Görüntüleme, deployment başlatma, container yeniden başlatma, dosya indirme |
| Viewer | Yalnızca görüntüleme |

Eklentiler kurulunca kendi yetkilerini rollere ekler. Bir düğme görünmüyorsa rolünüzde o yetki yoktur.

### Roller {#roller}

**Yönetim → Roller** (veya Kullanıcılar sayfasındaki **Roller** düğmesi) rol yönetimi yetkisi (`roles.manage`) ister.

- Liste her rolün kullanıcı ve izin sayısını gösterir.
- **Rol ekle:** ad, açıklama ve izinler. İzinler modüllere göre gruplanır (Sunucular, Docker, Deployment, Yedekleme …); eklentilerin izinleri eklentinin adıyla ayrı grupta durur. **Tümü** düğmesi bir modüldeki izinleri birlikte seçer.
- **Kopyala** simgesi seçilen rolün izinleriyle yeni rol formunu açar.
- **Düzenle:** özel rolün adı, açıklaması ve izinleri değişir. Yerleşik rollerin (Admin, Operator, Developer, Viewer) adı değişmez; izinleri değişir ve **Varsayılana döndür** ile panelin varsayılanlarına geri alınır. SuperAdmin yalnızca görüntülenir.
- **Sil:** yalnızca özel roller. Rolde kullanıcı varsa düzenleme sayfasının altındaki **Rolü sil** bölümünde kullanıcıların taşınacağı rolü seçin.

Bir role yalnızca kendi hesabınızda olan izinleri verebilirsiniz (SuperAdmin hariç). Kendi rolünüzden kendi kullanıcı veya rol yönetimi yetkinizi kaldıran bir değişiklikte panel ayrıca onay ister.

Rolden izin kaldırıldığında o roldeki kullanıcıların oturumu en geç 1 dakika içinde yenilenir (yeniden giriş gerekir) ve açık terminalleri kapanır. Yalnızca izin eklendiyse kullanıcılar oturumlarını kapatmadan en geç 1 dakikada yeni izni alır.

Güncellemede panel yalnızca o sürümde gelen YENİ varsayılan izinleri rollere ekler. Bir yerleşik rolden bilerek kaldırdığınız izin sonraki açılışlarda geri eklenmez.

## 18. API anahtarları {#api-anahtarlari}

Betikler ve CI araçları paneli REST API (`/api/v1`) üzerinden sizin adınıza kullanır. Anahtarları sağ üstteki hesap menüsünden **API anahtarları** sayfasında yönetirsiniz.

1. **Yeni anahtar:** ad (ör. "GitHub Actions deploy"), geçerlilik süresi (30, 90 veya 365 gün; yönetici izin verdiyse süresiz), kapsam ve isteğe bağlı IP listesi.
2. **Kapsam** yalnızca kendi izinlerinizden seçilir. Anahtar istek anında kapsam ile hesabınızın o anki izinlerinin kesişimiyle çalışır: rolünüzden kaldırılan bir izin anahtarla da kullanılamaz.
3. **İzin verilen IP adresleri:** virgül veya satır sonuyla tek IP ya da CIDR (ör. `203.0.113.10, 10.0.0.0/8`). Boşsa her adresten kullanılır.
4. **Anahtar oluştur** sonrası tam anahtar (`smk_…`) yalnızca bir kez gösterilir. **Kopyala** ile alıp gizli bir yerde (CI secret) saklayın. Panel anahtarın kendisini saklamaz; kaybederseniz iptal edip yenisini oluşturun.
5. Listede son kullanım zamanı ve IP'si görünür. Artık kullanmadığınız anahtarı **İptal et**; iptal edilen anahtarla gelen istekler hemen reddedilir.

Hesabınız pasifleştirilir veya kilitlenirse anahtarlarınız da çalışmaz. Kullanıcı yönetimi yetkisi olan yönetici **Tüm anahtarlar** sekmesinde herkesin anahtarını görür ve iptal edebilir.

**API belgesi** sekmesi temel adresi, kimlik doğrulama başlığını, curl örneklerini, uç listesini ve OpenAPI belgesinin (`/api/v1/openapi.json`) bağlantısını gösterir. Örnek:

```bash
curl -H "Authorization: Bearer $SM_API_KEY" https://panel.example.com/api/v1/servers
```

Her uç paneldeki izni ister; anahtarda izin yoksa yanıt 403'tür. Anahtar başına dakikada 120 istek sınırı vardır (yapılandırılabilir).

## 19. Denetim kaydı (Audit Log) {#denetim-kaydi}

**Audit Log** giriş, sunucu, Docker, yedek, dağıtım, ayar ve eklenti işlemlerini tutar. Kayıt panelden silinemez ve hiç temizlenmez.

- Arama, işlem, sonuç, tarih aralığı, kullanıcı, IP (başı yeterli) ve hedef türüyle süzün.
- Satırın saatine tıklayınca ayrıntı açılır: tarayıcı, IP ve zincir imzası.
- **CSV indir** en çok 50.000 satır verir; **Bütünlüğü doğrula** HMAC zincirini kontrol eder ve kayıt değiştirilmiş, silinmiş veya araya eklenmişse nerede kırıldığını gösterir. İkisi ayrı yetki ister.

Sunucu **Activity** sekmesi aynı kayıtların o sunucuya ait olanlarını gösterir.

## 20. Ayarlar ve kayıt saklama {#ayarlar}

**Ayarlar** sürüm, ortam, çalışma süresi ve veritabanı durumunu gösterir. Ayar yönetimi yetkisi olan kullanıcı aralıkları değiştirip **Kaydet**; çalışan işler yeni değeri bir sonraki turda kullanır. Her değişiklik denetim kaydına yazılır.

| Grup | Ne ayarlanır |
| --- | --- |
| İzleme | Otomatik toplama, aralık, eşzamanlılık, çevrimdışı sayılma, CPU/RAM/disk uyarı ve kritik eşikleri, ham ve saatlik metrik saklama, kaynak geçmişi aralığı (dakika), temizlenebilir alan taraması (saat) |
| Alarmlar | Değerlendirme aralığı, uptime en kısa aralık, SSL kontrol aralığı ve uyarı günü, bildirim geçmişi |
| Yedekleme | Zamanlayıcı, eşzamanlılık, saat dilimi, yedek / geri yükleme zaman aşımı |
| Güvenlik taraması | Otomatik tarama aralığı, saklama, sunucu başına korunan tarama |
| Bulut | Otomatik eşitleme aralığı |
| Kayıt saklama | Deployment logları, yedek logları, toplu komut geçmişi, terminal oturumları, kapanmış alarmlar, servis işlem logları, container örnekleri (7), container saatlik özetleri (90), process anlık görüntüleri (7) (gün; 0 = silme) |

Kayıt saklamada log metni kırpılır, kayıt kalır; proje, iş veya servis başına en yeni 10 kayıt korunur. Açık alarmlar ve denetim kayıtları hiç silinmez. Agent aralığı, bağlantı anahtarları, `Deployment:AcmeEmail` ve terminal politikası bu sayfada değil, yapılandırma dosyasındadır.

## 21. Eklentiler {#eklentiler}

**Eklentiler** yalnızca sistem yöneticisindedir. **Kur** tabloları oluşturur ve varsayılan izinleri rollere ekler. **Devre dışı bırak** sayfaları ve sunucu sekmesini kapatır, arka plan işlerini durdurur; veriyi silmez. Kaldırma düğmesi yoktur.

| Eklenti | Ne yapar |
| --- | --- |
| Dokploy | Sunucu sekmesi; kurulum sihirbazı ve durum |
| Dokku | Sunucu sekmesi; kurulum ve uygulama listesi |
| GitHub | GitHub App ile depo bağlama |
| Hetzner, DigitalOcean, Vultr, Linode, Scaleway | Bulut hesapları ([Bulut](#bulut)) |
| E-posta, Telegram, Discord | Bildirim kanalları ([Alarmlar](#alarmlar)) |
| S3, Azure Blob | Yedek depolama ([Yedekleme](#yedek-depolama)) |
| Ek Servis Şablonları | Meilisearch, ClickHouse ve Keycloak servisleri ([Eklenti şablonları](#servis-eklenti-sablonlari)) |

### Dokploy

Kurulum sihirbazı işletim sistemi, mimari, bellek (en az 2 GB), disk, Docker, Swarm ve 80/443/3000 portlarına bakar, sonra resmi kurulum betiğini çalıştırır. Betik Docker yoksa kurar ve Traefik'i 80/443'e, paneli 3000'e yayınlar. Sürüm: son kararlı, canary veya belirli sürüm. Onay için sunucu adını yazarsınız; sayfadan çıksanız da kurulum sürer. **Bağlantı ayarları**nda panel adresi ve Dokploy API anahtarı kaydedilir; anahtar ekranda tekrar görünmez.

### Dokku

Sekme sürümü ve uygulama listesini okur. Kurulu değilse kurulum düğmesi resmi betiği sudo ile başlatır. Çıktı sayfada yenilenir. Uygulama satırından süreç yeniden başlatılır.

### GitHub App

1. **GitHub → Yeni GitHub App oluştur**: uygulama adı ve isteğe bağlı kurum → **GitHub'da oluştur**. Yalnızca Contents ve Metadata okuma izni istenir.
2. GitHub panele döner; **Devam et**, ardından **Hesaba kur** ile uygulamayı hesabınıza veya kurumunuza kurun.
3. Proje formunda **Kaynak** olarak bu bağlantıyı seçip depoyu listeden alın.

Var olan bir App'i **Mevcut bir App'i ekle** ile App ID ve `.pem` özel anahtarıyla ekleyebilirsiniz. Panel ters vekil arkasındaysa `GitHub:PublicBaseUrl` doldurulmalıdır. **Kaldır** uygulamayı GitHub'dan silmez; projeler kullanıyorsa kaldırılamaz.

## 22. Sık iş sırası {#sik-is-sirasi}

Yeni bir uygulama sunucusu:

1. Grubu ve maliyeti netleştirin.
2. Sunucuyu ekleyin, bağlantıyı test edin.
3. Metrics'te ilk ölçümün geldiğini görün.
4. Docker sekmesinde `docker` yetkisini doğrulayın.
5. Gerekiyorsa **Servisler**den veritabanını kurun, otomatik yedeği açın.
6. Projeyi ekleyin, ortam değişkenlerini girin, servisi projeye bağlayın, deploy edin.
7. Yedek deposunu test edin, işi kurun, bir kez elle yedek alın.
8. Disk ve yedek-başarısız alarmını kanala bağlayın.
9. Gerekirse uptime ve SSL kontrolü ekleyin.

## 23. Sık sorulan sorular ve sorun giderme {#sorun-giderme}

### Ortam değişkenini değiştirdim ama container'a gitmiyor

Kaydetmek yetmez; değişiklik bir sonraki deploy'da veya **Uygula / Yeniden başlat** ile `.env` dosyasına yazılır. Compose projesinde servisin `environment:` bölümünde aynı anahtar varsa o değer önceliklidir; anahtarı oradan kaldırın. Komutlar türündeki projede panel container yönetmez; değişkenleri komutlarınızda `.env`'den okuyun. "Projenin başarılı bir deployment'ı yok" uyarısı alırsanız önce bir kez deploy edin.

### Servisin portu dışarıdan açılmıyor (veya herkese açık)

- Port yayınlanmış mı ve **Dışarıya aç** işaretli mi bakın; işaretsizse port yalnızca `127.0.0.1`'de dinler.
- **İzin verilen IP** listesinde kendi IP'niz var mı bakın. Liste doluysa diğer adresler `DOCKER-USER` kuralıyla düşürülür.
- UFW'de port açmak işe yaramaz: Docker portları UFW'yi atlar, kısıtlama `DOCKER-USER` zincirindedir. Sağlayıcınızın bulut güvenlik duvarını da kontrol edin.
- "IP kısıtlaması uygulanamadı" hatası sunucuda iptables veya `DOCKER-USER` zincirinin olmadığını gösterir.
- Sunucu yeniden başladıysa ve Genel sekmesi kuralların kaybolduğunu söylüyorsa **Kuralları yeniden uygula**.

### Let's Encrypt sertifikası alınamadı

- **Vekil kur** "Let's Encrypt e-postası yapılandırılmamış (Deployment:AcmeEmail)" diyorsa yönetici yapılandırmada `Deployment:AcmeEmail` (Docker Compose'da `SM_ACME_EMAIL`) değerini girip paneli yeniden başlatmalıdır.
- Domain'in A kaydı doğrudan bu sunucuya bakmalı; Cloudflare turuncu bulutu açıksa sertifika türü olarak **Cloudflare**'i seçin.
- 80 ve 443 portu internete açık olmalı ve başka bir servis tarafından kullanılmamalıdır ("80 veya 443 portu dolu").

### Proje silinirken sunucudaki klasör silinmedi

"Klasör panel tarafından oluşturulmamış (.sm-managed işareti yok)" mesajı, klasörün önceden var olduğunu gösterir. Panel kendi oluşturmadığı klasörü güvenlik için silmez. Klasörü sunucuda elle temizleyin. Aynı nedenle, boş olmayan ve git deposu olmayan bir klasöre deploy edilmez; boş bir klasör seçin.

### "Sunucuda Docker kurulu değil" / Docker bilgisi alınamadı

- Docker'ı sunucuya kurun (https://docs.docker.com/engine/install/) ve servisin çalıştığını doğrulayın.
- "Kullanıcının Docker'a erişim yetkisi yok" diyorsa SSH kullanıcısını `docker` grubuna ekleyin veya sunucu ayarlarında sudo'yu açın.
- "sudo parola istiyor" diyorsa sunucu ayarlarına sudo parolasını girin.

### Terminal açılmıyor, tarama başlamıyor

"Host key henüz doğrulanmadı" mesajında sunucu sayfasında **Bağlantıyı Test Et** yapın.

### Yedek başarısız

Hata mesajı çalıştırma sayfasındadır. Sık nedenler: veritabanı istemcisi container'da veya sunucuda yok, dosya okumak için sudo gerekli, volume yedeği için `alpine:3` imajı indirilemiyor, şifreleme açık ama parola kayıtlı değil (işi düzenleyip parola girin), depolama testi başarısız.

### Bir menü veya düğme görünmüyor

Menü ve düğmeler rol yetkisine göre gizlenir. Sistem yöneticinizden rolünüzü kontrol etmesini isteyin. Eklenti devre dışıysa ona ait sekmeler de görünmez.
