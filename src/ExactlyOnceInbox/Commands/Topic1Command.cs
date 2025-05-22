using ExactlyOnceInbox.Db;
using ExactlyOnceInbox.Entities;
using MediatR;

namespace ExactlyOnceInbox.Commands;

public record Topic1Command(string Payload, string IdempotenceKey) : IRequest;

public class Topic1CommandHandler : IRequestHandler<Topic1Command>
{
    private readonly AppDbContext _dbContext;

    public Topic1CommandHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(Topic1Command request, CancellationToken cancellationToken)
    {
        //change business entities
        // ...
        
        //add processed message
        _dbContext.ProcessedInboxMessages.Add(new ProcessedInboxMessage
        {
            IdempotenceKey = request.IdempotenceKey
        });

        //save entities with processed message in the same transaction 
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}