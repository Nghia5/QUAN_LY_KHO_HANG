using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IRefreshTokenRepository
{
    /// <summary>Finds any refresh token by its value regardless of revocation state.</summary>
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>Stages revocation of all unrevoked tokens for the user.</summary>
    Task StageRevocationsByUserIdAsync(int userId, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Stages a new refresh token for insert; call SaveChangesAsync to persist.</summary>
    void Add(RefreshToken token);

    /// <summary>Persists all pending changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class RefreshTokenRepository(WmsDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        dbContext.Set<RefreshToken>()
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == token, cancellationToken);

    public async Task StageRevocationsByUserIdAsync(
        int userId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.Revoke(ipAddress);
        }
    }

    public void Add(RefreshToken token) => dbContext.Set<RefreshToken>().Add(token);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
