using System;
using System.Configuration;
using Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;

namespace TodoListWebApp
{
    public partial class Startup
    {
        private static string postLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];

        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = AuthenticationConfig.ClientId,
                    ClientSecret= AuthenticationConfig.ClientSecret,
                    Authority = AuthenticationConfig.Authority,
                    RedirectUri = postLogoutRedirectUri,
                    PostLogoutRedirectUri = postLogoutRedirectUri,
                    ResponseType="code",
                    Scope= "openid profile offline_access "+SetOptions.TodoListScope,
                    Notifications = new OpenIdConnectAuthenticationNotifications()
                    {
                        AuthorizationCodeReceived= async context =>
                        {
                            // As AcquireTokenByAuthorizationCode is asynchronous we want to tell ASP.NET core
                            // that we are handing the code even if it's not done yet, so that it does 
                            // not concurrently call the Token endpoint.
                            //context.HandleCodeRedemption();

                            // Call MSAL.NET AcquireTokenByAuthorizationCode
                            var application = Common.BuildConfidentialClientApplication();
                            try
                            {
                                var result = await application.AcquireTokenByAuthorizationCode(new[] { SetOptions.TodoListScope },
                                                                                         context.ProtocolMessage.Code)
                                                        .ExecuteAsync();

                                // Do not share the access token with ASP.NET Core otherwise ASP.NET will cache it
                                // and will not send the OAuth 2.0 request in case a further call to
                                // AcquireTokenByAuthorizationCode in the future for incremental consent 
                                // (getting a code requesting more scopes)
                                // Share the ID Token so that the identity of the user is known in the application (in 
                                // HttpContext.User)
                                context.HandleCodeRedemption(null, result.IdToken);
                            }
                            catch(Exception ex)
                            {
                                var a = ex.Message;
                            }
                          

                            // Call the previous handler if any
                            //await handler(context);
                        },
                        AuthenticationFailed = (context) =>
                        {
                            return System.Threading.Tasks.Task.FromResult(0);
                        }
                    }

                }
                );;;

            // This makes any middleware defined above this line run before the Authorization rule is applied in web.config
            app.UseStageMarker(PipelineStage.Authenticate);
        }

        private static string EnsureTrailingSlash(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                return value + "/";
            }

            return value;
        }
       
    }
}
