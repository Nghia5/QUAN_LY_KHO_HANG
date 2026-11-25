using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IUploadedFileRepository
{
    Task<UploadedFile?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(UploadedFile file);
    void Remove(UploadedFile file);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class UploadedFileRepository(WmsDbContext dbContext) : IUploadedFileRepository
{
    public Task<UploadedFile?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UploadedFiles.SingleOrDefaultAsync(file => file.Id == id, cancellationToken);

    public void Add(UploadedFile file) => dbContext.UploadedFiles.Add(file);

    public void Remove(UploadedFile file) => dbContext.UploadedFiles.Remove(file);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
