using PrintDeck.Web.Services.Enums;

namespace PrintDeck.Web.Services;

/// <summary>Per-circuit notifications.</summary>
public sealed class ToastService
{
	private readonly List<Toast> _items = [];
	public event Action? Changed;
	public IReadOnlyList<Toast> Items
	{
		get { lock (_items) return _items.ToList(); }
	}

	public void Info(string message) => Show(ToastKind.Info, message);
	public void Success(string message) => Show(ToastKind.Success, message);
	public void Error(string message) => Show(ToastKind.Error, message);

	public void Dismiss(Guid id)
	{
		lock (_items) _items.RemoveAll(t => t.Id == id);
		Changed?.Invoke();
	}

	private void Show(ToastKind kind, string message)
	{
		Toast toast = new(Guid.NewGuid(), kind, message);
		lock (_items) _items.Add(toast);
		Changed?.Invoke();
		_ = Task.Delay(kind == ToastKind.Error ? 6000 : 3500).ContinueWith(_ => Dismiss(toast.Id));
	}
}
