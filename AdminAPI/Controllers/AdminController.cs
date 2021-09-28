using Commons.Models;
using Commons.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdminAPI.Controllers
{

    [Produces("application/json")]
    [Route("internal")]
    public class AdminController : Controller
    {
        private readonly HttpClient httpClient;
        private readonly FabricClient fabricClient;
        private readonly StatelessServiceContext serviceContext;

        public AdminController(HttpClient httpClient, FabricClient fabricClient, StatelessServiceContext serviceContext)
        {
            this.httpClient = httpClient;
            this.fabricClient = fabricClient;
            this.serviceContext = serviceContext;
        }

        [HttpPost("citizens/create")]
        public async Task<IActionResult> CreateCitizen([FromBody] Citizen citizen)
        {
            AdminAPI.RegisterCitizenCreation();

                        
            Uri proxyAddress = Utils.GetCitizenDataProxyAddress(this.serviceContext);
            long partitionKey = Utils.GetPartitionKey(citizen.Id);
            string proxyUrl = $"{proxyAddress}/api/CitizenData/create/?PartitionKey={partitionKey}&PartitionKind=Int64Range";

            StringContent content = new StringContent(JsonConvert.SerializeObject(citizen), Encoding.UTF8, "application/json");

            using (HttpResponseMessage response = await this.httpClient.PostAsync(proxyUrl, content))
            {
                return new ContentResult()
                {
                    StatusCode = (int)response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync()
                };
            }
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetAllApplications()
        {
            Uri proxyAddress = Utils.GetApplicationDataProxyAddress(this.serviceContext);

            ServicePartitionList partitionList = await this.fabricClient.QueryManager.GetPartitionListAsync(Utils.GetApplicationDataServiceName(this.serviceContext));

            List<KeyValuePair<int, Commons.Models.Application>> resultList = new List<KeyValuePair<int, Commons.Models.Application>>();

            foreach (Partition p in partitionList)
            {
                string proxyUrl = $"{proxyAddress}/api/ApplicationData?PartitionKey={((Int64RangePartitionInformation)p.PartitionInformation).LowKey}&PartitionKind=Int64Range";

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

            return this.Json(resultList);
        }


    }
}
