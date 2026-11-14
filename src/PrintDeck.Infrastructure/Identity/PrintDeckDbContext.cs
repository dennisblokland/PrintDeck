using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PrintDeck.Infrastructure.Identity;

/// <summary>SQLite database (printdeck.db in the data directory). Holds the ASP.NET Core Identity tables.</summary>
public sealed class PrintDeckDbContext(DbContextOptions<PrintDeckDbContext> options) : IdentityDbContext<PrintDeckUser>(options);
