using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using PrintDeck.Domain.Users;

namespace PrintDeck.Infrastructure.Identity;

/// <summary>SQLite database (printdeck.db in the data directory). Holds the ASP.NET Core Identity tables and the app keys.</summary>
public sealed class PrintDeckDbContext(DbContextOptions<PrintDeckDbContext> options) : IdentityDbContext<PrintDeckUser>(options)
{
	public DbSet<PrintDeckApiKey> ApiKeys => Set<PrintDeckApiKey>();

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);
		builder.Entity<PrintDeckApiKey>(k =>
		{
			k.ToTable("ApiKeys");
			k.HasKey(x => x.Id);
			k.Property(x => x.Id).HasMaxLength(32);
			k.Property(x => x.Name).HasMaxLength(ApiKeySecret.MaxNameLength).IsRequired();
			k.Property(x => x.Hash).HasMaxLength(64).IsRequired();
			k.Property(x => x.Hint).HasMaxLength(4).IsRequired();
			k.HasIndex(x => x.Hash).IsUnique();
			k.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
		});
	}
}
