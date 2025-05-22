using ExactlyOnceInbox.Db;
using ExactlyOnceInbox.Entities;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
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
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await using var dataConnection = _dbContext.CreateLinqToDBConnection();        
        //insert processed message
        await dataConnection.InsertAsync(new ProcessedInboxMessage {IdempotenceKey = request.IdempotenceKey}, token: cancellationToken);

        //save entities with processed message in the same transaction 
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}