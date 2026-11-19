using System.Linq.Expressions;
using Laundry.BuildingBlocks.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace Laundry.BuildingBlocks.Infrastructure.Paging;

public static class PagingExtensions
{
    // Keyset paging on (timestamp, id). The condition and the limit run in SQL.
    // The cursor carries only the sort position. It never carries tenant or filter data:
    // every query adds its own scoping (location, status, ...) before calling this method.
    public static async Task<Page<TResult>> ToPageAsync<TEntity, TResult>(
        this IQueryable<TEntity> query,
        PageRequest request,
        Expression<Func<TEntity, DateTimeOffset>> timestamp,
        Expression<Func<TEntity, Guid>> id,
        SortDirection direction,
        Expression<Func<TEntity, TResult>> projection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(projection);

        var entity = Expression.Parameter(typeof(TEntity), "e");
        var timestampBody = Rebind(timestamp, entity);
        var idBody = Rebind(id, entity);

        if (request.After is { } after)
        {
            var keyset = Expression.Lambda<Func<TEntity, bool>>(
                KeysetCondition(timestampBody, idBody, after, direction),
                entity);
            query = query.Where(keyset);
        }

        var ordered = direction == SortDirection.Ascending
            ? query.OrderBy(timestamp).ThenBy(id)
            : query.OrderByDescending(timestamp).ThenByDescending(id);

        var row = Expression.Lambda<Func<TEntity, PageRow<TResult>>>(
            Expression.MemberInit(
                Expression.New(typeof(PageRow<TResult>)),
                Expression.Bind(typeof(PageRow<TResult>).GetProperty(nameof(PageRow<>.Timestamp))!, timestampBody),
                Expression.Bind(typeof(PageRow<TResult>).GetProperty(nameof(PageRow<>.Id))!, idBody),
                Expression.Bind(typeof(PageRow<TResult>).GetProperty(nameof(PageRow<>.Item))!, Rebind(projection, entity))),
            entity);

        var rows = await ordered
            .Select(row)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasNext = rows.Count > request.Limit;
        var page = hasNext ? rows.Take(request.Limit).ToList() : rows;
        var nextCursor = hasNext
            ? new PageCursor(page[^1].Timestamp, page[^1].Id).Encode()
            : null;

        return new Page<TResult>(page.Select(r => r.Item).ToList(), nextCursor);
    }

    // after the cursor: timestamp beyond it, or the same timestamp and an id beyond it.
    private static BinaryExpression KeysetCondition(Expression timestamp, Expression id, PageCursor after, SortDirection direction)
    {
        var ascending = direction == SortDirection.Ascending;
        var timestampValue = Parameterized(after.Timestamp);
        var idValue = Parameterized(after.Id);

        var timestampBeyond = ascending
            ? Expression.GreaterThan(timestamp, timestampValue)
            : Expression.LessThan(timestamp, timestampValue);

        var idCompare = Expression.Call(id, typeof(Guid).GetMethod(nameof(Guid.CompareTo), [typeof(Guid)])!, idValue);
        var idBeyond = ascending
            ? Expression.GreaterThan(idCompare, Expression.Constant(0))
            : Expression.LessThan(idCompare, Expression.Constant(0));

        return Expression.OrElse(
            timestampBeyond,
            Expression.AndAlso(Expression.Equal(timestamp, timestampValue), idBeyond));
    }

    // A field of a captured object, so EF sends the value as a SQL parameter.
    private static MemberExpression Parameterized<T>(T value) =>
        Expression.Field(Expression.Constant(new Box<T>(value)), nameof(Box<>.Value));

    private static Expression Rebind<TIn, TOut>(Expression<Func<TIn, TOut>> lambda, ParameterExpression parameter) =>
        new ParameterReplacer(lambda.Parameters[0], parameter).Visit(lambda.Body);

    private sealed class Box<T>(T value)
    {
        public readonly T Value = value;
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
