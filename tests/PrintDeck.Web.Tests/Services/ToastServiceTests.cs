using PrintDeck.Web.Services;
using PrintDeck.Web.Services.Enums;

namespace PrintDeck.Web.Tests.Services;

public class ToastServiceTests
{
	private readonly ToastService _toasts = new();

	[Fact]
	public void Shows_toasts_of_each_kind_in_order()
	{
		_toasts.Info("connecting");
		_toasts.Success("connected");
		_toasts.Error("lost");

		Assert.Equal(
			[(ToastKind.Info, "connecting"), (ToastKind.Success, "connected"), (ToastKind.Error, "lost")],
			_toasts.Items.Select(t => (t.Kind, t.Message)));
		Assert.Equal(3, _toasts.Items.Select(t => t.Id).Distinct().Count());
	}

	[Fact]
	public void Announces_every_change()
	{
		int changes = 0;
		_toasts.Changed += () => changes++;

		_toasts.Info("a");
		_toasts.Dismiss(_toasts.Items[0].Id);

		Assert.Equal(2, changes);
	}

	[Fact]
	public void Dismiss_removes_only_that_toast()
	{
		_toasts.Info("keep");
		_toasts.Info("drop");

		_toasts.Dismiss(_toasts.Items[1].Id);

		Assert.Equal("keep", Assert.Single(_toasts.Items).Message);
	}

	[Fact]
	public void Dismissing_an_unknown_toast_is_harmless()
	{
		_toasts.Info("keep");
		_toasts.Dismiss(Guid.NewGuid());
		Assert.Single(_toasts.Items);
	}

	[Fact]
	public void Items_is_a_snapshot()
	{
		_toasts.Info("a");
		IReadOnlyList<Toast> before = _toasts.Items;
		_toasts.Info("b");
		Assert.Single(before);
	}

	[Fact]
	public async Task Info_disappears_by_itself_and_errors_stay_longer()
	{
		_toasts.Info("short");
		_toasts.Error("long");

		await Task.Delay(TimeSpan.FromSeconds(4.5), TestContext.Current.CancellationToken); // info: 3.5 s, error: 6 s
		Assert.Equal("long", Assert.Single(_toasts.Items).Message);

		await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);
		Assert.Empty(_toasts.Items);
	}
}
