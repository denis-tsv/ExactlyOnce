using System.Diagnostics;
using ExactlyOnce.Commands;
using ExactlyOnce.Configs;
using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using ExactlyOnce.Telemetry;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
            
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            
        var offset = await dbContext.InboxMessageOffsets
            .Where(x => x.AvailableAfter < DateTime.UtcNow)
            .OrderBy(x => x.AvailableAfter)
            .ForUpdateSkipLocked()
            .FirstOrDefaultAsync(cancellationToken);
        if (offset == null) return false;

        var inboxMessage = await dbContext.InboxMessages
            .Where(x => x.Topic == offset.Topic &&
                        x.Partition == offset.Partition &&
                        x.Offset > offset.LastProcessedOffset)
            .OrderBy(x => x.Offset)
            .FirstOrDefaultAsync(cancellationToken);
        if (inboxMessage == null)
        {
            offset.AvailableAfter = DateTime.UtcNow + _options.Value.LockedDelay;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        offset.AvailableAfter = DateTime.UtcNow;
        offset.LastProcessedOffset = inboxMessage.Offset;
        await ProcessMessageAsync(inboxMessage, scope.ServiceProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); //SaveChanges can be missed in ProcessMessageAsync  
        await transaction.CommitAsync(cancellationToken);
        
        return true;
    }

    private async Task ProcessMessageAsync(InboxMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
        using var activity = ActivitySources.ExactlyOnce.StartActivity("Processing", ActivityKind.Internal, context.ActivityContext);

        IRequest command = message.Topic switch
        {
            TopicNames.Topic1 => new Topic1Command(message.Payload),
            TopicNames.Topic2 => new Topic2Command(message.Payload),
            _ => throw new ArgumentOutOfRangeException()
        };

        await serviceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }
}