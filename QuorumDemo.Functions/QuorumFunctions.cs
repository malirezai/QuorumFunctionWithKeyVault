using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuorumDemo.Core;
using QuorumDemo.Core.Models;
using System.Collections.Generic;

namespace QuorumDemo.Functions
{
    /*
     * Function Body - defines the Contract Address, Function Name, Input Paramters, and PrivateFor strings
     */

    public class QuorumTransactionInput
    {
        public string contractAddress {get;set;}
        public string functionName {get;set;}
        public List<string> privateFor {get;set;}
        public object[] inputParams { get;set;}    
    }   

    /*
        THE FOLLOWING FUNCTIONS USE A KEY WITHIN KEY VAULT TO SIGN TRANSACTIONS. 
        ALL IMPLEMENTATION DETAILS FOR THIS EXTERNAL ACCOUNT CAN BE FOUND WIHIN ACCOUNTHELPER.CS
     */

    public static class QuorumCreateContractAsync
    {
        [FunctionName(nameof(QuorumCreateContractAsync))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject<QuorumTransactionInput>(requestBody);
            
            var privateFor = data?.privateFor;
            var inputParams = data?.inputParams;

            var contractInfo = new ContractInfo();
            var client = new HttpClient();

            log.LogInformation("Before getting JSON blob");

            var filejson = await client.GetStringAsync(Environment.GetEnvironmentVariable("CONTRACT_JSON_BLOB_URL", EnvironmentVariableTarget.Process));
            dynamic _file = JsonConvert.DeserializeObject(filejson);

            var abi = _file?.abi;
            var byteCode = _file?.bytecode?.Value;

            contractInfo.ContractABI = JsonConvert.SerializeObject(abi);
            contractInfo.ContractByteCode = byteCode;

            var keyVaultURI = Environment.GetEnvironmentVariable("KEYVAULT_PRIVATEKEY_URI", EnvironmentVariableTarget.Process);
            var RPC = Environment.GetEnvironmentVariable("RPC", EnvironmentVariableTarget.Process);


            QuorumContractHelper.Instance.SetWeb3Handler(RPC);

            var appID = Environment.GetEnvironmentVariable("APP_ID", EnvironmentVariableTarget.Process);
            var appSecret = Environment.GetEnvironmentVariable("APP_SECRET", EnvironmentVariableTarget.Process);

            var externalAccount = AccountHelper.BuildExternalSigner(log,keyVaultURI); 
            //var externalAccount = AccountHelper.BuildExternalSignerWithToken(log,keyVaultURI,appID,appSecret); 
            
            var res = await QuorumContractHelper.Instance.CreateContractWithExternalAccountAsync(contractInfo, externalAccount, inputParams, privateFor);

            return res != null
                ? (ActionResult)new OkObjectResult($"TXHash: {res.TransactionHash} \nBlockHash: {res.BlockHash} \nBlockNumber: {res.BlockNumber} \nContractAddress: {res.ContractAddress}")
                : new BadRequestObjectResult("There was an issue submitting the transaction");
        }
    }

    public static class QuorumSendTransactionAsync
    {

        [FunctionName(nameof(QuorumSendTransactionAsync))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<QuorumTransactionInput>(requestBody);
            
            var address = data?.contractAddress;
            var functionName = data?.functionName;
            var functionParams = data?.inputParams;

            if(functionName == null || (string.IsNullOrEmpty(address)))
                return new BadRequestObjectResult("You must supply a contract address and function name");

            List<string> privateFor = data?.privateFor;

            var contractInfo = new ContractInfo();
            var client = new HttpClient();

            var filejson = await client.GetStringAsync(Environment.GetEnvironmentVariable("CONTRACT_JSON_BLOB_URL", EnvironmentVariableTarget.Process));
            dynamic _file = JsonConvert.DeserializeObject(filejson);

            var abi = _file?.abi;
            var byteCode = _file?.bytecode?.Value;

            contractInfo.ContractABI = JsonConvert.SerializeObject(abi);
            contractInfo.ContractByteCode = byteCode;

            var keyVaultURI = Environment.GetEnvironmentVariable("KEYVAULT_PRIVATEKEY_URI", EnvironmentVariableTarget.Process);
            var RPC = Environment.GetEnvironmentVariable("RPC", EnvironmentVariableTarget.Process);

            QuorumContractHelper.Instance.SetWeb3Handler(RPC);

            var appID = Environment.GetEnvironmentVariable("APP_ID", EnvironmentVariableTarget.Process);
            var appSecret = Environment.GetEnvironmentVariable("APP_SECRET", EnvironmentVariableTarget.Process);

            var externalAccount = AccountHelper.BuildExternalSigner(log,keyVaultURI); 
            
            //var externalAccount = AccountHelper.BuildExternalSignerWithToken(log,keyVaultURI,appID,appSecret); 
            var res = await QuorumContractHelper.Instance.CreateTransactionWithExternalAccountAsync(address, contractInfo, functionName, externalAccount, functionParams, privateFor);

            return res != null
                ? (ActionResult)new OkObjectResult($"TXHash: {res.TransactionHash} \nBlockHash: {res.BlockHash} \nBlockNumber: {res.BlockNumber} \nContractAddress: {res.ContractAddress}")
                : new BadRequestObjectResult("There was an issue submitting the transaction");
        }
    }


    public static class QuorumCallFunctionAsync
    {

        [FunctionName(nameof(QuorumCallFunctionAsync))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<QuorumTransactionInput>(requestBody);
            
            var address = data?.contractAddress;
            var functionName = data?.functionName;
            var functionParams = data?.inputParams;
            
            if(functionName == null || (string.IsNullOrEmpty(address)))
                return new BadRequestObjectResult("You must supply a contract address and function name");

            List<string> privateFor = data?.privateFor;

            var contractInfo = new ContractInfo();
            var client = new HttpClient();

            var filejson = await client.GetStringAsync(Environment.GetEnvironmentVariable("CONTRACT_JSON_BLOB_URL", EnvironmentVariableTarget.Process));
            dynamic _file = JsonConvert.DeserializeObject(filejson);

            var abi = _file?.abi;

            var byteCode = _file?.bytecode?.Value;

            contractInfo.ContractABI = JsonConvert.SerializeObject(abi);
            contractInfo.ContractByteCode = byteCode;

            var accountJSON = Environment.GetEnvironmentVariable("KEYVAULT_ACCOUNT1_URL", EnvironmentVariableTarget.Process);
            
            var pwd = Environment.GetEnvironmentVariable("KEYVAULT_ETH_PASSWORD", EnvironmentVariableTarget.Process);
            var RPC = Environment.GetEnvironmentVariable("RPC", EnvironmentVariableTarget.Process);

            QuorumContractHelper.Instance.SetWeb3Handler(RPC);
            //var res = await QuorumContractHelper.Instance.CallContractFunctionAsync<int>(address, contractInfo, functionName, AccountHelper.DecryptAccount(accountJSON,pwd),functionParams);

            var keyVaultURI = Environment.GetEnvironmentVariable("KEYVAULT_PRIVATEKEY_URI", EnvironmentVariableTarget.Process);
            var appID = Environment.GetEnvironmentVariable("APP_ID", EnvironmentVariableTarget.Process);
            var appSecret = Environment.GetEnvironmentVariable("APP_SECRET", EnvironmentVariableTarget.Process);

            var externalAccount = AccountHelper.BuildExternalSignerWithToken(log,keyVaultURI,appID,appSecret); 
            var res = await QuorumContractHelper.Instance.CallContractFunctionAsync<int>(address, contractInfo, functionName, externalAccount.Address,functionParams);
            
            return res != null
                ? (ActionResult)new OkObjectResult($"Called Contract at address: {address} \nWith Function: {functionName} \nWith input: {functionParams} \nResult: {res}")
                : new BadRequestObjectResult("There was an issue submitting the transaction");
        }
    }

}
