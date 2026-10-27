namespace Bookstore_API.DTOs;
public class BookDTO
{
    public int BookId { get; set; }
    public AuthorDTO Author { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? SubTitle { get; set; }
}
