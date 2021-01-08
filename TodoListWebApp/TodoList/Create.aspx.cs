using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using TodoList.Shared;

namespace TodoListWebApp.TodoList
{
    public partial class Create : System.Web.UI.Page
    {
        private HttpClient _httpClient = new HttpClient();
        private string todoListBaseAddress = "https://localhost:44321";

        protected void Page_Load(object sender, EventArgs e)
        {
            CallAPI();
        }
        private void CallAPI()
        {
            PrepareAuthenticatedClientAsync().ConfigureAwait(false);
            HttpResponseMessage response = _httpClient.GetAsync(todoListBaseAddress + "/api/todolist").Result;
        }
        private async System.Threading.Tasks.Task PrepareAuthenticatedClientAsync()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            var app = Common.BuildConfidentialClientApplication();
            AuthenticationResult result = null;
            IAccount account = await app.GetAccountAsync(ClaimsPrincipal.Current.GetAccountId());
            try
            {
                result = await app.AcquireTokenSilent(new[] { SetOptions.TodoListScope }, account)
                   .ExecuteAsync();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            catch (MsalUiRequiredException ex)
            {
                throw ex;
            }
        }
    }
}