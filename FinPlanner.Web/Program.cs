using FinPlanner.Web;
using FinPlanner.Engine;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register the Accounts service as Scoped.  Scoped will create a new instance for each user session.
builder.Services.AddScoped<Scenario>();
builder.Services.AddScoped<PlanBuilder>();
builder.Services.AddScoped<MaximumExpenseCalculator>();

await builder.Build().RunAsync();
