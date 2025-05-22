using System.Diagnostics;
using ExactlyOnce.Commands;
using ExactlyOnce.Configs;
using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using ExactlyOnce.Telemetry;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry.Context.Propagation;

namespace ExactlyOnce.BackgroundServices;

public class ExactlyOnceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<ExactlyOnceConfiguration> _options;
    private readonly ILogger<ExactlyOnceBackgroundService> _logger;

    public ExactlyOnceBackgroundService(
         IServiceProvider serviceProvider,
         IOptions<ExactlyOnceConfiguration> options,
         ILogger<ExactlyOnceBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dataFound = await ProcessAsync(stoppingToken);
                if (!dataFound) await Task.Delay(_options.Value.NoInboxMessagesDelay, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error");                
            }
        }
    }

    private async Task<bool> ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var dataConnection = dbContext.CreateLinqToDBConnection();

        var offsets = await dataConnection.GetTable<InboxMessageOffset>()
            .Where(x => x.AvailableAfter < DateTime.UtcNow)
            .OrderBy(x => x.AvailableAfter)
            .Take(1)
            .SubQueryHint(PostgreSQLHints.ForUpdate)
            .SubQueryHint(PostgreSQLHints.SkipLocked)
            .AsSubQuery()
            .UpdateWithOutput(x => x,
                x => new InboxMessageOffset
                {
                    AvailableAfter = x.AvailableAfter + _options.Value.LockedDelay
                },
                (_, _, inserted) => inserted)
            .AsQueryable()
            .ToArrayAsyncLinqToDB(cancellationToken);
        var offset = offsets.FirstOrDefault();
        if (offset == null) return false;

        var inboxMessage = await dbContext.InboxMessages
            .Where(x => x.Topic == offset.Topic &&
                        x.Partition == offset.Partition &&
                        x.Offset > offset.LastProcessedOffset)
            .OrderBy(x => x.Offset)
            .FirstOrDefaultAsyncEF(cancellationToken);
        if (inboxMessage == null)
        {
            offset.AvailableAfter = DateTime.UtcNow + _options.Value.NoInboxMessagesDelay;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        bool messageProcessed = await dbContext.ProcessedInboxMessages
            .AnyAsyncEF(x => x.IdempotenceKey == inboxMessage.IdempotenceKey, cancellationToken);
        if (!messageProcessed)
        {
            try
            {
                await ProcessMessageAsync(inboxMessage, scope.ServiceProvider, cancellationToken);
            }
            catch (DbUpdateException dpDbUpdateException) when(dpDbUpdateException.InnerException is PostgresException {SqlState: "23505", ConstraintName: "pk_processed_inbox_messages"})
            {
                _logger.LogInformation("Message already processed: {IdempotenceKey}", inboxMessage.IdempotenceKey);
            }
        }
        else
        {
            _logger.LogInformation("Message already processed: {IdempotenceKey}", inboxMessage.IdempotenceKey);
        }

        await dbContext.InboxMessageOffsets
            .Where(x => x.Id == offset.Id)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.LastProcessedOffset, inboxMessage.Offset)
                    .SetProperty(p => p.AvailableAfter, DateTime.UtcNow),
                cancellationToken);

        return true;
    }

    private async Task ProcessMessageAsync(InboxMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
        using var activity = ActivitySources.ExactlyOnce.StartActivity("Processing", ActivityKind.Internal, context.ActivityContext);

        IRequest command = message.Topic switch
        {
            TopicNames.Topic1 => new Topic1Command(message.Payload, message.IdempotenceKey),
            TopicNames.Topic2 => new Topic2Command(message.Payload, message.IdempotenceKey),
            _ => throw new ArgumentOutOfRangeException()
        };

        await serviceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }
}