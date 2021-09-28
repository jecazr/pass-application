using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using Commons.Utilities;
using Commons.Models;
using Commons.Enums;
using Application = Commons.Models.Application;

namespace ApplicationWeb.Controllers
{
    [Produces("application/json")]
    [Route("public")]
    public class ApplicationWebController : Controller
    {

        private readonly HttpClient httpClient;
        private readonly FabricClient fabricClient;
        private readonly StatelessServiceContext serviceContext;

        public ApplicationWebController(HttpClient httpClient, FabricClient fabricClient, StatelessServiceContext serviceContext)
        {
            this.httpClient = httpClient;
            this.fabricClient = fabricClient;
            this.serviceContext = serviceContext;
        }

        [HttpGet("citizens/{citizenId}")]
        public async Task<IActionResult> GetCitizenData(string citizenId)
        {
            Uri proxyAddress = Utils.GetCitizenDataProxyAddress(this.serviceContext);

            long partitionKey = Utils.GetPartitionKey(citizenId);

            string proxyUrl = $"{proxyAddress}/api/CitizenData/?citizenId={citizenId}&PartitionKey={partitionKey}&PartitionKind=Int64Range";

            KeyValuePair<string, Citizen> result = new KeyValuePair<string, Citizen>();

            using (HttpResponseMessage response = await this.httpClient.GetAsync(proxyUrl))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    //TODO throw error
                } else
                {
                    result = JsonConvert.DeserializeObject<KeyValuePair<string, Citizen>>(await response.Content.ReadAsStringAsync());
                }
            }

            return this.Json(result);
        }



        [HttpGet("applications/{citizenId}")]
        public async Task<IActionResult> GetAllApplicationsForCitizen(string citizenId)
        {
            Uri proxyAddress = Utils.GetApplicationDataProxyAddress(this.serviceContext);
            long partitionKey = Utils.GetPartitionKey(citizenId);
            
            string proxyUrl = $"{proxyAddress}/api/ApplicationData/search?citizenId={citizenId}&PartitionKey={partitionKey}&PartitionKind=Int64Range";

            List<KeyValuePair<int, Commons.Models.Application>> resultList = new List<KeyValuePair<int, Commons.Models.Application>>();

            using (HttpResponseMessage response = await httpClient.GetAsync(proxyUrl))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    //TODO throw exception
                }

                resultList.AddRange(JsonConvert.DeserializeObject<List<KeyValuePair<int, Commons.Models.Application>>>(await response.Content.ReadAsStringAsync()));
            }
            

            return this.Json(resultList);
            
        }
        


        [HttpPost("applications/create/{citizenId}")]
        public async Task<IActionResult> CreateApplication(string citizenId)
        {
            //fetch citizen data
            Uri citizenProxyAddress = Utils.GetCitizenDataProxyAddress(this.serviceContext);
            long citizenPartitionKey = Utils.GetPartitionKey(citizenId);
            string citizenProxyUrl = $"{citizenProxyAddress}/api/CitizenData/?citizenId={citizenId}&PartitionKey={citizenPartitionKey}&PartitionKind=Int64Range";

            KeyValuePair<string, Citizen> result = new KeyValuePair<string, Citizen>();

            using (HttpResponseMessage response = await this.httpClient.GetAsync(citizenProxyUrl))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    //TODO throw error
                }
                else
                {
                    result = JsonConvert.DeserializeObject<KeyValuePair<string, Citizen>>(await response.Content.ReadAsStringAsync());
                }
            }

            Citizen citizen = result.Value;
            Application application = new Application { Id = Application.GetApplicationCounterNext(), CitizenId = citizen.Id, DateOfBirth = citizen.DateOfBirth, Gender = citizen.Gender, Name = citizen.Name, State = ApplicationState.Created };

            Uri applicationProxyAddress = Utils.GetApplicationDataProxyAddress(this.serviceContext);
            long applicationPartitionKey = Utils.GetPartitionKey(citizen.Id);
            string applicationProxyUrl = $"{applicationProxyAddress}/api/ApplicationData/create?PartitionKey={applicationPartitionKey}&PartitionKind=Int64Range";

            StringContent content = new StringContent(JsonConvert.SerializeObject(application), Encoding.UTF8, "application/json");

            using (HttpResponseMessage response = await httpClient.PostAsync(applicationProxyUrl, content))
            {
                return new ContentResult()
                {
                    StatusCode = (int)response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync()
                };
            }

        }

    }
}
