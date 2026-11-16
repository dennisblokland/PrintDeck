using Microsoft.AspNetCore.Identity;

namespace PrintDeck.Infrastructure.Identity;

public static class IdentityRules
{
	/// <summary>Password and lockout rules for PrintDeck accounts: length over composition (NIST SP 800-63B), lockout after repeated wrong passwords.</summary>
	public static void Apply(IdentityOptions o)
	{
		o.Password.RequiredLength = 8;
		o.Password.RequireDigit = false;
		o.Password.RequireLowercase = false;
		o.Password.RequireUppercase = false;
		o.Password.RequireNonAlphanumeric = false;
		o.Password.RequiredUniqueChars = 1;
		o.Lockout.MaxFailedAccessAttempts = 5;
		o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
		o.Lockout.AllowedForNewUsers = true;
		o.User.RequireUniqueEmail = false;
	}
}
