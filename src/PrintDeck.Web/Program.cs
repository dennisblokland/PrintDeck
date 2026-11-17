using Mediator;

using Microsoft.AspNetCore.Http.Features;

using PrintDeck.Application;
using PrintDeck.Application.Behaviors;
using PrintDeck.Infrastructure;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Web.Api;
using PrintDeck.Web.Auth;
using PrintDeck.Web.Components;
using PrintDeck.Web.Hosting;
using PrintDeck.Web.Services;
using PrintDeck.Web.Services.Enums;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

const long MaxUploadBytes = 1L << 30; // 1 GiB

builder.Services.AddApplicationCore();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(options =>
{
	options.ServiceLifetime = ServiceLifetime.Scoped;
	options.Assemblies = [typeof(PrintDeck.Application.DependencyInjection)];
	options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(AuthorizationBehavior<,>)];
});

builder.Services.AddPrintDeckReverseProxy();
builder.Services.AddPrintDeckAuthentication();
builder.Services.AddScoped<ToastService>();
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxUploadBytes);

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

WebApplication app = builder.Build();

await app.Services.InitializeDatabaseAsync();
await app.Services.InitializeSerialLogAsync();
app.Services.StartPrintHistory();

app.UsePrintDeckForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapSlicerApi();
app.MapToolpathApi();
app.MapLogsApi();
app.MapBackupApi();
app.MapAccountEndpoints();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
