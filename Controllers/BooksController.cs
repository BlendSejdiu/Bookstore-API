using Bookstore_API.DTOs;
using Bookstore_API.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BooksController(IBookService bookService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginationResult<BookDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginationResult<BookDTO>>> SearchBooks([FromQuery] string? title, [FromQuery] string? author, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new { message = "Page must be at least 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page size must be between 1 and 100." });

        if (((long)page - 1) * pageSize > int.MaxValue)
            return BadRequest(new { message = "The requested page is too large." });

        var result = await bookService.SearchAsync(title, author, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDTO>> GetBook(int id, CancellationToken cancellationToken)
    {
        var book = await bookService.GetByIdAsync(id, cancellationToken);

        if (book is null)
            return NotFound(new { message = "Book not found." });

        return Ok(book);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookDTO>> CreateBook([FromBody] BookDTO request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });

        if (request.Author is null || request.Author.AuthorId < 1)
            return BadRequest(new { message = "A valid author ID is required." });

        var book = await bookService.CreateAsync(request, cancellationToken);

        if (book is null)
            return BadRequest(new { message = "The specified author does not exist." });

        return CreatedAtAction(nameof(GetBook), new { id = book.BookId }, book);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BookDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDTO>> UpdateBook(int id, [FromBody] BookDTO request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });

        if (request.Author is null || request.Author.AuthorId < 1)
            return BadRequest(new { message = "A valid author ID is required." });

        try
        {
            var book = await bookService.UpdateAsync(id, request, cancellationToken);

            if (book is null)
                return NotFound(new { message = "Book not found." });

            return Ok(book);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "The specified author does not exist." });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBook(int id, CancellationToken cancellationToken)
    {
        var deleted = await bookService.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound(new { message = "Book not found." });

        return NoContent();
    }
}