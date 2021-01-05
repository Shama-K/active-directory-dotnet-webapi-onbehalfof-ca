﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;
using Microsoft.Net.Http.Headers;

namespace TodoList.Shared
{
    public class TokenAcquisition
    {
        private IConfidentialClientApplication _application;

        private readonly MicrosoftIdentityOptions _microsoftIdentityOptions=new MicrosoftIdentityOptions();
        private readonly ConfidentialClientApplicationOptions _applicationOptions=new ConfidentialClientApplicationOptions();
       
        public TokenAcquisition(MicrosoftIdentityOptions microsoftIdentityOptions, ConfidentialClientApplicationOptions applicationOptions)

        {
            _microsoftIdentityOptions = microsoftIdentityOptions;
            _applicationOptions=applicationOptions;
        }


        /// <summary>
        /// Used in web APIs (no user interaction).
        /// Replies to the client through the HTTP response by sending a 403 (forbidden) and populating the 'WWW-Authenticate' header so that
        /// the client, in turn, can trigger a user interaction so that the user consents to more scopes.
        /// </summary>
        /// <param name="scopes">Scopes to consent to.</param>
        /// <param name="msalServiceException">The <see cref="MsalUiRequiredException"/> that triggered the challenge.</param>
        /// <param name="httpResponse">The <see cref="HttpResponse"/> to update.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ReplyForbiddenWithWwwAuthenticateHeaderAsync(IEnumerable<string> scopes, MsalUiRequiredException msalServiceException, HttpResponse httpResponse = null)
        {
            // A user interaction is required, but we are in a web API, and therefore, we need to report back to the client through a 'WWW-Authenticate' header https://tools.ietf.org/html/rfc6750#section-3.1
            string proposedAction = Constants.Consent;
            if (msalServiceException.ErrorCode == MsalError.InvalidGrantError && AcceptedTokenVersionMismatch(msalServiceException))
            {
                throw msalServiceException;
            }

            _application = await GetOrBuildConfidentialClientApplicationAsync().ConfigureAwait(false);
            try
            {

          
            string consentUrl = $"{_application.Authority}/oauth2/v2.0/authorize?client_id={_applicationOptions.ClientId}"
                + $"&response_type=code&redirect_uri={_application.AppConfig.RedirectUri}"
                + $"&response_mode=query&scope=offline_access%20{string.Join("%20", scopes)}";

            IDictionary<string, string> parameters = new Dictionary<string, string>()
                {
                    { Constants.ConsentUrl, consentUrl },
                    { Constants.Claims, msalServiceException.Claims },
                    { Constants.Scopes, string.Join(",", scopes) },
                    { Constants.ProposedAction, proposedAction },
                };

            string parameterString = string.Join(", ", parameters.Select(p => $"{p.Key}=\"{p.Value}\""));


                if (httpResponse == null)
                {
                    throw new InvalidOperationException(IDWebErrorMessage.HttpContextAndHttpResponseAreNull);
                }

                var headers = httpResponse.Headers;
                httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;

                headers[HeaderNames.WWWAuthenticate] = new StringValues($"{Constants.Bearer} {parameterString}");

            }
            catch (Exception ex)
            {
                var a = ex.Message;
            }
        }

        public /* for testing */ async Task<IConfidentialClientApplication> GetOrBuildConfidentialClientApplicationAsync()
        {
            if (_application == null)
            {
                return await BuildConfidentialClientApplicationAsync().ConfigureAwait(false);
            }

            return _application;
        }
        /// <summary>
        /// Creates an MSAL confidential client application.
        /// </summary>
        public async Task<IConfidentialClientApplication> BuildConfidentialClientApplicationAsync()
        {
            var request = HttpContext.Current;
            var url = HttpContext.Current.Request.Url;
            string currentUri = null;

            if (!string.IsNullOrEmpty(_applicationOptions.RedirectUri))
            {
                currentUri = _applicationOptions.RedirectUri;
            }

            if (request != null && string.IsNullOrEmpty(currentUri))
            {
                currentUri = new UriBuilder(
                   url.Scheme,
                   url.Host,
                   url.Port).ToString();
            }

            PrepareAuthorityInstanceForMsal();

            if (!string.IsNullOrEmpty(_microsoftIdentityOptions.ClientSecret))
            {
                _applicationOptions.ClientSecret = _microsoftIdentityOptions.ClientSecret;
            }

            MicrosoftIdentityOptionsValidation.ValidateEitherClientCertificateOrClientSecret(
                 _applicationOptions.ClientSecret);

            try
            {
                var builder = ConfidentialClientApplicationBuilder
                        .CreateWithApplicationOptions(_applicationOptions);

                // The redirect URI is not needed for OBO
                if (!string.IsNullOrEmpty(currentUri))
                {
                    builder.WithRedirectUri(currentUri);
                }

                string authority;

                if (_microsoftIdentityOptions.IsB2C)
                {
                   // authority = $"{_applicationOptions.Instance}{ClaimConstants.Tfp}/{_microsoftIdentityOptions.Domain}/{_microsoftIdentityOptions.DefaultUserFlow}";
                   // builder.WithB2CAuthority(authority);
                }
                else
                {
                    authority = $"{_applicationOptions.Instance}{_applicationOptions.TenantId}/";
                    builder.WithAuthority(authority);
                }


                IConfidentialClientApplication app = builder.Build();
                _application = app;

                // Initialize token cache providers
                // After the ConfidentialClientApplication is created, we overwrite its default UserTokenCache serialization with our implementation
                IMsalTokenCacheProvider memoryTokenCacheProvider = CreateTokenCacheSerializer();
                await memoryTokenCacheProvider.InitializeAsync(app.UserTokenCache);
                return app;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public async Task<AuthenticationResult> GetUserTokenOnBehalfOfAsync(IEnumerable<string> requestedScopes)
        {
            string authority = $"{_applicationOptions.Instance}{_applicationOptions.TenantId}/";

            IConfidentialClientApplication app = BuildConfidentialClientApplicationAsync().Result;

            var bootstrapContext = ClaimsPrincipal.Current.Identities.First().BootstrapContext;

            string userAccessToken = (string)bootstrapContext;
            UserAssertion userAssertion = new UserAssertion(userAccessToken, "urn:ietf:params:oauth:grant-type:jwt-bearer");
                var result = await app.AcquireTokenOnBehalfOf(requestedScopes, userAssertion)
                            .WithAuthority(authority)
                            .ExecuteAsync();
                return result;
            
        }
        private static bool AcceptedTokenVersionMismatch(MsalUiRequiredException msalServiceException)
        {
            // Normally app developers should not make decisions based on the internal AAD code
            // however until the STS sends sub-error codes for this error, this is the only
            // way to distinguish the case.
            // This is subject to change in the future
            return msalServiceException.Message.Contains(
                ErrorCodes.B2CPasswordResetErrorCode);
        }
        private void PrepareAuthorityInstanceForMsal()
        {
            if (_microsoftIdentityOptions.IsB2C && _applicationOptions.Instance.EndsWith("/tfp/"))
            {
                _applicationOptions.Instance = _applicationOptions.Instance.Replace("/tfp/", string.Empty).Trim();
            }

            _applicationOptions.Instance = _applicationOptions.Instance.TrimEnd('/') + "/";
        }
        private static IMsalTokenCacheProvider CreateTokenCacheSerializer()
        {
            IServiceCollection services = new ServiceCollection();

            // In memory token cache. Other forms of serialization are possible.
            // See https://github.com/AzureAD/microsoft-identity-web/wiki/asp-net 
            services.AddInMemoryTokenCaches();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            IMsalTokenCacheProvider msalTokenCacheProvider = serviceProvider.GetRequiredService<IMsalTokenCacheProvider>();
            return msalTokenCacheProvider;
        }
        public async Task RemoveAccount()
        {
            string authority = $"{_applicationOptions.Instance}{_applicationOptions.TenantId}/";
            IConfidentialClientApplication clientapp = BuildConfidentialClientApplicationAsync().Result;

            // We only clear the user's tokens.
            IMsalTokenCacheProvider memoryTokenCacheProvider = CreateTokenCacheSerializer();
            await memoryTokenCacheProvider.InitializeAsync(clientapp.UserTokenCache);
            var userAccount = await clientapp.GetAccountAsync(ClaimsPrincipal.Current.GetAccountId());
            if (userAccount != null)
            {
                await clientapp.RemoveAsync(userAccount);
            }
        }
    }
    public static class Extensions
    {
        public static string GetAccountId(this ClaimsPrincipal claimsPrincipal)
        {
            string oid = claimsPrincipal.GetObjectId();
            string tid = claimsPrincipal.GetTenantId();
            return $"{oid}.{tid}";
        }
    }
}