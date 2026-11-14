using Microsoft.AspNetCore.Identity;

namespace PrintDeck.Infrastructure.Identity;

/// <summary>A PrintDeck login. Role membership (Admin / Viewer) lives in the Identity role tables.</summary>
public sealed class PrintDeckUser : IdentityUser;
