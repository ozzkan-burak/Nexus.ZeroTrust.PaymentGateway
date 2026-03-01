// Ag (Network) isleyicileri icin gerekli kutuphane.
using System.Net;
// Sertifika islemleri icin gerekli kutuphane.
using System.Security.Cryptography.X509Certificates;
// OpenTelemetry metriklerini tanimlamak icin gerekli kutuphaneler.
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
// Polly politikalari icin gerekli ana kutuphane.
using Polly;
// HttpClient uzerine Polly yetenekleri ekleyen eklenti kutuphanesi.
using Polly.Extensions.Http;
// YARP'in HTTP isteklerini olusturan cekirdek siniflarini barindiran kutuphane.
using Yarp.ReverseProxy.Forwarder;
// HTTP isleyicileri (Handlers) icin gerekli kutuphane.
using Microsoft.Extensions.Http;

// Web uygulamasi olusturmak icin gerekli builder nesnesini baslatiyoruz.
var builder = WebApplication.CreateBuilder(args);

// YARP'in HTTP istemcisi uretirken bizim yazdigimiz Polly fabrikasini kullanmasini soyluyoruz.
builder.Services.AddSingleton<IForwarderHttpClientFactory, PollyForwarderHttpClientFactory>();

// OpenTelemetry servislerini sisteme dahil ediyoruz.
builder.Services.AddOpenTelemetry()
    // Hangi servisten log geldigini bilmek icin servis adini kaydediyoruz.
    .ConfigureResource(resource => resource.AddService("NexusPayment.Gateway"))
    // Izleme (Tracing) ayarlarini yapilandiriyoruz.
    .WithTracing(tracing =>
    {
        // Gateway'e disaridan gelen istekleri izliyoruz.
        tracing.AddAspNetCoreInstrumentation();
        // YARP'in arka plana yaptigi gidis isteklerini (Polly denemeleri dahil) izliyoruz.
        tracing.AddHttpClientInstrumentation();
        // Verileri OTLP protokolu ile localhost'ta calisan Jaeger'a gonderiyoruz.
        tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"));
    });

// YARP servislerini bagimlilik enjeksiyonu (DI) konteynerine ekliyoruz.
builder.Services.AddReverseProxy()
    // YARP'in ayarlarini appsettings.json icinden okumasini soyluyoruz.
    // NOT: ConfigureHttpClient kismini sildik, cunku sertifika isini fabrikaya tasidik.
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Uygulamayi insa edip calistirilabilir hale getiriyoruz.
var app = builder.Build();

// YARP ara yazilimini HTTP istek hattina ekliyoruz.
app.MapReverseProxy();

// Uygulamayi baslatiyor ve gelen istekleri dinlemeye basliyoruz.
app.Run();

// --- POLLY ENTEGRASYON SINIFI ---
// YARP'in IForwarderHttpClientFactory arayuzunu dogrudan uygulayarak (implement) kendi fabrikamizi yaziyoruz.
public class PollyForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    // Cevresel degiskenleri (klasor yollari vb.) almak icin kullanacagimiz nesne.
    private readonly IWebHostEnvironment _env;

    // Bagimlilik enjeksiyonu (DI) ile IWebHostEnvironment nesnesini aliyoruz.
    public PollyForwarderHttpClientFactory(IWebHostEnvironment env)
    {
        // Gelen nesneyi sinif icindeki degiskene atiyoruz.
        _env = env;
    }

    // YARP'in arka plana istek atarken kullanacagi istemciyi (client) ureten ana metot.
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        // 1. YARP'in ihtiyac duydugu cekirdek HTTP isleyicisini (SocketsHttpHandler) olusturuyoruz.
        var handler = new SocketsHttpHandler
        {
            // YARP'in varsayilan davranisi olarak proxy kullanimini kapatiyoruz.
            UseProxy = false,
            // YARP yonlendirmelerinde otomatik yonlendirmeyi kapatiyoruz.
            AllowAutoRedirect = false,
            // Otomatik sikistirma acmayi kapatiyoruz (YARP baytlari dogrudan aktarir).
            AutomaticDecompression = DecompressionMethods.None,
            // Cookie (Cerez) kullanimini kapatiyoruz.
            UseCookies = false
        };

        // 2. mTLS Sertifikamizi dogrudan bu cekirdek isleyiciye ekliyoruz.
        // Gateway projesinin calistigi kok dizini alip sertifikanin yolunu belirliyoruz.
        var certPath = Path.Combine(_env.ContentRootPath, "client.pfx");
        // Fiziksel pfx dosyasini ve parolasini okuyoruz.
        var clientCert = new X509Certificate2(certPath, "root123");
        // Arka plana yapilacak isteklere mTLS sertifikasini ekliyoruz.
        handler.SslOptions.ClientCertificates = new X509CertificateCollection { clientCert };

        // 3. Polly Politikasini Tanimliyoruz.
        // HandleTransientHttpError metodu; HTTP 5xx hatalarini otomatik yakalar.
        // WaitAndRetryAsync metodu; Hata durumunda pes etmeden once 3 kez, 500'er milisaniye bekleyerek tekrar denemesini soyler.
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(500));

        // 4. Cekirdek isleyiciyi (handler) Polly'nin Delegation Handler'i icine sariyoruz.
        var pollyHandler = new PolicyHttpMessageHandler(retryPolicy)
        {
            // Gercek istegi atacak olan ic isleyiciyi atiyoruz.
            InnerHandler = handler
        };

        // 5. YARP'in kullanacagi Invoker nesnesini, icinde Polly ve mTLS olan bu yeni isleyiciyle donduruyoruz.
        return new HttpMessageInvoker(pollyHandler, disposeHandler: true);
    }
}