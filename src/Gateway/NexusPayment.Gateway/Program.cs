// src/Gateway/NexusPayment.Gateway/Program.cs

// Web uygulamasi olusturmak icin gerekli builder nesnesini baslatiyoruz.
var builder = WebApplication.CreateBuilder(args);

// YARP servislerini bagimlilik enjeksiyonu (DI) konteynerine ekliyoruz.
builder.Services.AddReverseProxy()
    // YARP'in ayarlarini appsettings.json icindeki "ReverseProxy" dugumunden okumasini soyluyoruz.
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Uygulamayi insa edip calistirilabilir (runnable) hale getiriyoruz.
var app = builder.Build();

// YARP ara yazilimini (Middleware) HTTP istek hattina (pipeline) ekliyoruz.
// Bu satir, gelen isteklerin yakalanip appsettings'deki kurallara gore yonlendirilmesini saglar.
app.MapReverseProxy();

// Uygulamayi baslatiyor ve gelen HTTP isteklerini dinlemeye basliyoruz.
app.Run();