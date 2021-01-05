﻿/*
 The MIT License (MIT)

Copyright (c) 2015 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
// The following using statements were added for this sample.
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TodoListClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Redirect URI is the URI where Azure AD will return OAuth responses.
        // The Authority is the sign-in URL of the tenant.
        //
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        Uri redirectUri = new Uri(ConfigurationManager.AppSettings["ida:RedirectUri"]);

        private static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        private static string todoListResourceId = ConfigurationManager.AppSettings["todo:TodoListResourceId"];
        private static string todoListBaseAddress = ConfigurationManager.AppSettings["todo:TodoListBaseAddress"];
        private static string[] scopes = { ConfigurationManager.AppSettings["TodoListServiceScope"] };

        private HttpClient httpClient = new HttpClient();
        private IPublicClientApplication _app = null;

        // Button strings
        const string signInString = "Sign In";
        const string clearCacheString = "Clear Cache";

        public MainWindow()
        {
            InitializeComponent();
            _app = PublicClientApplicationBuilder.Create(clientId)
                 .WithAuthority(authority)
                 .WithDefaultRedirectUri()
                 .Build();// new AuthenticationContext(authority, new FileCache());

            TokenCacheHelper.EnableSerialization(_app.UserTokenCache);

            GetTodoList();
        }

        private void GetTodoList()
        {
             GetTodoList(SignInButton.Content.ToString() != clearCacheString);
        }

        private async Task GetTodoList(bool isAppStarting)
        {
            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            var accounts = _app.GetAccountsAsync().Result;
            try
            {
                result = await _app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                SignInButton.Content = clearCacheString;
                this.SetUserName(result.Account);
            }
            catch (MsalUiRequiredException ex)
            {
                // There is no access token in the cache, so prompt the user to sign-in.
                if (!isAppStarting)
                {
                    MessageBox.Show("Please sign in to view your To-Do list");
                    SignInButton.Content = signInString;
                }
                UserName.Content = Properties.Resources.UserNotSignedIn;

                return;
            }
            catch (MsalException ex)
            {
                // An unexpected error occurred.
                string message = ex.Message;
                if (ex.InnerException != null)
                {
                    message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                }
                MessageBox.Show(message);
                UserName.Content = Properties.Resources.UserNotSignedIn;

                return;
            }

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do list service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/todolist");

            if (response.IsSuccessStatusCode)
            {

                // Read the response and databind to the GridView to display To Do items.
                string s = await response.Content.ReadAsStringAsync();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<TodoItem> toDoArray = serializer.Deserialize<List<TodoItem>>(s);

                TodoList.ItemsSource = toDoArray.Select(t => new { t.Title });
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }

            return;
        }

        private async void AddTodoItem(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TodoText.Text))
            {
                MessageBox.Show("Please enter a value for the To Do item name");
                return;
            }

            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            var accounts = _app.GetAccountsAsync().Result;
            try
            {
                result = await _app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                this.SetUserName(result.Account);
            }
            catch (MsalUiRequiredException ex)
            {
                // There is no access token in the cache, so prompt the user to sign-in.

                MessageBox.Show("Please sign in first");
                SignInButton.Content = signInString;

                UserName.Content = Properties.Resources.UserNotSignedIn;

                return;
            }
            catch (MsalException ex)
            {
                // An unexpected error occurred.
                string message = ex.Message;
                if (ex.InnerException != null)
                {
                    message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                }

                MessageBox.Show(message);

                UserName.Content = Properties.Resources.UserNotSignedIn;

                return;
            }
            //
            // Call the To Do service.
            //

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Forms encode Todo item, to POST to the todo list web api.
            HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", TodoText.Text) });

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.PostAsync(todoListBaseAddress + "/api/todolist", content);

            if (response.IsSuccessStatusCode)
            {
                TodoText.Text = "";
                GetTodoList();
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }
        }

        private async void SignIn(object sender = null, RoutedEventArgs args = null)
        {
            var accounts = (await _app.GetAccountsAsync()).ToList();
            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == clearCacheString)
            {
                TodoList.ItemsSource = string.Empty;
                //TokenCacheHelper.Clear();
                while (accounts.Any())
                {
                    await _app.RemoveAsync(accounts.First());
                    accounts = (await _app.GetAccountsAsync()).ToList();
                }

                // Also clear cookies from the browser control.
                SignInButton.Content = signInString;
                UserName.Content = Properties.Resources.UserNotSignedIn;
                return;
            }

            //
            // Get an access token to call the To Do list service.
            //
            AuthenticationResult result = null;
            try
            {
                // Force a sign-in (PromptBehavior.Always), as the ADAL web browser might contain cookies for the current user, and using .Auto
                // would re-sign-in the same user
                result = await _app.AcquireTokenInteractive(scopes).ExecuteAsync();
                SignInButton.Content = clearCacheString;
                SetUserName(result.Account);
                GetTodoList();
            }
            catch (MsalException ex)
            {
                if (ex.ErrorCode == "access_denied")
                {
                    // The user canceled sign in, take no action.
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                UserName.Content = Properties.Resources.UserNotSignedIn;

                return;
            }

        }

        // Set user name to text box
        private void SetUserName(IAccount userInfo)
        {
            string userName = userInfo.Username;

            if (userName == null)
                userName = Properties.Resources.UserNotIdentified;

            UserName.Content = userName;
        }

        const String INTERACTION_REQUIRED = "interaction_required";
        const String USER_CANCELED = "authentication_canceled";

        private async void AccessCAWebAPI(object sender, RoutedEventArgs e)
        {
            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            var accounts = (await _app.GetAccountsAsync()).ToList();
            try
            {
                result = await _app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // There is no access token in the cache, so prompt the user to sign-in.
                MessageBox.Show("Please sign in first");
                SignInButton.Content = "Sign In";
                return;
            }
            catch (MsalException ex)
            {
                // An unexpected error occurred.
                string message = ex.Message;
                if (ex.InnerException != null)
                {
                    message += "Inner Exception : " + ex.InnerException.Message;
                }
                MessageBox.Show(message);
                return;
            }

            //
            // Call the To Do service. We may get a claims challenge and need to redo the auth
            //
            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/AccessCaApi");

            if (response.IsSuccessStatusCode)
            {
                // User's token has already had an interactive auth with CA Policy 
                // Call to our api was successful 
                MessageBox.Show("We already Stepped-up.  Successfully called CA protected Web API");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && response.ReasonPhrase == INTERACTION_REQUIRED)
            {
                // We need to setup the token to account for a Conditional Access Policy
                String claimsParam = await response.Content.ReadAsStringAsync();

                if (String.IsNullOrWhiteSpace(claimsParam))
                {
                    MessageBox.Show("ESTS Returned no Claims on interaction_required");
                    return;
                }

                await SignInCA(claimsParam, result.Account.Username);

                accounts = (await _app.GetAccountsAsync()).ToList();

                try
                {
                    // Stepped up Access Token is in the cache
                    result = await _app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                }
                catch (MsalUiRequiredException)
                {
                    // There is no access token in the cache
                    MessageBox.Show("Please sign in first");
                    return;
                }
                catch (MsalException ex)
                {

                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }
                    MessageBox.Show(message);

                    return;
                }

                // Valid Access token in result, call our api with new token 
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage responseCA = await httpClient.GetAsync(todoListBaseAddress + "/api/AccessCaApi");

                if (responseCA.IsSuccessStatusCode)
                {
                    MessageBox.Show("Successfully called CA-Protected Web API");

                }
                else
                {
                    MessageBox.Show("Problem calling Web API HTTP " + response.StatusCode);
                }
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }
        }

        private async Task SignInCA(String claims, string displayName)
        {
            //
            // Get an access token to call the To Do list service w/ CA.
            //
            AuthenticationResult result = null;
            var accounts = _app.GetAccountsAsync().Result;
            try
            {
                result = await _app.AcquireTokenInteractive(scopes)
                    .WithClaims(claims).ExecuteAsync();

                /* Update UI */
                SignInButton.Content = "Clear Cache";

                /* Re-call the middle tier now that we've stepped/proofed-up */
                GetTodoList();
            }
            catch (MsalException ex)
            {
                if (ex.ErrorCode == USER_CANCELED)
                {
                    MessageBox.Show("Sign in was canceled by the user");
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }
                return;
            }
        }

        /// <summary>
        /// When the Web API needs consent, it can sent a 403 with information in the WWW-Authenticate header in 
        /// order to challenge the user
        /// </summary>
        /// <param name="response">HttpResonse received from the service</param>
        /// <returns></returns>
        //private async Task HandleChallengeFromWebApi(HttpResponseMessage response, IAccount account)
        //{
        //    var bearerResponse = response.Headers.WwwAuthenticate.ToString().Split(' ');
        //    if (!string.IsNullOrEmpty(bearerResponse[0]) && bearerResponse[0] == "Bearer")
        //    {
        //        string claims = GetParameter(bearerResponse, "claims");
        //        claims = (bearerResponse.FirstOrDefault(p => p.StartsWith($"claims="))?.Substring(8)?.Trim('"'))?.Trim(',');
        //        string[] scopes = GetParameter(bearerResponse, "scopes")?.Split(',');
        //        string proposedAction = GetParameter(bearerResponse, "proposedAction");
        //        string consentUri = GetParameter(bearerResponse, "consentUri");
        //        string loginHint = account?.Username;
        //        string domainHint = IsConsumerAccount(account) ? "consumers" : "organizations";
        //        string extraQueryParameters = $"claims={claims}&domainHint={domainHint}";

        //        await SignInCA(claims, account?.Username);
        //    }
        //}
        /// <summary>
        /// Tells if the account is a consumer account
        /// </summary>
        /// <param name="account">Account</param>
        /// <returns><c>true</c> if the application supports MSA+AAD and the home tenant id of the account is the MSA tenant. <c>false</c>
        /// otherwise (in particular if the app is a single-tenant app, returning <c>false</c> enables MSA accounts which are guest
        /// of a directory</returns>
        private static bool IsConsumerAccount(IAccount account)
        {
            const string msaTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";
            return (tenant == "common" || tenant == "consumers") && account?.HomeAccountId.TenantId == msaTenantId;
        }
        private static string GetParameter(IEnumerable<string> parameters, string parameterName)
        {
            int offset = parameterName.Length + 1;
            return parameters.FirstOrDefault(p => p.StartsWith($"{parameterName}="))?.Substring(offset)?.Trim('"');
        }
    }
}
