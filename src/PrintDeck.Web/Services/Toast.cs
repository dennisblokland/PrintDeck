using PrintDeck.Web.Services.Enums;

namespace PrintDeck.Web.Services;

public sealed record Toast(Guid Id, ToastKind Kind, string Message);
