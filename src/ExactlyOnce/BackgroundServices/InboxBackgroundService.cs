using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using ExactlyOnce.Configs;
using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExactlyOnce.BackgroundServices;

public class InboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxBackgroundService> _logger;
    private readonly IOptions<ExactlyOnceConfiguration> _options;

    public InboxBackgroundService(
        IServiceProvider serviceProvider, 
        ILogger<InboxBackgroundService> logger,
        IOptions<ExactlyOnceConfiguration> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tasks = new[]
                {
                    Task.Run(() => ConsumeAsync<string, string>(TopicNames.Topic1, stoppingToken), stoppingToken),
                    Task.Run(() => ConsumeAsync<Null, string>(TopicNames.Topic2, stoppingToken), stoppingToken),
                };

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Something wrong");
            }
        }
    }

    private async Task ConsumeAsync<TKey, TValue>(string topic, CancellationToken cancellationToken)
    {
        var consumer = _serviceProvider.GetRequiredService<IConsumer<TKey, TValue>>();
        consumer.Subscribe(topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var results = new List<ConsumeResult<TKey, TValue>>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.Value.NoKafkaMessagesDelay);
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(cts.Token);
            
                    results.Add(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                
                if (results.Count == _options.Value.BatchSize) break;
            }
        
            if (results.Any()) await ProcessAsync(results, cancellationToken);
        }
    }

    private async Task ProcessAsync<TKey, TValue>(ICollection<ConsumeResult<TKey, TValue>> consumeResults, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var dataConnection = dbContext.CreateLinqToDBConnection();

        var messages = consumeResults.Select(consumeResult =>
        {
            var headers = consumeResult.Message.Headers.ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.GetValueBytes()));
            return new InboxMessage
            {
                Topic = consumeResult.Topic,
                Partition = consumeResult.Partition.Value,
                Offset = consumeResult.Offset.Value,
                CreatedAt = consumeResult.Message.Timestamp.UtcDateTime,
                IdempotenceKey = headers[HeaderNames.IdempotenceKey],
                Payload = JsonSerializer.Serialize(consumeResult.Message.Value),
                Headers = headers
            };
        }).ToList();

        await dataConnection
            .GetTable<InboxMessage>()
            .Merge()
            .Using(messages)
            .On(entity => entity.IdempotenceKey, dto => dto.IdempotenceKey)
            .InsertWhenNotMatched()
            .MergeAsync(cancellationToken);
    }
}