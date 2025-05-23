using System.Diagnostics;
using ExactlyOnceInbox.Commands;
using ExactlyOnceInbox.Configs;
using ExactlyOnceInbox.Db;
using ExactlyOnceInbox.Entities;
using ExactlyOnceInbox.Telemetry;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry.Context.Propagation;

namespace ExactlyOnceInbox.BackgroundServices;

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

        var offset = await GelOffsetDetachedAsync(dataConnection, cancellationToken);
        if (offset == null) return false;

        var inboxMessage = await GetMessageAsync(dbContext, offset, cancellationToken);
        if (inboxMessage == null)
        {
            await UpdateOffsetAsync(dbContext, 
                id: offset.Id, 
                offset: offset.LastProcessedOffset, // the same value
                availableAfter: DateTime.UtcNow + _options.Value.NoInboxMessagesDelay, 
                cancellationToken);
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
            //EF Core
            catch (DbUpdateException dbDbUpdateException) when(dbDbUpdateException.InnerException is PostgresException {SqlState: "23505", ConstraintName: "pk_processed_inbox_messages"})
            {
                _logger.LogInformation("Message already processed: {IdempotenceKey}", inboxMessage.IdempotenceKey);
            }
            //Linq2db
            catch (PostgresException postgresException) when(postgresException is {SqlState: "23505", ConstraintName: "pk_processed_inbox_messages"})
            {
                _logger.LogInformation("Message already processed: {IdempotenceKey}", inboxMessage.IdempotenceKey);
            }
        }
        else
        {
            _logger.LogInformation("Message already processed: {IdempotenceKey}", inboxMessage.IdempotenceKey);
        }

        await UpdateOffsetAsync(dbContext, 
            id: offset.Id, 
            offset: inboxMessage.Offset, 
            availableAfter: DateTime.UtcNow, 
            cancellationToken);

        return true;
    }

    private static async Task<InboxMessage?> GetMessageAsync(AppDbContext dbContext, InboxMessageOffset offset, CancellationToken cancellationToken)
    {
        var inboxMessage = await dbContext.InboxMessages
            .Where(x => x.Topic == offset.Topic &&
                        x.Partition == offset.Partition &&
                        x.Offset > offset.LastProcessedOffset)
            .OrderBy(x => x.Offset)
            .FirstOrDefaultAsyncEF(cancellationToken);
        return inboxMessage;
    }

    private static async Task UpdateOffsetAsync(AppDbContext dbContext, int id, long offset, DateTime availableAfter, CancellationToken cancellationToken)
    {
        await dbContext.InboxMessageOffsets
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.LastProcessedOffset, offset)
                    .SetProperty(p => p.AvailableAfter, availableAfter),
                cancellationToken);
    }

    private async Task<InboxMessageOffset?> GelOffsetDetachedAsync(DataConnection dataConnection, CancellationToken cancellationToken)
    {
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
        return offsets.FirstOrDefault();
    }

    private async Task ProcessMessageAsync(InboxMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
        using var activity = ActivitySources.ExactlyOnce.StartActivity("Processing", ActivityKind.Internal, context.ActivityContext);

        IRequest command = message.Topic switch
        {
            TopicNames.Topic1 => new NotIdempotentCommand(message.Payload, message.IdempotenceKey),
            TopicNames.Topic2 => new IdempotentCommand(message.Payload, message.IdempotenceKey),
            _ => throw new ArgumentOutOfRangeException()
        };

        await serviceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }
}