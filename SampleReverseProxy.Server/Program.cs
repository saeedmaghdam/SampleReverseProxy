using SampleReverseProxy.Server;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ITunnel, Tunnel>();
        services.AddSingleton<IListener, Listener>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();