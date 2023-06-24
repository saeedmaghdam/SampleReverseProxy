using SampleReverseProxy.Client2;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ITunnel, Tunnel>();

var app = builder.Build();

// Configure the HTTP request pipeline.

//app.UseRouting();

app.UseMiddleware<TunnelMiddleware>();

app.Run();