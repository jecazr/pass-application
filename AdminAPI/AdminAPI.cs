using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Net.Http;
using System.Fabric.Description;
using Commons.Utilities;

namespace AdminAPI
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class AdminAPI : StatelessService
    {

        private static int numberOfRequestWithinThisPeriod = 0;
        private static readonly TimeSpan ReportingIntervalInSeconds = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ScaleIntervalInSeconds = TimeSpan.FromSeconds(60);
        private const string citizenCreationMetricName = "CitizenCreationPerPeriod";

        public AdminAPI(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<HttpClient>(new HttpClient())
                                            .AddSingleton<FabricClient>(new FabricClient())
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }


        protected override async Task RunAsync(CancellationToken ct)
        {

            DefineMetricsAndPlacementConstraints();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                Partition.ReportLoad(new List<LoadMetric> { new LoadMetric(citizenCreationMetricName, numberOfRequestWithinThisPeriod) });

                ServiceEventSource.Current.ServiceMessage(Context, "{0} --- {1} : {2}",
                    Context.InstanceId,
                    citizenCreationMetricName,
                    numberOfRequestWithinThisPeriod);

                numberOfRequestWithinThisPeriod = 0;

                await Task.Delay(ReportingIntervalInSeconds, ct);
            }
        }

        private async void DefineMetricsAndPlacementConstraints()
        {
            FabricClient fabricClient = new FabricClient();
            StatelessServiceUpdateDescription updateDescription = new StatelessServiceUpdateDescription();
            updateDescription.PlacementConstraints = "(ExternalAccess == false)";

            StatelessServiceLoadMetricDescription citizenCreationPerPeriodMetric = new StatelessServiceLoadMetricDescription();
            citizenCreationPerPeriodMetric.Name = citizenCreationMetricName;
            citizenCreationPerPeriodMetric.DefaultLoad = 0;
            citizenCreationPerPeriodMetric.Weight = ServiceLoadMetricWeight.High;

            if (updateDescription.Metrics == null)
            {
                updateDescription.Metrics = new MetricsCollection();
            }
            updateDescription.Metrics.Add(citizenCreationPerPeriodMetric);

            AveragePartitionLoadScalingTrigger trigger = new AveragePartitionLoadScalingTrigger();
            trigger.MetricName = citizenCreationMetricName;
            trigger.ScaleInterval = ScaleIntervalInSeconds;
            trigger.LowerLoadThreshold = 5;
            trigger.UpperLoadThreshold = 20;

            PartitionInstanceCountScaleMechanism mechanism = new PartitionInstanceCountScaleMechanism();
            mechanism.MaxInstanceCount = 3;
            mechanism.MinInstanceCount = 1;
            mechanism.ScaleIncrement = 1;

            ScalingPolicyDescription policy = new ScalingPolicyDescription(mechanism, trigger);
            if (updateDescription.ScalingPolicies == null)
            {
                updateDescription.ScalingPolicies = new List<ScalingPolicyDescription>();
            }
            updateDescription.ScalingPolicies.Add(policy);

            await fabricClient.ServiceManager.UpdateServiceAsync(Utils.GetAdminAPIServiceName(Context), updateDescription);

        }


        public static void RegisterCitizenCreation()
        {
            numberOfRequestWithinThisPeriod++;
        }
    }

}
