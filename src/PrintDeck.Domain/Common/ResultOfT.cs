namespace PrintDeck.Domain.Common;

public sealed record Result<T>(T? Value, bool IsSuccess, string? Error) : Result(IsSuccess, Error);
