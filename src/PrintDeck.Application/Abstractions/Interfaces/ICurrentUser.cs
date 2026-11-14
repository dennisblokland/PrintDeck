namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Who is signed in for the current request or Blazor circuit; null when nobody is.</summary>
public interface ICurrentUser
{
	ValueTask<CurrentUserInfo?> GetAsync();
}
