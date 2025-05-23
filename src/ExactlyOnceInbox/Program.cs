using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using ExactlyOnceInbox.BackgroundServices;
using ExactlyOnceInbox.Commands;
using ExactlyOnceInbox.Configs;
using ExactlyOnceInbox.Db;
using ExactlyOnceInbox.Telemetry;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddKafkaClient();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder.AddSource(ActivitySources.ExactlyOnceSourceName));
builder.Services.AddDbContextPool<AppDbContext>(optionsBuilder =>
{
    var dataSource = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("ExactlyOnce"))
        .EnableDynamicJson()
        .Build();
    optionsBuilder
        .UseNpgsql(
            dataSource,
            options => options.MigrationsHistoryTable("_migrations", "exactly_once"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new ForUpdateInterceptor());
});
LinqToDBForEFTools.Implementation = new CustomLinqToDBForEFToolsImpl(builder.Configuration.GetConnectionString("ExactlyOnce")!);
LinqToDBForEFTools.Initialize();

builder.Services.AddHostedService<InboxBackgroundService>();
builder.Services.AddHostedService<ExactlyOnceBackgroundService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<NotIdempotentCommand>());

builder.Services.Configure<ExactlyOnceConfiguration>(builder.Configuration.GetSection("ExactlyOnce"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.MapPost("/messages", async (MessageDto[] messages, IServiceProvider sp, CancellationToken ct) =>
    {
        Task ProduceAsync<TKey>(TKey key, MessageDto dto)
        {
            var activityContext = Activity.Current?.Context;
            var headers = activityContext.GetHeaders();
            headers.Add(HeaderNames.IdempotenceKey, dto.IdempotenceKey);
            var producer = sp.GetRequiredService<IProducer<TKey, string>>();
            var message = new Message<TKey, string>
            {
                Key = key,
                Value = dto.Payload,
                Headers = new()
            };
            foreach (var header in headers)
                message.Headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
            return producer.ProduceAsync(dto.Topic, message, ct);
        }

        var tasks = messages.Select(x =>
        {
            if (x.Key == null) return ProduceAsync<Null>(null!, x);
            return ProduceAsync(x.Key, x);
        }).ToArray();

        await Task.WhenAll(tasks);

    })
    .WithName("Create");

app.Run();

public record MessageDto(
    string Topic,
    string? Key,
    string IdempotenceKey,
    string Payload
    );