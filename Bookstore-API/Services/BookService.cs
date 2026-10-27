using Bookstore_API.Database.Entities;
using Bookstore_API.Database.Persistence;
using Bookstore_API.DTOs;
using Bookstore_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace Bookstore_API.Services;

public class BookService(ApplicationDbContext _context) : IBookService
{
    public async Task<PaginationResult<BookDTO>> SearchAsync(string? title, string? author, int page, int pageSize, CancellationToken cancellationToken = default)
    {

        if (page < 1 || pageSize < 1 || pageSize > 100 || ((long)page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page));

        IQueryable<Book> query = _context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var searchTitle = title.Trim();
            query = query.Where(b => b.Title.Contains(searchTitle));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            var searchAuthor = author.Trim();
            query = query.Where(b => b.Author.Name.Contains(searchAuthor));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(b => b.BookId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new BookDTO
            {
                BookId = b.BookId,
                Title = b.Title,
                SubTitle = b.SubTitle,
                Author = new AuthorDTO
                {
                    AuthorId = b.Author.AuthorId,
                    Name = b.Author.Name
                }
            }).ToListAsync(cancellationToken);

        return new PaginationResult<BookDTO>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<BookDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Books.AsNoTracking().Where(b => b.BookId == id)
                .Select(b => new BookDTO
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    SubTitle = b.SubTitle,
                    Author = new AuthorDTO
                    {
                        AuthorId = b.Author.AuthorId,
                        Name = b.Author.Name
                    }
                }).FirstOrDefaultAsync(cancellationToken);
    }
        

    public async Task<BookDTO?> CreateAsync(BookRequestDTO request, CancellationToken cancellationToken = default)
    {
        if (!await _context.Authors.AnyAsync(a => a.AuthorId == request.Author.AuthorId, cancellationToken))
            return null;

        var book = new Book
        {
            AuthorId = request.Author.AuthorId,
            Title = request.Title.Trim(),
            SubTitle = request.SubTitle?.Trim()
        };

        await _context.Books.AddAsync(book);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(book.BookId, cancellationToken);
    }

    public async Task<BookDTO?> UpdateAsync(int id, BookRequestDTO request, CancellationToken cancellationToken = default)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == id, cancellationToken);

        if (book is null) 
            return null;

        if (!await _context.Authors.AnyAsync(a => a.AuthorId == request.Author.AuthorId, cancellationToken))
            throw new ArgumentException("Author does not exist.");

        book.AuthorId = request.Author.AuthorId;
        book.Title = request.Title.Trim();
        book.SubTitle = request.SubTitle?.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == id, cancellationToken);
        if (book is null) 
            return false;

        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

