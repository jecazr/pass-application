using Commons.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CitizenData.Controllers
{
    [Route("api/[controller]")]
    public class CitizenDataController : Controller
    {
        private const string citizenDictionaryName = "citizens";
        private readonly IReliableStateManager stateManager;

        public CitizenDataController(IReliableStateManager stateManager)
        {
            this.stateManager = stateManager;
        }



        [HttpPost("create")]
        public async Task<IActionResult> CreateCitizen([FromBody] Citizen citizen)
        {
            IReliableDictionary<string, Citizen> citizenDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, Citizen>>(citizenDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                await citizenDictionary.AddOrUpdateAsync(tx, citizen.Id, citizen, (key, oldvalue) => citizen);
                await tx.CommitAsync();
            }

            return new OkResult();
        }



        [HttpGet]
        public async Task<IActionResult> GetCitizen(string citizenId)
        {
            CancellationToken ct = new CancellationToken();

            IReliableDictionary<string, Citizen> resultDic = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, Citizen>>(citizenDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<string, Citizen>> enumberable = await resultDic.CreateEnumerableAsync(tx);
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<string, Citizen>> enumerator = enumberable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(ct))
                {
                    if (enumerator.Current.Key == citizenId)
                    {
                        return this.Json(enumerator.Current);
                    }
                }
            }

            return NotFound();
        }
    }
}
