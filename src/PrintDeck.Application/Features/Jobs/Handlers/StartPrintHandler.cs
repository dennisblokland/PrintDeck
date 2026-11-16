using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Jobs.Handlers;

public sealed class StartPrintHandler(IPrinterSession session, IGcodeFileStore files, ISettingsStore settings) : ICommandHandler<StartPrintCommand, Result>
{
	public async ValueTask<Result> Handle(StartPrintCommand c, CancellationToken ct)
	{
		GcodeFile? file = await files.GetAsync(c.FileName, ct);
		if (file is null) return Result.Failure($"File {c.FileName} not found.");
		if (file.SizeBytes == 0) return Result.Failure("File is empty.");
		HostSettings s = await settings.GetAsync(ct);

		// No point reading the whole file when the printer will refuse anyway.
		if (!c.IgnoreProblems && session.State == PrinterState.Operational)
		{
			IReadOnlyList<GcodeProblem> problems;
			await using (Stream check = files.OpenRead(file.Name))
				problems = await GcodeFileChecker.CheckAsync(check, s.ActiveProfile, ct);
			if (problems.FirstOrDefault(p => p.Severity == GcodeProblemSeverity.Error) is { } error)
				return Result.Failure($"Not printing {file.Name}: {error.Message}");
		}

		PrintOptions options = new(s.ShowProgressOnPrinter, file.Analysis?.LayerCount);
		return session.StartJob(file.Name, files.OpenRead(file.Name), file.SizeBytes, file.Analysis?.EstimatedPrintTime, options);
	}
}
