﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace MvpApi.Services.Utilities
{
    public class OAuthHelper
    {
        // Live SDK -> Graph scope comparison https://docs.microsoft.com/en-us/onedrive/developer/rest-api/concepts/migrating-from-live-sdk?view=odsp-graph-online#permissions
        // string[] oldLiveScopes = { "wl.emails", "wl.basic", "wl.offline_access", "wl.signin" };

        // Graph equivalents for Live SDK scope are: "email", "Contacts.Read", "offline_access", "openid"
        // Note: Don't include 'openid' 'profile' or 'offline_access' in array, these are always sent by MSAL (you'll get exception)
        readonly string[] _graphScopes = { "Contacts.Read", "email" }; // 

        public OAuthHelper()
        {
            // MSAL test
            // - for any Work or School accounts, or Microsoft personal account, use "common"
            // - for Microsoft Personal account, use "consumers"
            string clientId = "090fa1d9-3d6f-4f6f-a733-a8b8a3fe16ff";
            string tenant = "consumers";
            string authority = $"https://login.microsoftonline.com/{tenant}";

            PublicClientApp = new PublicClientApplication(clientId, authority);
        }

        public PublicClientApplication PublicClientApp { get; }

        public async Task<AuthenticationResult> LogInAsync()
        {
            AuthenticationResult authResult;

            var accounts = await PublicClientApp.GetAccountsAsync();

            try
            {
                authResult = await PublicClientApp.AcquireTokenSilentAsync(_graphScopes, accounts.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync, this indicates you need to call AcquireTokenAsync to acquire a token
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    authResult = await PublicClientApp.AcquireTokenAsync(_graphScopes);
                }
                catch (MsalException msalex)
                {
                    Debug.WriteLine($"Error Acquiring Token:{Environment.NewLine}{msalex}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Acquiring Token Silently:{Environment.NewLine}{ex}");
                return null;
            }

            if (authResult != null)
            {
                //Debug.WriteLine($"{authResult.Account.Username} - Signed In. Expires {authResult.ExpiresOn.ToLocalTime()}{Environment.NewLine}");
                //Debug.WriteLine(DisplayBasicTokenInfo(authResult));

                // Store encrypted
                // StorageHelpers.Instance.StoreToken("access_token", authResult.AccessToken);
                // StorageHelpers.Instance.StoreToken("expires_on", authResult.ExpiresOn.ToString());

                Debug.WriteLine($"LogInAsync Start: {authResult.Account.Username}");

                return authResult;
            }
            else
            {
                Debug.WriteLine("user not signed in");
                return null;
            }
        }

        public async Task<Tuple<bool, string>> LogOutAsync()
        {
            var accounts = await PublicClientApp.GetAccountsAsync();

            if (accounts.Any())
            {
                try
                {
                    await PublicClientApp.RemoveAsync(accounts.FirstOrDefault());
                    return new Tuple<bool, string>(true, "User has signed out.");
                }
                catch (MsalException ex)
                {
                    return new Tuple<bool, string>(false, $"Error signing-out user: {ex.Message}");
                }
            }

            return new Tuple<bool, string>(true, "There were no signed-in accounts.");
        }

        public string DisplayBasicTokenInfo(AuthenticationResult authResult)
        {
            var tokenInfoText = "";

            if (authResult != null)
            {
                tokenInfoText += $"Username: {authResult.Account.Username}" + Environment.NewLine;
                tokenInfoText += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
                tokenInfoText += $"Access Token: {authResult.AccessToken}" + Environment.NewLine;
            }

            return tokenInfoText;
        }
    }
}
