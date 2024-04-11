using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using CodeConverterCLI.CommandLib;
using CodeConverterCLI.Models;
using System.Threading;
using System.IO;

namespace CodeConverterCLI;

class Program
{
    static readonly string API_URL = "https://localhost:63286";
    static HttpClient apiClient = new();
    static UserInfoResponse? userInfo;
    static string? accessToken;

    static void ConfigureHttpClient(ref HttpClient client, string url)
    {
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    static string GetAccessTokenFilePath()
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string directoryPath = Path.Combine(homeDirectory, ".codeconv");
        return Path.Combine(directoryPath, "access_token.json");
    }

    static async Task<T> ParseJson<T>(HttpResponseMessage response)
    {
        using var responseStream = await response.Content.ReadAsStreamAsync();
        return JsonSerializer.Deserialize<T>(responseStream)!;
    }

    static async Task<LoginResponse> GetLoginInfo()
    {
        HttpResponseMessage response = await apiClient.PostAsync(
            "/api/Login/InitiateLogin", null);
        response.EnsureSuccessStatusCode();

        return await ParseJson<LoginResponse>(response);
    }

    static async Task GetUserInfo(UserInfoRequest userInfoRequest)
    {
        HttpResponseMessage response = await apiClient.PostAsJsonAsync(
            "/api/Login/GetUserInfo", userInfoRequest);

        response.EnsureSuccessStatusCode();
     
        userInfo = await ParseJson<UserInfoResponse>(response);
    }

    static async Task<AccessTokenResponse> GetAccessToken(AccessTokenRequest accessTokenRequest, LoginResponse loginResponse)
    {
        var startTime = DateTime.Now;
        int interval = int.Parse(loginResponse.Interval);

        var tcs = new TaskCompletionSource<AccessTokenResponse>();

        Timer timer = new(async _ =>
        {
            HttpResponseMessage response = await apiClient.PostAsJsonAsync(
                "/api/Login/PollForAccessCode", accessTokenRequest);

            if (response.IsSuccessStatusCode)
            {
                AccessTokenResponse accessTokenResponse = await ParseJson<AccessTokenResponse>(response);
                tcs.SetResult(accessTokenResponse);
            } else
            {
                var errorResponseString = await response.Content.ReadAsStringAsync();
                ErrorResponse errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorResponseString)!;

                if (errorResponse.Error != "authorization_pending") tcs.SetException(new Exception(errorResponse.Error));
            }
        }, null, 0, interval * 1000);

        AccessTokenResponse accessTokenResponse = await tcs.Task;

        timer.Dispose();

        return accessTokenResponse;
    }

    static void StoreAccessTokenInFile(AccessTokenResponse accessTokenResponse)
    {
        string jsonString = JsonSerializer.Serialize(accessTokenResponse);
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string directoryPath = Path.Combine(homeDirectory, ".codeconv");
        string filePath = Path.Combine(directoryPath, "access_token.json");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);   
        }

        File.WriteAllText(filePath, jsonString);
    }

    static async Task LoginHandler(Dictionary<string, object?>? optionArgs)
    {
        string accessTokenFilePath = GetAccessTokenFilePath();
        AccessTokenResponse? accessTokenObj = default;

        if (File.Exists(accessTokenFilePath))
        {
            string accessTokenJson = File.ReadAllText(accessTokenFilePath);

            accessTokenObj = JsonSerializer.Deserialize<AccessTokenResponse>(accessTokenJson);
            accessToken = accessTokenObj?.AccessToken;

            if (accessToken != null)
            {
                await GetUserInfo(new UserInfoRequest() { AccessToken = accessToken });
                Console.WriteLine("Logged in successfully");
            }
        }

        if (accessTokenObj == default)
        {
            LoginResponse loginResponse = await GetLoginInfo();
            Console.WriteLine($"Attempting to open browser so you can authorize. Should the browser not open use the following URL: \n\n{loginResponse.VerificationUriComplete}\n");

            Process.Start(new ProcessStartInfo(loginResponse.VerificationUriComplete) { UseShellExecute = true });

            try
            {
                AccessTokenResponse accessTokenResponse = await GetAccessToken(
                        new AccessTokenRequest
                        {
                            DeviceCode = loginResponse.DeviceCode,
                        },
                        loginResponse);

                accessToken = accessTokenResponse.AccessToken;
                await GetUserInfo(new UserInfoRequest() { AccessToken = accessToken });

                StoreAccessTokenInFile(accessTokenResponse);

                Console.WriteLine("Logged in successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured ({e.Message}) while fetching the access token");
            }
        } 
    }

    static void AddMainCLICommands(ref RootCommand rootCommand)
    {
        var cliCommands = new CommandHandlers(accessToken, API_URL, userInfo);

        var script = new Command("script", ["sc"], "Base command for working with scripts", true);
        var convert = new Command("convert", ["conv"], "Convert the given script to another language", true);

        convert.AddOptions(
                new CommandOption<string>(["-f", "--filename"], "The path to the script", true),
                new CommandOption<string>(["--from"], "The script language/type", true),
                new CommandOption<string>(["--to"], "The target language", true),
                new CommandOption<string>(["-o", "--output"], "The output file", true),
                new CommandOption<string>(["--dir"], "The output directory (defaults to the current directory)")
                );

        convert.SetHandler(cliCommands.ConvertHandler);

        script.AddSubcommands(convert);

        rootCommand.AddSubcommands(script);
    }

    static async Task Configure()
    {
        string accessTokenFilePath = GetAccessTokenFilePath();

        if (File.Exists(accessTokenFilePath))
        {
            string accessTokenJson = File.ReadAllText(accessTokenFilePath);

            accessToken = JsonSerializer.Deserialize<AccessTokenResponse>(accessTokenJson)?.AccessToken;

            if (accessToken != null)
            {
                await GetUserInfo(new UserInfoRequest() { AccessToken = accessToken });
            }
        }
    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("A CLI tool for language conversion");

        ConfigureHttpClient(ref apiClient, API_URL);
        await Configure();

        rootCommand.AddOptions(
            new CommandOption<bool?>(
                ["--version", "-v"],
                "Show version information"));
        
        rootCommand.SetHandler((Dictionary<string, object?>? optionArgs) =>
        {
            bool showVersion = (bool?)optionArgs?.GetValueOrDefault("version", default) ?? false;

            if (showVersion == true)
            {
                Console.WriteLine($"{rootCommand.Name} v1.0.0");
            }
        });

        Command login = new("login", [], "Authenticate with the backend and cache the access token");
        Command logout = new("logout", [], "Delete the access token");

        login.SetHandler(LoginHandler);
        logout.SetHandler((Dictionary<string, object?>? optionArgs) =>
        {
            string accessTokenFilePath = GetAccessTokenFilePath();
            if (File.Exists(accessTokenFilePath)) 
            {
                File.Delete(accessTokenFilePath);
                Console.WriteLine("Logged out successfully");
            }
            else
            {
                Console.WriteLine("You were not logged in");
            }
        });

        rootCommand.AddSubcommands(login, logout);

        AddMainCLICommands(ref rootCommand);

        return await rootCommand.Execute(args);
    }
}