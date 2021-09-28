using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using System.Net.Http;
using Commons.Utilities;
using Newtonsoft.Json;
using System.Fabric.Description;

namespace ApplicationApprover
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class ApplicationApprover : StatelessService
    {

        private static readonly string ApplicationApprovalRateMetricName = "ApplicationApprovalRate";
        private static readonly TimeSpan ReportingIntervalInSeconds = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ScaleIntervalInSeconds = TimeSpan.FromSeconds(60);
        private static int numberOfApprovalWithinThisPeriod = 0;
        private static int maxNumberOfApprovalPerIteration = 21;

        public ApplicationApprover(StatelessServiceContext context) : base(context)
        {
        }

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
            FabricClient fabricClient = new FabricClient();
            HttpClient httpClient = new HttpClient();

            DefineMetrics(fabricClient);


            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // fetch all applications in "created" state
                Uri proxyAddress = Utils.GetApplicationDataProxyAddress(this.Context);

                System.Fabric.Query.ServicePartitionList partitionList = await fabricClient.QueryManager.GetPartitionListAsync(Utils.GetApplicationDataServiceName(this.Context));

                List<KeyValuePair<int, Commons.Models.Application>> resultList = new List<KeyValuePair<int, Commons.Models.Application>>();

                foreach (System.Fabric.Query.Partition p in partitionList)
                {
                    string proxyUrl = $"{proxyAddress}/api/ApplicationData/search?state=Created&PartitionKey={((Int64RangePartitionInformation)p.PartitionInformation).LowKey}&PartitionKind=Int64Range";

                    using (HttpResponseMessage response = await httpClient.GetAsync(proxyUrl))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            continue;
                            //TODO throw exception
                        }

                        resultList.AddRange(JsonConvert.DeserializeObject<List<KeyValuePair<int, Commons.Models.Application>>>(await response.Content.ReadAsStringAsync()));
                    }
                }

                int numberOfApproval = resultList.Count > maxNumberOfApprovalPerIteration ? maxNumberOfApprovalPerIteration : resultList.Count;

                RegisterAprovalNumber(numberOfApproval);
                int approved = 0;

                // approve x applications
                foreach (KeyValuePair<int, Commons.Models.Application> pair in resultList)
                {
                    long partitionKey = Utils.GetPartitionKey(pair.Value.CitizenId);
                    string proxyUrl = $"{proxyAddress}/api/ApplicationData/changeState?ApplicationId={pair.Key}&NewState=Approved&PartitionKey={partitionKey}&PartitionKind=Int64Range";

                    using (HttpResponseMessage response = await httpClient.PutAsync(proxyUrl, null))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            continue;
                            //TODO throw exception
                        }
                    }
                    approved++;
                    if (approved == numberOfApproval)
                    {
                        break;
                    }
                }


                // reportLoad and initiate autoscaling
                Partition.ReportLoad(new List<LoadMetric> { new LoadMetric(ApplicationApprovalRateMetricName, numberOfApprovalWithinThisPeriod) });

                ServiceEventSource.Current.ServiceMessage(Context, "{0} --- {1} : {2}",
                    Context.InstanceId,
                    ApplicationApprovalRateMetricName,
                    numberOfApprovalWithinThisPeriod);

                numberOfApprovalWithinThisPeriod = 0;

                // sleep
                await Task.Delay(ReportingIntervalInSeconds, ct);


            }
        }



        private void DefineMetrics(FabricClient fabricClient)
        {
            StatelessServiceLoadMetricDescription citizenCreationPerPeriodMetric = new StatelessServiceLoadMetricDescription();
            citizenCreationPerPeriodMetric.Name = ApplicationApprovalRateMetricName;
            citizenCreationPerPeriodMetric.DefaultLoad = 0;
            citizenCreationPerPeriodMetric.Weight = ServiceLoadMetricWeight.High;

            StatelessServiceUpdateDescription updateDescription = new StatelessServiceUpdateDescription();
            if (updateDescription.Metrics == null)
            {
                updateDescription.Metrics = new MetricsCollection();
            }
            updateDescription.Metrics.Add(citizenCreationPerPeriodMetric);

            AveragePartitionLoadScalingTrigger trigger = new AveragePartitionLoadScalingTrigger();
            trigger.MetricName = ApplicationApprovalRateMetricName;
            trigger.ScaleInterval = ScaleIntervalInSeconds;
            trigger.LowerLoadThreshold = 5;
            trigger.UpperLoadThreshold = 20;

            PartitionInstanceCountScaleMechanism mechanism = new PartitionInstanceCountScaleMechanism();
            mechanism.MaxInstanceCount = 4;
            mechanism.MinInstanceCount = 1;
            mechanism.ScaleIncrement = 1;

            ScalingPolicyDescription policy = new ScalingPolicyDescription(mechanism, trigger);
            if (updateDescription.ScalingPolicies == null)
            {
                updateDescription.ScalingPolicies = new List<ScalingPolicyDescription>();
            }
            updateDescription.ScalingPolicies.Add(policy);

            fabricClient.ServiceManager.UpdateServiceAsync(Utils.GetApplicationApproverServiceName(Context), updateDescription);
        }

        public static void RegisterAprovalNumber(int numberOfApproved)
        {
            numberOfApprovalWithinThisPeriod += numberOfApproved;
        }

    }

}
