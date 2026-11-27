using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;
using WMS.Api.Models.Documents;

namespace WMS.Api.Repositories;

public interface IUploadedFileRepository
{
    Task<UploadedFile?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<List<UploadedFile>> ListForDocumentAsync(WarehouseDocumentType type, int id, CancellationToken cancellationToken);
    Task<List<UploadedFile>> ListAllAsync(CancellationToken cancellationToken);
    void Add(UploadedFile file);
    void Remove(UploadedFile file);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class UploadedFileRepository(WmsDbContext dbContext) : IUploadedFileRepository
{
    public Task<UploadedFile?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UploadedFiles.SingleOrDefaultAsync(file => file.Id == id, cancellationToken);

    public Task<List<UploadedFile>> ListForDocumentAsync(WarehouseDocumentType type, int id, CancellationToken cancellationToken) =>
        dbContext.UploadedFiles.AsNoTracking()
            .Where(file => file.DocumentType == type && file.DocumentId == id)
            .OrderBy(file => file.UploadedAt).ThenBy(file => file.Id)
            .ToListAsync(cancellationToken);

    public Task<List<UploadedFile>> ListAllAsync(CancellationToken cancellationToken) =>
        dbContext.UploadedFiles.AsNoTracking()
            .OrderByDescending(file => file.UploadedAt)
            .ToListAsync(cancellationToken);

    public void Add(UploadedFile file) => dbContext.UploadedFiles.Add(file);

    public void Remove(UploadedFile file) => dbContext.UploadedFiles.Remove(file);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
