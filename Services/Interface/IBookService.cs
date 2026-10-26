using Bookstore_API.DTOs;

namespace Bookstore_API.Services.Interface;

public interface IBookService
{
    Task<PaginationResult<BookDTO>> SearchAsync(string? title, string? author, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<BookDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BookDTO?> CreateAsync(BookDTO request, CancellationToken cancellationToken = default);
    Task<BookDTO?> UpdateAsync(int id, BookDTO request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

