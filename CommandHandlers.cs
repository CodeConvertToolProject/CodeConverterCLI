using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;

namespace CodeConverterCLI;

internal class CommandHandlers(string? accessToken, string apiUrl, UserInfoResponse? userInfo, HttpClient apiClient)
{
    private string? _accessToken = accessToken;
    private string _apiUrl = apiUrl;
    private UserInfoResponse? _apiKey = userInfo;

    private HttpClient _apiClient = apiClient;

    private string openaiModel = "gpt-3.5-turbo-instruct";

    private string openaiAccessKey = "";

    private void CheckAuthentication()
    {
        if (String.IsNullOrEmpty(_accessToken))
        {
            Console.WriteLine(_accessToken);
            throw new Exception("You have to be logged in to run this command");
        }
    }

    public async Task<string> ScriptConvertHandler(string content, string sourceScript, string targetScript, int maxTokens)
    {
        //using HttpClient client = new();

       // _apiClient.BaseAddress = new Uri(_apiUrl);
        _apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");

        string reqUrl = $"/api/ScriptConvert/{openaiAccessKey}";

        var req = new
        {
            model = openaiModel,
            sourceScript,
            targetScript,
            content,
            maxTokens
        };

        var jsonReq = new StringContent(JsonConvert.SerializeObject(req), Encoding.UTF8, "application/json");

        Console.WriteLine("\n\nREQ:" + await jsonReq.ReadAsStringAsync());

        var response = await _apiClient.PostAsync(reqUrl, jsonReq);

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();

            return jsonResponse;
        }
        else
        {
            return response.ToString();
        }

        //string file 
    }

}
