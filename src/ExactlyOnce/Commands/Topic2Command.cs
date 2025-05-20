using ExactlyOnce.Db;
using ExactlyOnce.Entities;
using MediatR;

namespace ExactlyOnce.Commands;

public record Topic2Command(string Payload) : IRequest;

public class Topic2CommandHandler : IRequestHandler<Topic2Command>
{
    private readonly AppDbContext _dbContext;

    public Topic2CommandHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(Topic2Command request, CancellationToken cancellationToken)
    {
        _dbContext.ProcessedDatas.Add(new ProcessedData
        {
            Data = request.Payload
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}