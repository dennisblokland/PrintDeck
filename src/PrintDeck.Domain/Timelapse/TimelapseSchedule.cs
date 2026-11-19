using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Domain.Timelapse;

/// <summary>
/// Decides when the next timelapse frame is due during one print. Timed: the first frame right away, then one every
/// interval of printing (the clock stops while paused). Per layer: one frame when each new layer starts.
/// </summary>
public sealed class TimelapseSchedule(TimelapseMode mode, TimeSpan interval)
{
	private TimeSpan _printingSinceFrame;
	private DateTimeOffset? _lastTick;
	private bool _first = true;
	private int _lastLayer;

	public TimelapseMode Mode => mode;

	/// <summary>Called regularly while the print runs; true when a timed frame is due. Paused time doesn't count.</summary>
	public bool Tick(DateTimeOffset now, bool printing)
	{
		if (mode != TimelapseMode.Timed) return false;
		TimeSpan step = _lastTick is { } last && now > last ? now - last : TimeSpan.Zero;
		_lastTick = now;
		if (!printing) return false;

		_printingSinceFrame += step;
		if (!_first && _printingSinceFrame < interval) return false;
		_first = false;
		_printingSinceFrame = TimeSpan.Zero;
		return true;
	}

	/// <summary>Called when the printed layer changes; true when this layer gets a frame.</summary>
	public bool OnLayer(int layer)
	{
		if (mode != TimelapseMode.Layer || layer <= _lastLayer) return false;
		_lastLayer = layer;
		return true;
	}
}
