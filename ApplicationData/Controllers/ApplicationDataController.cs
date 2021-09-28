using Commons.Enums;
using Commons.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationData.Controllers
{

    [Route("api/[controller]")]
    public class ApplicationDataController : Controller
    {

        private const string applicationDictionaryName = "applications";
        private readonly IReliableStateManager stateManager;

        public ApplicationDataController(IReliableStateManager stateManager)
        {
            this.stateManager = stateManager;
        }


        [HttpGet]
        public async Task<IActionResult> GetApplications()
        {
            CancellationToken ct = new CancellationToken();

            IReliableDictionary<int, Application> applicationDic = await this.stateManager.GetOrAddAsync<IReliableDictionary<int, Application>>(applicationDictionaryName);

            List<KeyValuePair<int, Application>> applicationList = new List<KeyValuePair<int, Application>>();

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<int, Application>> enumerable = await applicationDic.CreateEnumerableAsync(tx);
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<int, Application>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(ct))
                {
                    applicationList.Add(enumerator.Current);
                }

            }

            return this.Json(applicationList);
        }


        [HttpGet("{citizenId}")]
        public async Task<IActionResult> GetApplicationsForCitizen(string citizenId)
        {
            CancellationToken ct = new CancellationToken();

            IReliableDictionary<int, Application> applicationDic = await this.stateManager.GetOrAddAsync<IReliableDictionary<int, Application>>(applicationDictionaryName);

            List<KeyValuePair<int, Application>> applicationList = new List<KeyValuePair<int, Application>>();

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<int, Application>> enumerable = await applicationDic.CreateEnumerableAsync(tx);
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<int, Application>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(ct))
                {
                    if (enumerator.Current.Value.CitizenId == citizenId)
                    {
                        applicationList.Add(enumerator.Current);
                    }
                }
            }

            return this.Json(applicationList);
        }


        [HttpGet("search")]
        public async Task<IActionResult> fetchAplications(string citizenId, ApplicationState? state)
        {
            CancellationToken ct = new CancellationToken();

            IReliableDictionary<int, Application> applicationDic = await this.stateManager.GetOrAddAsync<IReliableDictionary<int, Application>>(applicationDictionaryName);

            List<KeyValuePair<int, Application>> applicationList = new List<KeyValuePair<int, Application>>();

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<int, Application>> enumerable = await applicationDic.CreateEnumerableAsync(tx);
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<int, Application>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(ct))
                {
                    Application application = enumerator.Current.Value;
                    if (citizenId != null && state != null 
                        && application.CitizenId == citizenId && application.State == state)
                    {
                        applicationList.Add(enumerator.Current);
                    }
                    else if (citizenId != null && application.CitizenId == citizenId)
                    {
                        applicationList.Add(enumerator.Current);
                    }
                    else if (state != null && application.State == state)
                    {
                        applicationList.Add(enumerator.Current);
                    }
                    
                }
            }

            return this.Json(applicationList);
        }

        [HttpPut("changeState")]
        public async Task<IActionResult> ChangeApplicationState(int ApplicationId, ApplicationState NewState)
        {
            IReliableDictionary<int, Application> applicationDict = await this.stateManager.GetOrAddAsync<IReliableDictionary<int, Application>>(applicationDictionaryName);
            
            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                ConditionalValue<Application> ConditionalApp = await applicationDict.TryGetValueAsync(tx, ApplicationId);
                if (ConditionalApp.HasValue)
                {
                    Application application = ConditionalApp.Value;
                    application.State = NewState;
                    await applicationDict.AddOrUpdateAsync(tx, ApplicationId, application, (key, oldvalue) => application);
                    await tx.CommitAsync();
                }
                else
                {
                    return new NotFoundResult();
                }
            }
            return new OkResult();
        }



        [HttpPost("create")]
        public async Task<IActionResult> CreateApplication([FromBody] Application application)
        {
            IReliableDictionary<int, Application> applicationDict = await this.stateManager.GetOrAddAsync<IReliableDictionary<int, Application>>(applicationDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                await applicationDict.AddOrUpdateAsync(tx, application.Id, application, (key, oldvalue) => application);
                await tx.CommitAsync();
            }

            return new OkResult();
        }
    }
}
