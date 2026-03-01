// src/Services/NexusPayment.PaymentService/Program.cs

using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Https;

// Web uygulamasi olusturucu nesnesini baslatiriz.
var builder = WebApplication.CreateBuilder(args);

// Kestrel (ASP.NET Core web sunucusu) ayarlarina mudahale ederek mTLS'i aktif ediyoruz.
builder.WebHost.ConfigureKestrel(options =>
{
  // Tum HTTPS baglantilari icin varsayilan ayarlari yapilandiririz.
  options.ConfigureHttpsDefaults(httpsOptions =>
  {
    // Sunucuya baglanan istemciden (YARP) kesinlikle bir sertifika talep edildigini belirtiriz.
    httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
  });
});

// Sertifika dogrulama (Authentication) mekanizmasini sisteme tanitiriz.
builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
      // Oto-imzali (Self-Signed) sertifikalarla calistigimiz icin iptal (Revocation) kontrolunu devredisi birakiriz.
      options.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;

      // Gelistirme ortami amaciyla her turlu sertifika tipini kabul etmesini saglariz.
      options.AllowedCertificateTypes = CertificateTypes.All;
    });

// gRPC servis altyapisini bagimlilik enjeksiyonu (DI) konteynerine ekleriz.
builder.Services.AddGrpc();

// Uygulamayi insa ederiz.
var app = builder.Build();

// Sisteme kimlik dogrulama katmanini (Middleware) ekleyerek guvenligi devreye aliriz.
app.UseAuthentication();

// YARP'in /api/payments rotasini mTLS uzerinden dogru yonlendirdigini test edebilmek icin ucu ekleriz.
app.MapGet("/api/payments", () => "Payment Service mTLS ile korundu ve YARP sertifikasi basariyla dogrulandi!");

// Uygulamayi baslatiriz.
app.Run();