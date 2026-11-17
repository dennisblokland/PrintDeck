using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>The G-code scripts sent around a print, and the snippets they include.</summary>
public sealed record UpdateGcodeScriptsCommand(
	string BeforePrint,
	string AfterPrint,
	string Cancel,
	string Pause,
	string Resume,
	IReadOnlyList<GcodeSnippet> Snippets)
	: ICommand<Result>;
