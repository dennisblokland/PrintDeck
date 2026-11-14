using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Behaviors;

/// <summary>Logs every command/query, its duration, and failed Results.</summary>
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
	: IPipelineBehavior<TMessage, TResponse>
	where TMessage : notnull, IMessage
{
	public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
	{
		long started = Stopwatch.GetTimestamp();
		try
		{
			TResponse? response = await next(message, cancellationToken);
			if (response is Result { IsSuccess: false } failed)
				logger.LogInformation("{Message} rejected: {Error}", typeof(TMessage).Name, failed.Error);
			else
				logger.LogDebug("{Message} handled in {Ms:0.0} ms", typeof(TMessage).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
			return response;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "{Message} threw", typeof(TMessage).Name);
			throw;
		}
	}
}
