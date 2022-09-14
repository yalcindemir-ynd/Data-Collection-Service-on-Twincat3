using DataCollectionService;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<AdsWorker>();
    })
    .Build();

await host.RunAsync();
