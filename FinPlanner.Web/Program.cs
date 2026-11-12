using FinPlanner.Web;
using FinPlanner.Engine;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Keep editable inputs and calculations scoped to this browser session.
builder.Services.AddScoped(_ => new Scenario { LifeExpectancy = 100 });
builder.Services.AddScoped<PlanCalculator>();

await builder.Build().RunAsync();
