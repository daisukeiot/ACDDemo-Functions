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
using Azure;
using Azure.Communication;
using System.Net;

namespace Microsoft.ACDDEmo
{
    public static class GetToken
    {
        [FunctionName("GetToken")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionString");
            CommunicationIdentityClient client;
            Response<CommunicationUserIdentifierAndToken> identityAndTokenResponse;
            CommunicationUserIdentifier identity;
            String token;
            DateTimeOffset expiresOn;
            IActionResult requestResponse;

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

            try
            {
                identityAndTokenResponse = await client.CreateUserAndTokenAsync(scopes: new[] { CommunicationTokenScope.VoIP });
                var response = identityAndTokenResponse.GetRawResponse();

                if (response.Status != 201)
                {
                    log.LogError($"CreateUserAndTokenAsync() returned status {response.Status}");
                    requestResponse = CreateResponse($"CreateUserAndTokenAsync() returned status {response.Status}", HttpStatusCode.InternalServerError);
                    return new BadRequestObjectResult(requestResponse);
                }

                // Retrieve the identity, token, and expiration date from the response
                identity = identityAndTokenResponse.Value.User;
                token = identityAndTokenResponse.Value.AccessToken.Token;
                expiresOn = identityAndTokenResponse.Value.AccessToken.ExpiresOn;

                var responseContent = new
                {
                    userId = identity.Id,
                    token = token,
                    expiresOn = expiresOn
                };

                requestResponse = CreateResponse(responseContent, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                log.LogError($"CreateUserAndTokenAsync() failed : Exception {ex.GetType()} : {ex.Message}");
                requestResponse = CreateResponse(ex.Message, HttpStatusCode.InternalServerError);
                return new BadRequestObjectResult(requestResponse);
            }

            // Print the details to the screen
            log.LogInformation($"{identityAndTokenResponse.Value.AccessToken.ToString()}");
            log.LogInformation($"\nCreated an identity with ID: {identity.Id}");
            log.LogInformation($"\nIssued an access token with 'voip' scope that expires at {expiresOn}:");
            //log.LogInformation(token);

            //Console.WriteLine($"\nCreated an identity with ID: {identity.Id}");
            string responseMessage = $"{{IdentityId : \"{identity}\", token : \"{token}\"}}";

            return new OkObjectResult(requestResponse);
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
