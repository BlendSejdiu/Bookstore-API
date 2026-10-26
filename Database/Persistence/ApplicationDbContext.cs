using Bookstore_API.Database.Entities;
using Microsoft.EntityFrameworkCore;
namespace Bookstore_API.Database.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
    : DbContext(options)
{

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(a => a.AuthorId);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.BookId);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(100);
            entity.HasOne(b => b.Author).WithMany(a => a.Books).HasForeignKey(b => b.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(b => b.AuthorId);
        });
    }
}
