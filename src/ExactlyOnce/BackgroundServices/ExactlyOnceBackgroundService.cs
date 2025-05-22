using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using ExactlyOnce.Commands;
using ExactlyOnce.Configs;
using ExactlyOnce.Db;
using ExactlyOnce.Telemetry;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Context.Propagation;

namespace ExactlyOnce.BackgroundServices;

public class ExactlyOnceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExactlyOnceBackgroundService> _logger;

    public ExactlyOnceBackgroundService(
        IServiceProvider serviceProvider, 
        ILogger<ExactlyOnceBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var tasks = new[]
            {
                Task.Run(() => ConsumeAsync<string, string>(TopicNames.Topic1, stoppingToken), stoppingToken),
                Task.Run(() => ConsumeAsync<Null, string>(TopicNames.Topic2, stoppingToken), stoppingToken),
            };

            await Task.WhenAll(tasks);
        }
    }

    private async Task ConsumeAsync<TKey, TValue>(string topic, CancellationToken cancellationToken)
    {
        var consumer = _serviceProvider.GetRequiredService<IConsumer<TKey, TValue>>();
        consumer.Subscribe(topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = consumer.Consume(cancellationToken);
            if (result == null || result.IsPartitionEOF) continue;

            try
            {
                await ProcessAsync(result, cancellationToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error");
                consumer.Seek(result.TopicPartitionOffset);
            }
        }
    }

    private async Task ProcessAsync<TKey, TValue>(ConsumeResult<TKey, TValue> consumeResult, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var headers = consumeResult.Message.Headers.ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.GetValueBytes()));
        var idempotenceKey = headers[HeaderNames.IdempotenceKey];

        var messageProcessed = await dbContext.ProcessedInboxMessages
            .AnyAsync(x => x.IdempotenceKey == idempotenceKey, cancellationToken);
        if (messageProcessed)
        {
            _logger.LogInformation("Message already processed: {IdempotenceKey}", idempotenceKey);
            return;
        }

        try
        {
            var context = Propagators.DefaultTextMapPropagator.Extract(default, headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
            using var activity = ActivitySources.ExactlyOnce.StartActivity("Processing", ActivityKind.Internal, context.ActivityContext);

            await ProcessMessageAsync(consumeResult, idempotenceKey, scope.ServiceProvider, cancellationToken);
        }
        catch (DbUpdateException dpDbUpdateException) when(dpDbUpdateException.InnerException is PostgresException {SqlState: "23505", ConstraintName: "pk_processed_inbox_messages"})
        {
            _logger.LogInformation("Message already processed: {IdempotenceKey}", idempotenceKey);
        }
    }
    
    private async Task ProcessMessageAsync<TKey, TValue>(ConsumeResult<TKey, TValue> message, string idempotenceKey, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        IRequest command = message.Topic switch
        {
            TopicNames.Topic1 => new Topic1Command(message.Message.Value, idempotenceKey),
            TopicNames.Topic2 => new Topic2Command(message.Message.Value, idempotenceKey),
            _ => throw new ArgumentOutOfRangeException()
        };

        await serviceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }
}