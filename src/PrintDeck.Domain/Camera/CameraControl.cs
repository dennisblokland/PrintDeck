using PrintDeck.Domain.Camera.Enums;

namespace PrintDeck.Domain.Camera;

/// <summary>A v4l2 camera control (brightness, focus, exposure, ...) as the camera reports it.</summary>
/// <param name="Inactive">The camera ignores it for now, e.g. exposure time while auto exposure is on.</param>
public sealed record CameraControl(
	string Name,
	CameraControlType Type,
	long Min,
	long Max,
	long Step,
	long Default,
	long Value,
	bool Inactive,
	IReadOnlyList<CameraMenuItem> Menu)
{
	/// <summary>"white_balance_temperature" as "White balance temperature".</summary>
	public string Label => Name.Length == 0 ? Name : char.ToUpperInvariant(Name[0]) + Name[1..].Replace('_', ' ');

	/// <summary>Why the camera would refuse <paramref name="value"/>, or null when it fits.</summary>
	public string? Check(long value) => Type switch
	{
		CameraControlType.Boolean when value is not (0 or 1) => $"{Label} is on or off.",
		CameraControlType.Menu when Menu.All(m => m.Value != value) => $"{Label} has no option {value}.",
		CameraControlType.Integer when value < Min || value > Max => $"{Label} must be between {Min} and {Max}.",
		CameraControlType.Integer when Step > 1 && (value - Min) % Step != 0 => $"{Label} goes in steps of {Step} from {Min}.",
		_ => null
	};

	/// <summary>
	/// Orders values so switches and menus (auto exposure, autofocus) go first: the values they unlock,
	/// such as exposure time or focus, are only taken once the automatic mode is off.
	/// </summary>
	public static IReadOnlyList<CameraControlValue> InApplyOrder(IEnumerable<CameraControlValue> values, IReadOnlyList<CameraControl> controls) =>
		values
			.Select(v => (Value: v, Control: controls.FirstOrDefault(c => c.Name == v.Name)))
			.Where(x => x.Control is not null)
			.OrderBy(x => x.Control!.Type == CameraControlType.Integer ? 1 : 0)
			.Select(x => x.Value)
			.ToList();
}
