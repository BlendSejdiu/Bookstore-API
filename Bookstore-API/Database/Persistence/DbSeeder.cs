using Bookstore_API.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookstore_API.Database.Persistence;

public static class DbSeeder
{
    public static void Seed(ApplicationDbContext context)
    {
        if (context.Authors.Any())
            return;

        context.Authors.AddRange
        (
            new Author { Name = "George Orwell" },
            new Author { Name = "Jane Austen" },
            new Author { Name = "J. R. R. Tolkien" },
            new Author { Name = "Agatha Christie" },
            new Author { Name = "Stephen King" }
        );

        context.SaveChanges();
    }

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Authors.AnyAsync(cancellationToken))
            return;
        
        context.Authors.AddRange(
                new Author { Name = "George Orwell" },
                new Author { Name = "Jane Austen" },
                new Author { Name = "J. R. R. Tolkien" },
                new Author { Name = "Agatha Christie" },
                new Author { Name = "Stephen King" }
            );

        await context.SaveChangesAsync(cancellationToken);
    }
}