using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using MediatR;

namespace ExactlyOnce.Commands;

public record Topic1Command(string Payload) : IRequest;

public class Topic1CommandHandler : IRequestHandler<Topic1Command>
{
    private readonly AppDbContext _dbContext;

    public Topic1CommandHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(Topic1Command request, CancellationToken cancellationToken)
    {
        _dbContext.ProcessedDatas.Add(new ProcessedData
        {
            Data = request.Payload
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}