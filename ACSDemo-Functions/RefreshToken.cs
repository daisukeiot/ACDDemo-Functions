using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Communication.Identity;
using Azure.Core;
using Azure;
using System.Net;
using Azure.Communication;

namespace ACSDemo_Functions
{
    public static class RefreshToken
    {
        [FunctionName("RefreshToken")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionString");
            CommunicationIdentityClient client;
            IActionResult requestResponse;
            Response<AccessToken> tokenResponse;

            string requestBody = String.Empty;

            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            PostData data = JsonConvert.DeserializeObject<PostData>(requestBody);

            try
            {
                client = new CommunicationIdentityClient(connectionString);

            }
            catch (InvalidOperationException ex)
            {
                log.LogError($"CommunicationIdentityClient() initilization failed. : Exception {ex.GetType()} : {ex.Message}");
                requestResponse = CreateResponse(ex.Message, HttpStatusCode.BadRequest);
                return new BadRequestObjectResult(requestResponse);
            }

            string acsId = data.id;

            try
            {
                var identityToRefresh = new CommunicationUserIdentifier(acsId);
                tokenResponse = await client.GetTokenAsync(identityToRefresh, scopes: new[] { CommunicationTokenScope.VoIP });

                var response = tokenResponse.GetRawResponse();

                if (response.Status != 200)
                {
                    log.LogError($"GetTokenAsync() returned status {response.Status}");
                    requestResponse = CreateResponse($"GetTokenAsync() returned status {response.Status}", HttpStatusCode.InternalServerError);
                    return new BadRequestObjectResult(requestResponse);
                }

                var responseContent = new
                {
                    token = tokenResponse.Value.Token,
                    expiresOn = tokenResponse.Value.ExpiresOn
                };

                requestResponse = CreateResponse(responseContent, HttpStatusCode.OK);

            }
            catch (Exception ex)
            {
                log.LogError($"CreateUserAndTokenAsync() failed : Exception {ex.GetType()} : {ex.Message}");
                requestResponse = CreateResponse(ex.Message, HttpStatusCode.InternalServerError);
                return new BadRequestObjectResult(requestResponse);
            }

            log.LogInformation($"{tokenResponse.Value.Token.ToString()}");
            log.LogInformation($"\nCreated an identity with ID: {acsId}");
            log.LogInformation($"\nIssued an access token with 'voip' scope that expires at {tokenResponse.Value.ExpiresOn}:");


            return new OkObjectResult(requestResponse);
        }

        public class PostData
        {
            public string id { get; set; }
        }

        private static IActionResult CreateResponse(object message, HttpStatusCode httpCode)
        {
            ContentResult result = new ContentResult();
            result.Content = JsonConvert.SerializeObject(message, Formatting.None);
            result.ContentType = "application/json";
            result.StatusCode = (int)httpCode;
            return result;
        }
    }
}
