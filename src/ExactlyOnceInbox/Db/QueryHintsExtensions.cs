using Microsoft.EntityFrameworkCore;

namespace ExactlyOnceInbox.Db;

public static class QueryHintsExtensions
{
    public const string HintForUpdate = "Hint: FOR UPDATE";
    public const string HintForUpdateSkipLocked = "Hint: FOR UPDATE SKIP LOCKED";
    
    public static IQueryable<TEntity> ForUpdateSkipLocked<TEntity>(this IQueryable<TEntity> source) =>
        source.TagWith(HintForUpdateSkipLocked);

    public static IQueryable<TEntity> ForUpdate<TEntity>(this IQueryable<TEntity> source) =>
        source.TagWith(HintForUpdate);
}
