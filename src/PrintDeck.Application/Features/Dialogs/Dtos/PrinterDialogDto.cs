using PrintDeck.Domain.Printer;

namespace PrintDeck.Application.Features.Dialogs.Dtos;

public sealed record PrinterDialogDto(HostPrompt? Prompt, PrinterNotification? Notification, bool WaitingForUser);
