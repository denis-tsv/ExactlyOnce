using ExactlyOnceInbox.Db;
using ExactlyOnceInbox.Entities;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using MediatR;

namespace ExactlyOnceInbox.Commands;

public record NotIdempotentCommand(string Payload, string IdempotenceKey) : IRequest;

public class NotIdempotentCommandHandler : IRequestHandler<NotIdempotentCommand>
{
    private readonly AppDbContext _dbContext;

    public NotIdempotentCommandHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(NotIdempotentCommand request, CancellationToken cancellationToken)
    {
        //change business entities
        // _dbContext.Users.Add(new User { Email = "new_value"});  
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await using var dataConnection = _dbContext.CreateLinqToDBConnection();        
        //insert processed message
        await dataConnection.InsertAsync(new ProcessedInboxMessage {IdempotenceKey = request.IdempotenceKey}, token: cancellationToken);

        //save entities with processed message in the same transaction 
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}