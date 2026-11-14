using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrintDeck.Infrastructure.Identity;

/// <summary>Used by <c>dotnet ef migrations add</c> only; the app configures the real connection in DependencyInjection.</summary>
internal sealed class PrintDeckDbContextFactory : IDesignTimeDbContextFactory<PrintDeckDbContext>
{
	public PrintDeckDbContext CreateDbContext(string[] args) =>
		new(new DbContextOptionsBuilder<PrintDeckDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
