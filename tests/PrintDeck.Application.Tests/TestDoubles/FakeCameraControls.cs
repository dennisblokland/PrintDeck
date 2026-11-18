using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Camera.Enums;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeCameraControls : ICameraControls
{
	public List<string> Connected { get; } = ["/dev/video0", "VIRTUAL"];

	public List<CameraControl> Controls { get; } =
	[
		new("brightness", CameraControlType.Integer, -64, 64, 1, 0, 10, false, []),
		new("auto_exposure", CameraControlType.Menu, 0, 3, 1, 3, 3, false, [new(1, "Manual Mode"), new(3, "Aperture Priority Mode")]),
		new("exposure_time_absolute", CameraControlType.Integer, 3, 2047, 1, 250, 250, true, []),
	];

	public string? ListError { get; set; }
	public string? SetError { get; set; }
	public List<(string Device, CameraControlValue Value)> Sets { get; } = [];

	public IReadOnlyList<string> Devices() => Connected;

	public Task<Result<IReadOnlyList<CameraControl>>> ListAsync(string device, CancellationToken ct) =>
		Task.FromResult(ListError is null
			? Result.Success<IReadOnlyList<CameraControl>>(Controls.ToList())
			: Result.Failure<IReadOnlyList<CameraControl>>(ListError));

	public Task<Result> SetAsync(string device, IReadOnlyList<CameraControlValue> values, CancellationToken ct)
	{
		if (SetError is not null) return Task.FromResult(Result.Failure(SetError));
		Sets.AddRange(values.Select(v => (device, v)));
		return Task.FromResult(Result.Success());
	}
}
