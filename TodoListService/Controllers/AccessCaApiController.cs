﻿using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using TodoList.Shared;
using TodoListService.Utils;

namespace TodoListService.Controllers
{
    [Authorize]
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class AccessCaApiController : ApiController
    {
        //
        // To authenticate to the Graph API, the app needs to know the Grah API's App ID URI.
        // To contact the Me endpoint on the Graph API we need the URL as well.
        //
        private IEnumerable<string> caResourceIdScope = new List<string> { ConfigurationManager.AppSettings["ida:CAProtectedResourceScope"] };

        // Error Constants
        const String SERVICE_UNAVAILABLE = "temporarily_unavailable";
        const String INTERACTION_REQUIRED = "interaction_required";

        TokenAcquisition _tokenAcquisition;

        [HttpGet]
        // GET: api/ConditionalAccess
        public async Task<string> Get()
        {
            var scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");
            if (scopeClaim == null || (!scopeClaim.Value.ContainsAny("user_impersonation")))
            {
                throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The Scope claim does not contain 'user_impersonation' or scope claim not found" });
            }

            AuthenticationResult result = null;

            _tokenAcquisition = new TokenAcquisition(SetOptions.SetMicrosoftIdOptions(), SetOptions.SetConClientAppOptions());

            // In the case of a transient error, retry once after 1 second, then abandon.
            // Retrying is optional.  It may be better, for your application, to return an error immediately to the user and have the user initiate the retry.
            bool retry = false;
            int retryCount = 0;

            do
            {
                retry = false;
                try
                {
                    result = await _tokenAcquisition.GetUserTokenOnBehalfOfAsync(caResourceIdScope);
                }
                catch (MsalUiRequiredException ex)
                {
                    await _tokenAcquisition.ReplyForbiddenWithWwwAuthenticateHeaderAsync((caResourceIdScope),
                        ex, HttpContext.Current.Response);
                    HttpContext.Current.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden, ReasonPhrase = INTERACTION_REQUIRED, Content = new StringContent(ex.Claims) });
                }
                catch (Exception ex)
                {
                    HttpResponseMessage myMessage = new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden, ReasonPhrase = SERVICE_UNAVAILABLE, Content = new StringContent(ex.Message) };
                    throw new HttpResponseException(myMessage);
                }
            } while ((retry == true) && (retryCount < 1));

            /*
             You can now use this  access token to accesss our Conditional-Access protected Web API using On-behalf-of
             Use this code below to call the downstream Web API OBO
             
            string oboAccessToken = result.AccessToken;
            private HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            HttpResponseMessage response = await httpClient.GetAsync(WebAPI2HttpEndpoint (App ID URI + "/endpoint");
            */

            return "protected API successfully called";
        }
    }
}