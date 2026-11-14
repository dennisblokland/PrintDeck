using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Behaviors;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Behaviors;

public class LoggingBehaviorTests
{
	public sealed record Ping : ICommand<Result>;

	private readonly ListLogger _logger = new();
	private LoggingBehavior<Ping, Result> Behavior => new(_logger);
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Passes_the_response_through_and_logs_at_debug()
	{
		Result response = await Behavior.Handle(new Ping(), (_, _) => ValueTask.FromResult(Result.Success()), Ct);

		Assert.True(response.IsSuccess);
		(LogLevel level, string message) = Assert.Single(_logger.Entries);
		Assert.Equal(LogLevel.Debug, level);
		Assert.StartsWith("Ping handled in", message);
	}

	[Fact]
	public async Task Logs_rejected_results_with_their_error()
	{
		Result response = await Behavior.Handle(new Ping(), (_, _) => ValueTask.FromResult(Result.Failure("Printer is not connected.")), Ct);

		Assert.False(response.IsSuccess);
		Assert.Equal((LogLevel.Information, "Ping rejected: Printer is not connected."), Assert.Single(_logger.Entries));
	}

	[Fact]
	public async Task Logs_and_rethrows_exceptions()
	{
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await Behavior.Handle(new Ping(), (_, _) => throw new InvalidOperationException("boom"), Ct));

		Assert.Equal((LogLevel.Error, "Ping threw"), Assert.Single(_logger.Entries));
	}

	private sealed class ListLogger : ILogger<LoggingBehavior<Ping, Result>>
	{
		public List<(LogLevel Level, string Message)> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Entries.Add((logLevel, formatter(state, exception)));
	}
}
