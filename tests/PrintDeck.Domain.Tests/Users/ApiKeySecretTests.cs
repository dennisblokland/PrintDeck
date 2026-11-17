using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Domain.Tests.Users;

public class ApiKeySecretTests
{
	[Fact]
	public void New_keys_look_like_the_shared_key_and_differ()
	{
		string key = ApiKeySecret.New();
		Assert.Matches("^[0-9A-F]{32}$", key);
		Assert.NotEqual(key, ApiKeySecret.New());
	}

	[Fact]
	public void The_hash_is_sha256_and_never_the_key()
	{
		Assert.Equal("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08", ApiKeySecret.Hash("test"));
		Assert.NotEqual(ApiKeySecret.Hash("A"), ApiKeySecret.Hash("a"));
	}

	[Theory]
	[InlineData("0123456789ABCDEF", "CDEF")]
	[InlineData("AB", "AB")]
	public void The_hint_is_the_last_four_characters(string key, string hint)
	{
		Assert.Equal(hint, ApiKeySecret.Hint(key));
	}

	[Theory]
	[InlineData("PrusaSlicer", null)]
	[InlineData("  Cura  ", null)]
	[InlineData("", "Give the key a name of up to 64 characters.")]
	[InlineData("   ", "Give the key a name of up to 64 characters.")]
	[InlineData("orcaslicer ", "A key with that name already exists.")]
	public void Names_must_be_short_and_unique(string name, string? problem)
	{
		Assert.Equal(problem, ApiKeySecret.NameProblem(name, ["OrcaSlicer", "Home Assistant"]));
	}

	[Fact]
	public void Names_longer_than_64_characters_are_refused()
	{
		Assert.Null(ApiKeySecret.NameProblem(new string('x', 64), []));
		Assert.NotNull(ApiKeySecret.NameProblem(new string('x', 65), []));
	}

	[Fact]
	public void An_owner_is_shown_as_key_and_account()
	{
		Assert.Equal("OrcaSlicer (ada)", new ApiKeyOwner("OrcaSlicer", "ada", UserRole.Admin).DisplayName);
	}
}
