﻿using Microsoft.Owin.Security.ActiveDirectory;
using Owin;
using Microsoft.IdentityModel.Tokens;

namespace TodoListService
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit https://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            app.UseWindowsAzureActiveDirectoryBearerAuthentication(
                new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    Tenant = AuthenticationConfig.TenantId,
                    TokenValidationParameters = new TokenValidationParameters
                    {
                        SaveSigninToken = true,
                        ValidAudiences = new [] { AuthenticationConfig.ClientId, $"api://{AuthenticationConfig.ClientId}" },
                    }
                }); 
        }
    }
}
