// src/Services/NexusPayment.PaymentService/Program.cs

// Sertifika dogrulama yetenekleri icin gerekli kutuphane.
using Microsoft.AspNetCore.Authentication.Certificate;
// Kestrel ayarlari icin gerekli kutuphane.
using Microsoft.AspNetCore.Server.Kestrel.Https;
// Sertifika tipleri ve iptal modlari icin gerekli kutuphane.
using System.Security.Cryptography.X509Certificates;
// OpenTelemetry metriklerini tanimlamak icin gerekli kutuphaneler.
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Web uygulamasi olusturucu nesnesini baslatiriz.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry servislerini sisteme dahil ediyoruz.
builder.Services.AddOpenTelemetry()
    // Hangi servisten log geldigini bilmek icin servis adini kaydediyoruz.
    .ConfigureResource(resource => resource.AddService("NexusPayment.PaymentService"))
    // Izleme (Tracing) ayarlarini yapilandiriyoruz.
    .WithTracing(tracing =>
    {
      // Gateway'den mTLS tuneli ile iceri giren istekleri izlemeye aliriz.
      tracing.AddAspNetCoreInstrumentation();
      // Toplanan verileri OTLP protokolu ile localhost'ta calisan Jaeger'a (4317 portu) gondeririz.
      tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"));
    });

// Kestrel (ASP.NET Core web sunucusu) ayarlarina mudahale ederek mTLS'i aktif ediyoruz.
builder.WebHost.ConfigureKestrel(options =>
{
  // Tum HTTPS baglantilari icin varsayilan ayarlari yapilandiririz.
  options.ConfigureHttpsDefaults(httpsOptions =>
  {
    // Sunucuya baglanan istemciden (YARP) kesinlikle bir sertifika talep edildigini belirtiriz.
    httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

    // TLS el sikismasi sirasinda gelen oto-imzali sertifikalarin kabul edilmesini saglar.
    httpsOptions.ClientCertificateValidation = (certificate, chain, errors) => true;
  });
});

// Sertifika dogrulama (Authentication) mekanizmasini uygulama katmaninda tanitiriz.
builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
      // Iptal (Revocation) kontrolunu devredisi birakiriz.
      options.RevocationMode = X509RevocationMode.NoCheck;
      // Gelistirme ortami amaciyla her turlu sertifika tipini kabul etmesini saglariz.
      options.AllowedCertificateTypes = CertificateTypes.All;
    });

// gRPC servis altyapisini bagimlilik enjeksiyonu (DI) konteynerine ekleriz.
builder.Services.AddGrpc();

// Uygulamayi insa ederiz.
var app = builder.Build();

// Sisteme kimlik dogrulama katmanini ekleyerek guvenligi devreye aliriz.
app.UseAuthentication();

// YARP'in /api/payments rotasini mTLS uzerinden karsilayan uc noktasi.
app.MapGet("/api/payments", () =>
{
  // --- YENI EKLENEN KISIM: KAOS MUHENDISLIGI ---
  // %50 ihtimalle sistemi bilerek cokertiyoruz.
  var random = new Random();
  if (random.NextDouble() < 0.5)
  {
    // Sistemin anlik cokusunu simule eden 500 Internal Server Error donduruyoruz.
    return Results.StatusCode(500);
  }

  // Eger sistem cokmezse basarili senaryoyu donduruyoruz.
  return Results.Ok("Payment Service mTLS ile korundu ve YARP sertifikasi basariyla dogrulandi!");
});

// Uygulamayi baslatiriz.
app.Run();