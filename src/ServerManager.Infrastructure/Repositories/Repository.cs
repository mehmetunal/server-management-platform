using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(predicate).ToListAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate is null
            ? _dbSet.CountAsync(cancellationToken)
            : _dbSet.CountAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _dbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity) =>
        _dbSet.Update(entity);

    public void Remove(T entity) =>
        _dbSet.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
