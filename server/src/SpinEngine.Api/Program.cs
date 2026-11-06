using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpinEngine.Api.Middlewares;
using SpinEngine.Application.Commands;
using SpinEngine.Application.Interfaces;
using SpinEngine.Domain.Services;
using SpinEngine.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SpinEngine LiveOps Backend API", Version = "v1" });
});

// Domain Services
builder.Services.AddSingleton<ISlotRngService, SlotRngService>();
builder.Services.AddSingleton<IPaytableEvaluator, PaytableEvaluator>();

// Infrastructure Services
builder.Services.AddSingleton<ISlotConfigProvider, FileSlotConfigProvider>();
builder.Services.AddSingleton<IWalletRepository, InMemoryWalletRepository>();
builder.Services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Application Services
builder.Services.AddScoped<SpinCommandHandler>();

var app = builder.Build();

// Middlewares
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

app.Run();
