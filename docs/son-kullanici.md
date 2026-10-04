# Server Manager — Son kullanıcı kılavuzu

Bu metin paneli her gün kullanan kişi içindir. Kurulum, sunucu adresi veya parola biçimi anlatılmaz. Hesabınızı size yöneticiniz açar.

Server Manager, birden fazla sunucuyu tek ekrandan görmenizi sağlar. Sunucunun çalışıp çalışmadığını, dolup dolmadığını, yedeğinin alınıp alınmadığını ve bir sorun olduğunda kime haber gittiğini buradan takip edersiniz.

## Giriş

1. Yöneticinizin verdiği adresle panele girin.
2. E-posta ve parolanızı yazın.
3. Hesabınızda ikinci doğrulama açıksa telefonunuzdaki kodu da girin.

Bir süre işlem yapmazsanız oturum kapanır; yeniden giriş yapmanız gerekir. Beş kez yanlış parola denerseniz hesap kısa süre kilitlenir.

Sağ üstten koyu veya açık görünüme geçebilirsiniz.

## Ekranda ne görürsünüz

Soldaki menü, hesabınızın yetkisine göre değişir. Göremediğiniz bir sayfa size kapalıdır; bu bir arıza değildir.

| Menü | Ne işe yarar |
| --- | --- |
| Dashboard | Tüm sunucuların özeti: kaç tanesi sağlıklı, kaç tanesi sorunlu, açık uyarılar |
| Sunucular | Kayıtlı sunucuların listesi |
| Gruplar | Sunucuları proje veya ekip gibi gruplara ayırma |
| Sağlayıcılar | Bulut hesabındaki makineleri panelde gösterme ve yeni makine açma |
| Maliyet | Aylık sunucu giderinin özeti |
| Docker, İmajlar, Volume'lar, Ağlar | Sunucudaki uygulamaların kutuları ve dosya alanları |
| Projeler, Deployment'lar | Bir uygulamayı sunucuya gönderme ve geçmişi |
| Terminal | Sunucuya tarayıcıdan komut yazma |
| Dosyalar | Sunucudaki klasörlere göz atma, dosya açma ve indirme |
| Servisler, Process'ler, Loglar | Sunucuda çalışan işler ve kayıtlar |
| Toplu komut | Aynı işi birden çok sunucuda birden yaptırma |
| Metrikler | İşlemci, bellek ve disk doluluk grafikleri |
| Uptime | Bir sitenin veya kapının açık olup olmadığı |
| Alarmlar | Sorun çıktığında oluşan uyarılar ve kime haber gideceği |
| SSL | Sertifikanın ne zaman biteceği |
| Yedekleme | Yedek alma, indirme ve geri koyma |
| Güvenlik | Sunucunun temel güvenlik kontrolü |
| Kullanıcılar | Panele kimlerin gireceği |
| Audit Log | Panelde kimin ne yaptığı |
| Eklentiler | Ek araçları açma veya kapatma |
| Ayarlar | Panelin sürümü ve ne sıklıkta kontrol ettiği. Parola ve anahtar burada değişmez. Aralıkları yalnızca yönetici kaydeder |

Bir sunucuya tıkladığınızda üstte sekmeler çıkar: özet, grafikler, Docker, terminal, dosyalar, dağıtımlar, güvenlik ve sistem kayıtları. Hesabınızın görmesine izin verilen sekmeler listelenir. Dokploy veya Dokku gibi ek araçlar da, yöneticiniz açtıysa, burada ayrı sekme olur.

## Sunucu renkleri

| Durum | Anlamı |
| --- | --- |
| Sağlıklı | Ölçümler normal |
| Uyarı | Doluluk yükselmiş, henüz kritik değil |
| Kritik | Doluluk çok yüksek |
| Çevrimdışı | Panele cevap vermiyor |
| Bakımda | Bilerek bakıma alındı; rengi kendiliğinden değişmez |

İlk bağlantı doğrulanmadan grafikler gelmez. Sunucu sayfasındaki **Bağlantıyı test et** bunu yapar. Yönetici “izleme kapalı” dediyse otomatik ölçüm durur; **Şimdi topla** ile tek seferlik bakabilirsiniz.

## Günlük işler

**Bir sunucunun doluluğuna bakmak.** Sunucular listesinden sunucuyu açın, grafikler sekmesine geçin. Son bir saatten son bir aya kadar aralık seçebilirsiniz. Liste ve özet ekranı yeni ölçüm geldikçe kendiliğinden güncellenir.

**Bir uygulamayı durdurup açmak.** Docker menüsünden sunucuyu seçin. Durdurma, yeniden başlatma ve silme işlemlerinde panel sizden onay ister. Silmede kutunun adını yazmanız istenir.

**Dosya indirmek veya düzeltmek.** Dosyalar menüsünden sunucuyu seçin, klasörlerde gezinin. Metin dosyasını açıp kaydedebilir, bilgisayarınızdan dosya bırakarak yükleyebilirsiniz.

**Komut yazmak.** Terminal menüsünden sunucuyu seçin. Tehlikeli bir komut (örneğin makineyi kapatmak veya diski silmek) sunucuya gitmeden önce ikinci kez sorulur. Yazdığınız komutlar panelde saklanır; ekranda görünen yazıların tamamı saklanmaz.

**Yedek almak.** Yedekleme sayfasında işler, geçmiş ve depolama sekmeleri vardır. **Şimdi yedek al** o an bir kopya çıkarır. Zamanı gelen işler kendiliğinden çalışır. Başarılı kopyalar, işte yazan “son kaç kopya kalsın” kuralına göre eskiden silinebilir. İşin kendisini silseniz bile daha önce alınmış kopyalar ve geçmiş durur; geçmişten indirebilir veya geri koyabilirsiniz. Silinmiş bir işin sayfası açılır, fakat yeni yedek alınamaz ve iş düzenlenemez.

**Uyarı gelince.** Üstteki zil açık uyarıları gösterir. Alarmlar sayfasında uyarıyı üstlenebilirsiniz; böylece başkası da sizin baktığınızı görür. Kural ve haber kanalı (e-posta, Telegram, Discord) ayarını genelde yönetici yapar.

**Bir site ayakta mı?** Uptime sayfasında adres veya kapı kontrolü vardır. SSL sayfası sertifikanın bitişine kalan günü gösterir.

**Bir uygulamayı sunucuya göndermek.** Projeler sayfasında depo ve sunucu tanımlıdır. Deployment geçmişinden canlı yazıyı izleyebilirsiniz.

**Aynı işi çok sunucuda yapmak.** Toplu komut sayfasında komutu ve sunucuları seçin. Çok sayıda sunucu seçtiyseniz panel sayı yazmanızı ister. Çalışma bitince her sunucunun sonucu ayrı durur.

## Kim ne yapabilir

Dört günlük rol vardır. Üstüne bir de her şeyi görebilen sistem yöneticisi vardır.

| Rol | Kısaca |
| --- | --- |
| İzleyici | Bakar, değiştirmez |
| Geliştirici | Bakar, dağıtım başlatabilir, dosya indirebilir |
| Operatör | Docker, terminal, dosya, yedek alma ve uyarı üstlenme |
| Yönetici | Sunucu ekler, kullanıcı dışında neredeyse her şeyi yönetir |
| Sistem yöneticisi | Kullanıcılar, eklentiler ve ayarlar dahil tamamı |

Bir düğme görünmüyorsa o iş size açık değildir.

## Silinen kayıtlar

Paneldeki sunucu, yedek işi veya grup silindiğinde kayıt çöpe atılmaz, gizlenir. Geçmiş, denetim kaydı ve alınmış yedekler durur. Denetim kaydından silinmiş bir sunucuya giderseniz “bu sunucu silindi” yazısını görürsünüz; bağlantı ve grafikler kapalıdır.

Denetim kayıtları panelden silinemez ve değiştirilemez.

## Ne zaman yöneticinize yazmalısınız

- Panele giremiyorsanız
- Sunucu sürekli çevrimdışı görünüyorsa
- Yedek sürekli başarısız oluyorsa
- Görmeniz gereken bir menü yoksa
- İkinci doğrulama koduna ulaşamıyorsanız

Parola ve sunucu giriş bilgileri bu kılavuzda yoktur. Onları kimseyle paylaşmayın ve sohbet veya e-postaya yapıştırmayın.
