using Dasync.Collections;
using Newtonsoft.Json;
using OfficeEnum.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace OfficeEnum
{
    static class Program
    {
        private static async Task<bool> ValidateAccount(this HttpClient httpClient, string username, string country = "US")
        {

            var getCredentialTypeReq = new GetCredentialTypeReq
            {
                username = username,
                country = country

            };

            var httpReq = await httpClient.PostAsync(
                "https://login.microsoftonline.com/common/GetCredentialType?",
                new StringContent(JsonConvert.SerializeObject(getCredentialTypeReq), Encoding.UTF8, "application/json")
                );

            if (httpReq.IsSuccessStatusCode)
            {
                var httpResp = await httpReq.Content.ReadAsStringAsync();

                GetCredentialTypeResp getCredentialTypeResp = JsonConvert.DeserializeObject<GetCredentialTypeResp>(httpResp);

                return (getCredentialTypeResp.IfExistsResult == 0);
            }

            return false;
        }
        static async Task AsyncMain(string[] args)
        {
            //Read a list of usernames
            var usernameArray = File.ReadAllLines(args[0]);
            Console.WriteLine($"Reading accounts from file {args[0]}");

            var proxy = new WebProxy
            {
                Address = new Uri($"http://127.0.0.1:8080"),
                BypassProxyOnLocal = false,
                UseDefaultCredentials = false,
            };


            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                ServerCertificateCustomValidationCallback = (message, xcert, chain, errors) =>
                {

                    return true;
                },
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
            };

            using (var httpClient = new HttpClient(httpClientHandler))
            {
                var canaryUsername = $"{Guid.NewGuid().ToString().Replace("-", "")}@" + usernameArray[0].Split("@")[1];

                if (await httpClient.ValidateAccount(canaryUsername))
                {
                    Console.WriteLine($"Our canary acount hit, cannot enum!");
                    Environment.Exit(0);
                }


                //Loop trougth ASYNC all those usernames
                await usernameArray.ParallelForEachAsync(
                    async username =>
                    {
                        if (await httpClient.ValidateAccount(username))
                            Console.WriteLine($"Account {username} is valid!!");

                    }, maxDegreeOfParallelism: 300);
            }
        }

        static void Main(string[] args)
        {
            AsyncMain(args).GetAwaiter().GetResult();
        }
    }
}
