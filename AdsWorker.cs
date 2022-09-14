using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataCollectionService
{
    public class AdsWorker : BackgroundService
    {
        private readonly ILogger<AdsWorker> _logger;
        private readonly AdsDataCollection AdsCollector;

        public AdsWorker(ILogger<AdsWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            AdsCollector = new(logger,configuration);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AdsWorker running at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await AdsCollector.Ads(stoppingToken);

                _logger.LogInformation("OnConnection: {}", AdsCollector.OnConnection);

                if (AdsCollector.OnConnection)
                {
                    foreach (KeyValuePair<string, string> kvp in AdsCollector.DataList)
                    {
                        _logger.LogInformation("\nKey: {} , Value: {}", kvp.Key, kvp.Value);
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
