using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Behaviors;

/// <summary>
/// Gatekeeper for every command and query. Queries need a signed-in user; commands (anything that
/// changes the printer, files or settings) need an admin unless the message says otherwise
/// (<see cref="IAllowAnonymous"/>, <see cref="IAllowAnyUser"/>, <see cref="IAccountAdministration"/>).
/// A refused command returns a failed <see cref="Result"/>; a refused query throws <see cref="UnauthorizedAccessException"/>.
/// </summary>
public sealed class AuthorizationBehavior<TMessage, TResponse>(ICurrentUser currentUser)
	: IPipelineBehavior<TMessage, TResponse>
	where TMessage : notnull, IMessage
{
	public const string SignInRequired = "Sign in first.";
	public const string AdminRequired = "Only admins can do that.";
	public const string AccountRequired = "Sign in with an admin account to manage users.";

	public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
	{
		if (message is IAllowAnonymous) return await next(message, cancellationToken);

		string? denied = Check(message, await currentUser.GetAsync());
		return denied is null ? await next(message, cancellationToken) : Deny(denied);
	}

	internal static string? Check(TMessage message, CurrentUserInfo? user) => (message, user) switch
	{
		(IAllowAnonymous, _) => null,
		(_, null) => SignInRequired,
		(IAccountAdministration, { IsApiKey: true }) => AccountRequired,
		(IAccountAdministration, { IsAdmin: false }) => AdminRequired,
		(IAllowAnyUser, _) => null,
		(IBaseCommand, { IsAdmin: false }) => AdminRequired,
		_ => null
	};

	private static TResponse Deny(string error)
	{
		Type type = typeof(TResponse);
		if (type == typeof(Result)) return (TResponse)(object)Result.Failure(error);
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
			return (TResponse)Activator.CreateInstance(type, null, false, error)!;
		throw new UnauthorizedAccessException(error);
	}
}
