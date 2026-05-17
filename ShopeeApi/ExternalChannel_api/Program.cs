var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:8001");

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<ExternalChannel_api.Services.ShopeeOrderFileStore>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UsePathBase("/api");

app.MapGet("/", () => Results.Ok("ExternalChannel API is running"));

app.UseAuthorization();

app.MapControllers();

app.Run();
