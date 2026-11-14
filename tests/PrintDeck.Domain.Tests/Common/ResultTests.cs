using PrintDeck.Domain.Common;

namespace PrintDeck.Domain.Tests.Common;

public class ResultTests
{
	[Fact]
	public void Success_has_no_error()
	{
		Result r = Result.Success();
		Assert.True(r.IsSuccess);
		Assert.Null(r.Error);
	}

	[Fact]
	public void Failure_carries_the_error()
	{
		Result r = Result.Failure("boom");
		Assert.False(r.IsSuccess);
		Assert.Equal("boom", r.Error);
	}

	[Fact]
	public void Generic_success_carries_the_value()
	{
		Result<int> r = Result.Success(42);
		Assert.True(r.IsSuccess);
		Assert.Equal(42, r.Value);
		Assert.Null(r.Error);
	}

	[Fact]
	public void Generic_failure_has_default_value()
	{
		Result<string> r = Result.Failure<string>("nope");
		Assert.False(r.IsSuccess);
		Assert.Null(r.Value);
		Assert.Equal("nope", r.Error);
	}
}
