using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using MediatR;

namespace ExactlyOnce.Commands;

public record IdempotentCommand(object Payload, string IdempotenceKey) : IRequest;

public class IdempotentCommandHandler : IRequestHandler<IdempotentCommand>
{
    private readonly AppDbContext _dbContext;

    public IdempotentCommandHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(IdempotentCommand request, CancellationToken cancellationToken)
    {
        //check business rules like
        // if (_dbContext.Users.Any(x => x.Email == "new_value")) log duplicate and return
        
        //change business entities
        // _dbContext.Users.Add(new User { Email = "new_value"});       
        
        //add processed message
        _dbContext.ProcessedInboxMessages.Add(new ProcessedMessage
        {
            IdempotenceKey = request.IdempotenceKey
        });

        //save entities with processed message in the same transaction 
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}