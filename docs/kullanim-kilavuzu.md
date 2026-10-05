# Mag Server Manager — Kullanma kılavuzu

Bu kılavuz, hesabı açık bir operatör veya yöneticinin panelde işi nasıl yapacağını anlatır. Kurulum, veritabanı ve yapılandırma anahtarları [teknik dokümanda](teknik-dokuman.md) durur. Ekranların sade anlatımı [son kullanıcı kılavuzundadır](son-kullanici.md).

Panel adresi kurulumunuza göredir. Yerel geliştirmede `http://localhost:5180` veya `https://localhost:7180` kullanılır.

## 1. Oturum

1. E-posta ve parolayla giriş yapın.
2. İki adımlı doğrulama açıksa authenticator kodunu girin. Zorunluysa kodu kurmadan panele geçemezsiniz.
3. Beş hatalı denemeden sonra hesap 15 dakika kilitlenir.
4. Oturum, işlem yokken varsayılan olarak 60 dakika sonra düşer.

Hesap oluşturma **Kullanıcılar** sayfasındadır. Bunu yalnızca sistem yöneticisi yapar. Roller: SuperAdmin, Admin, Operator, Developer, Viewer. Menü, rolünüzün iznine göre kısalır.

## 2. İlk sunucu

1. **Sunucular → Sunucu ekle**.
2. Ad, hostname veya IP, SSH portu (çoğu sunucuda 22), kullanıcı adı ve kimlik bilgisini girin.
3. Kimlik parola veya private key olabilir. Key passphrase istiyorsa onu da yazın.
4. SSH kullanıcısı `docker` veya root değilse **sudo** kutusunu işaretleyin ve sudo parolasını girin. Dosya yedeği ve birçok sistem işi bunu ister.
5. Ortam, grup, etiket, konum, sağlayıcı ve aylık maliyeti isteğe bağlı doldurun.
6. Kaydedin, sunucuyu açın, **Bağlantıyı test et**.

İlk başarılı test, sunucunun host key parmak izini kaydeder. Sonraki bağlantılarda anahtar değişirse panel bağlanmaz. Adres veya port değişince parmak izi sıfırlanır; testi yeniden yapın.

**İzleme** kutusu açıksa, parmak izi kaydından sonra ölçümler kendiliğinden gelir. Kapalıysa yalnızca **Şimdi topla** çalışır.

Sunucuyu silmek için adını birebir yazmanız istenir. Kayıt gizlenir; denetim geçmişi ve eski yedekler durur.

## 3. Sunucu sayfası

Üstteki sekmeler yetkinize göre görünür:

| Sekme | İş |
| --- | --- |
| Overview | Kimlik, son bağlantı, son ölçüm |
| Metrics | İşlemci, bellek, disk, ağ grafikleri |
| Docker | Container, imaj, volume, ağ |
| Terminal | Tarayıcıdan SSH kabuğu |
| Files | SFTP dosya yöneticisi |
| Deployments | Bu sunucuya giden dağıtımlar |
| Security | Güvenlik taraması |
| Servisler | Bu sunucuya tek tıkla kurulan veritabanı ve uygulama servisleri |
| Sistem Servisleri, Process'ler, Loglar | systemd/OpenRC, süreç listesi, journal ve `/var/log` |
| Activity | Bu sunucuyla ilgili denetim kayıtları |
| Dokploy, Dokku | Eklenti açıksa kurulum ve durum |

Soldaki Docker, Terminal, Dosyalar ve Metrikler önce sunucu seçtirir, sonra aynı sekmeye gider.

## 4. İzleme, uptime ve SSL

**Metrics** sekmesinde 1 saat ile 30 gün arası seçin. Kısa aralık ham ölçümden, 7 ve 30 gün saatlik özettendir.

Durum: eşik aşılmazsa Sağlıklı, uyarı eşiğinde Uyarı, kritik eşikte Kritik. Üst üste bağlantı koparsa Çevrimdışı. Bakımda işaretli sunucunun durumu otomatik değişmez.

Panelin SSH ile ulaşamadığı sunucu için Metrics sekmesindeki agent paneli kullanılır. **Agent kur** bir kez gösterilen komutu üretir. Komutu sunucuda root olarak çalıştırın. Token bir daha gösterilmez. İptal edince eski komut geçersiz olur. Agent birkaç dakika rapor göndermezse panel yine SSH ile ölçer; ikisi birden aynı sunucuyu doldurmaz.

**Uptime** bir adresin veya TCP portunun açık olduğunu dakikalık kontrol eder. **SSL** sertifika bitişine kalan günü izler. İkisi de alarm kuralına bağlanabilir.

## 5. Alarm

1. **Alarmlar** sayfasında açık olayları görün. Zil simgesi özeti gösterir.
2. Üstlenmek, “buna bakıyorum” demektir.
3. **Kurallar** eşiği tanımlar: doluluk, çevrimdışı, uptime, SSL bitişi, yedek başarısız.
4. **Kanallar** e-posta, Telegram veya Discord olabilir. Kanalı kaydetmeden önce dene düğmesi mesaj yollar.

Kanal sırrı kayıttan sonra ekranda görünmez. Boş bırakıp kaydetmek eski sırrı korur.

## 6. Docker

Sunucuda Docker kurulu olmalı ve kullanıcı ya `docker` grubunda olmalı ya da sudo işaretli olmalıdır.

Yapabilecekleriniz, izninize göre:

- Container başlat, durdur, yeniden başlat, duraklat, sonlandır, yeniden adlandır, sil
- Log ara, canlı izle, indir
- Container içine terminal aç
- İmaj çek, sil, kullanılmayanları temizle
- Volume ve ağ oluştur veya sil

Silme ve temizlik, ad yazılarak onaylanır. Yeni bir Docker ağı, sunucunun kendi ağıyla veya panelin bağlandığı adresle çakışırsa reddedilir. Böylece SSH yolunu yanlışlıkla kesmezsiniz.

## 7. Terminal ve dosyalar

**Terminal** bir veya daha çok sekme açar. Sayfa yenilense de aynı oturuma dönebilirsiniz. Araç çubuğunda arama, kopyala, yapıştır, çıktı indirme ve tam ekran vardır.

`rm -r`, disk biçimlendirme, kapatma ve güvenlik duvarı gibi komutlar sunucuya gitmeden ikinci onay bekler. Onay gelmezse komut iptal olur. Bu davranış yönetici ayarıyla kapatılabilir veya tamamen engellenebilir.

**Dosyalar** klasörlerde gezinir. Yeni klasör, yeniden adlandırma, taşıma, kopyalama, silme, yükleme, indirme ve izin değiştirme vardır. Metin dosyası editörde açılır. Silme onay ister.

## 8. Yedekleme

Üç sekme: **İşler**, **Geçmiş**, **Depolama**.

### Depolama

Önce hedefin yazılabilir olduğunu **Test et** ile doğrulayın.

| Hedef | Ne zaman |
| --- | --- |
| Yerel disk | Yedek dosyası panelin kendi diskinde kalsın |
| S3 uyumlu | Amazon S3, Cloudflare R2, MinIO, Backblaze B2 |
| Azure Blob | Azure veya Azurite. Azurite adresi `http://127.0.0.1:10000/devstoreaccount1` biçimindedir |

Anahtar ve hesap parolası bir daha gösterilmez.

### İş

Tür seçin:

- **Dosya:** sunucudaki klasörler. Hariç tutmak için `*.log` gibi desen yazabilirsiniz. Okuma için çoğu zaman sudo gerekir.
- **Docker volume:** volume adı. Çalışan uygulamayı durdurmaz. İçinde veritabanı dosyası varsa tutarlı kopya için veritabanı türünü kullanın.
- **Veritabanı:** PostgreSQL, MySQL/MariaDB, MongoDB, Redis veya SQL Server. Container içinde veya sunucunun kendi istemcisiyle. Parola komut satırına yazılmaz. MongoDB'de veritabanı adı boşsa tümü yedeklenir. Redis ve SQL Server aynı sunucuda çalışmalıdır. Redis yedeği panelden geri yüklenmez; geri yükleme sayfası elle yapılacak adımları gösterir.

Zamanlama: elle, saatlik, günlük veya haftalık. Saklama: son N kopya ve isteğe bağlı olarak N günden eskiler. En yeni başarılı kopya bu kuralla silinmez.

**Şimdi yedek al** ilerlemeyi işin sayfasında gösterir. Aynı iş için ikinci işlem, ilki bitene kadar başlamaz.

Geçmişten indirebilir, şifreliyse çözülmüş indirebilir ve geri yükleyebilirsiniz. İş silinmiş olsa da geçmişteki bağlantı açılır; yeni yedek ve düzenleme kapalıdır.

## 9. Dağıtım

1. **Projeler** altında depo, dal, sunucu, build ve deploy komutlarını tanımlayın.
2. GitHub App eklentisi açıksa depoyu kişisel erişim anahtarı yazmadan bağlayabilirsiniz. Bağlantı GitHub sayfasında onaylanır.
3. **Deployment başlat**. Canlı log deployment sayfasındadır.
4. Bitmiş bir dağıtımı yeniden çalıştırabilirsiniz.
5. Proje sayfasındaki **Domainler** kartından host ekleyin. DNS kaydını siz açarsınız. Sertifika Cloudflare (sunucuda sertifika yok, yalnızca 80), Let's Encrypt veya yapıştırdığınız özel sertifika olabilir. Komut türündeki projeye domain bağlanmaz.
6. İlk domainden önce **Vekil kur**. Bu, sunucuda `sm-traefik` adlı Traefik'i 80 ve 443'e alır. Port doluysa kurulum durur.
7. Proje silinirken kutu işaretlenmezse yalnızca panel kaydı kapanır. Kutu işaretlenirse sunucudaki klasör, container, imaj, volume ve domain yönlendirmesi de silinir. Deployment geçmişi kalır.

8. **Ortam değişkenleri** sekmesinde değişken ekleyin, değiştirin veya `.env` içe aktarın. Değişiklik bir sonraki deploy'da ya da **Uygula / Yeniden başlat** ile (build olmadan) sunucuya yazılır. Compose projesinde `.env` tüm servislere aktarılır.
9. **Çalışma logları** sekmesinde container seçip son satırları, yalnızca hata/uyarıları veya canlı akışı izleyin.
10. **Otomatik deploy** sekmesinde webhook'u açın; gösterilen adres ve gizli anahtarı GitHub'da *Settings → Webhooks* (içerik türü `application/json`, yalnızca push) veya GitLab'da *Settings → Webhooks* ("Secret token", Push events) içine girin. Panel ters vekil arkasındaysa yönetici `Deployment:PublicBaseUrl` değerini ayarlamalıdır.
11. Deployment geçmişinde **Bu sürüme geri dön** önceki başarılı sürümü yeniden çalıştırır. Dockerfile projesinde imaj sunucuda duruyorsa build yapılmaz; saklanan imaj sayısı `Deployment:KeepImageCount` ile sınırlıdır.

Build komutu hedef sunucuda çalışır. Bu yetkiyi yalnızca sunucuya komut yazmasına güvendiğiniz role verin. Let's Encrypt için domain'in A kaydı bu sunucuya doğrudan bakmalıdır. Turuncu bulut açıksa Cloudflare türünü seçin.

## 10. Servisler

Menüde **Servisler** veya sunucu sayfasındaki **Servisler** sekmesi; tek tıkla PostgreSQL, MySQL, MariaDB, Redis, MongoDB, SQL Server, MinIO, RabbitMQ, Adminer, pgAdmin, Uptime Kuma ve n8n kurar. (systemd servisleri artık **Sistem Servisleri** adıyla durur.)

1. **Servis ekle** → şablonu seçin. Sunucu, ad, sürüm ve veri saklama (Docker volume veya sunucu klasörü) girin. Parola otomatik üretilir.
2. **Ağ ve portlar:** port yayınlanırsa varsayılan olarak yalnızca sunucudan (`127.0.0.1`) erişilir. **Dışarıya aç** tüm internete açar; bu durumda **izinli IP** listesi girin. Docker portları UFW'yi atlar; panel kısıtlamayı `DOCKER-USER` kurallarıyla yapar ve sunucu açılışında `sm-services-firewall` systemd birimiyle geri yükler.
3. Veritabanı şablonlarında **Otomatik yedek** kutusunu işaretleyip depolama hedefi, saat (veya saat aralığı), saklanacak yedek sayısı ve isteğe bağlı şifreleme parolası seçin. İş, kurulum başarıyla bitince oluşur.
4. **Kur**. Adımlar canlı görünür; sayfayı kapatsanız da kurulum sürer.
5. Servis sayfası: **Genel** (durum, iç/dış adres, maskeli bağlantı adresi; **Parolayı göster** yetki ister ve kayda geçer), **Loglar**, **İşlemler**, **Ayarlar** (değişken, port, IP listesi, sınırlar; kaydedince container veri korunarak yeniden oluşturulur), **Sürüm** (önce yedek alın; ana sürüm değişiminde ayrıca onay istenir), **Konsol** (psql, mysql, redis-cli …), **Tehlikeli bölge** (adı yazarak kaldırma; "Veriyi de sil" geri alınamaz).
6. **Projeye bağla:** Genel sekmesinde aynı sunucudaki projeyi seçin, eklenecek değişkenleri işaretleyin ve gerekirse adlarını değiştirin (ör. `DATABASE_URL`). Var olan anahtarların değiştirilip değiştirilmeyeceğini seçin. **Hemen uygula** projeyi build etmeden yeniden başlatır. Proje container'ları `sm-services` ağına katılır ve servise container adıyla (`sm-svc-…`) bağlanır. Proje sayfasının **Ortam değişkenleri** sekmesinde **Bağlı servisler** görünür; **Bağı kaldır** değişkenleri silmez, kutu işaretlenirse yalnızca bağlarken eklenenleri siler.
7. Veritabanı servisinin Genel sekmesindeki **Yedekleme** kartı servisi yedekleyen işleri gösterir; **Yedek işi oluştur** ile sonradan iş eklenir.

Projeye bağlamak için hem servis yönetimi hem proje düzenleme yetkisi, yedek işi için yedek yönetimi yetkisi gerekir; yetkisi olmayan kullanıcı bu düğmeleri görmez.

## 11. Toplu komut ve şablon

**Toplu komut** seçilen sunucularda aynı betiği çalıştırır. Onay penceresi komutu ve sunucu listesini gösterir. Beş veya daha fazla sunucuda sayıyı yazmanız istenir.

**sudo ile çalıştır** yalnızca sudo açık sunuculara gider. Diğerleri başarısız sayılır, yetkisiz çalıştırılmaz.

**Şablonlar** iki türlüdür:

- Kabuk betiği, toplu komut formuna kopyalanır. Çalıştırmadan önce düzenleyebilirsiniz.
- cloud-init, bulutta yeni makine açılırken ilk kurulum metni olarak gider. `#cloud-config` veya `#!` ile başlamalıdır.

Şablona parola veya API anahtarı yazmayın. Metin şifrelenmeden durur.

## 12. Bulut

**Sağlayıcılar** Hetzner Cloud, DigitalOcean, Vultr, Linode ve Scaleway hesaplarını tutar. Eklenti kapalıysa hesap listede kalır, eşitleme ve yeni makine çalışmaz.

1. API anahtarını ekleyin. Panel anahtarı sağlayıcıda dener, sonra saklar.
2. Listeden **Panele ekle** sunucu formunu ad, IP ve fiyatla doldurur. SSH kullanıcı ve parola veya key hâlâ sizin girmeniz gerekir.
3. **Eşitle** bağlı sunucuların aylık fiyatını günceller.
4. **Sunucu oluştur** bölge, tip ve imaj seçtirir. Onayda sunucu adını yazarsınız. Hetzner, Vultr ve Linode, anahtar vermezseniz root parolasını yalnızca o pencerede bir kez gösterir. Bu parola panele yazılmaz. DigitalOcean parolayı e-postanıza gönderir. Linode adı harfle başlamalıdır; nokta kullanılamaz.

**Maliyet** sayfası para birimine göre toplar. Doları liraya çevirmez.

## 13. Dokploy ve Dokku

İkisi de sunucu sekmesidir. Sistem yöneticisi eklentiyi kapatırsa sekme kaybolur; sunucudaki kurulum silinmez.

**Dokploy.** Kurulum sihirbazı işletim sistemi, bellek, disk, Docker ve portlara bakar, sonra resmi kurulum betiğini çalıştırır. Sayfadan çıksanız da kurulum sürer. Durum sayfası sürüm, adres ve sağlık kontrolünü gösterir. Proje özeti için Dokploy API anahtarı kaydedilir; anahtar ekranda tekrar görünmez.

**Dokku.** Sekme sürümü ve uygulama listesini okur. Kurulu değilse **Dokku kur** resmi betiği sudo ile başlatır. Çıktı sayfada yenilenir. Uygulama satırından süreç yeniden başlatılır.

## 14. Güvenlik taraması

**Güvenlik** sayfası veya sunucunun Security sekmesi SSH ayarı, açık port, güvenlik duvarı ve bekleyen güncelleme gibi maddeleri tarar. Tarama öncesi bağlantı testi yapılmış olmalıdır. Bulgu, sunucuyu kendiliğinden değiştirmez.

## 15. Kullanıcı, denetim, ayarlar

**Kullanıcılar** yalnızca sistem yöneticisindedir. Rol değişikliği ve pasifleştirme anında değil, en geç bir dakika içinde açık oturumu düşürür.

**Audit Log** giriş, sunucu, Docker, yedek, dağıtım ve eklenti işlemlerini tutar. Kayıt silinemez. Bir satırın saatine tıklayınca ayrıntı açılır. Sunucu etkinlik sekmesi aynı kayıtların o sunucuya ait olanlarını gösterir.

**Ayarlar** sürüm, ortam, çalışma süresi ve veritabanı durumunu gösterir. Yönetici izleme, alarm, yedek, tarama ve bulut aralıklarını buradan kaydeder; bir sonraki turda uygulanır. Agent aralığı ve bağlantı anahtarları bu sayfada değişmez.

**Eklentiler** yalnızca sistem yöneticisindedir. Kur, tabloları ve izinleri ekler. Devre dışı bırakmak sayfayı kapatır, veriyi silmez. Kaldırma düğmesi yoktur.

## 16. Sık iş sırası

Yeni bir uygulama sunucusu:

1. Grubu ve maliyeti netleştirin.
2. Sunucuyu ekleyin, bağlantıyı test edin.
3. Metrics’te ilk ölçümün geldiğini görün.
4. Docker sekmesinde `docker` yetkisini doğrulayın.
5. Yedek deposunu test edin, işi kurun, bir kez elle yedek alın.
6. Disk ve yedek-başarısız alarmını kanala bağlayın.
7. Gerekirse uptime ve SSL kontrolü ekleyin.
