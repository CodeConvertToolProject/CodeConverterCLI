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
using System.Text.Json.Serialization;
using System.Linq;

namespace CodeConverterCLI;

internal class UserInfo
{
    [JsonPropertyName("username")]
    public string? UserName { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
}

internal class Developer
{
    [JsonPropertyName("devId")]
    public int DevId { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = String.Empty;
}

class Program
{
    static readonly string API_URL = "https://ec2-176-34-174-191.eu-west-1.compute.amazonaws.com";
    static HttpClient apiClient = new();
    static UserInfo? userInfo;

    static void ConfigureHttpClient(ref HttpClient client, string url)
    {
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    static string GetUserInfoFilePath()
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string directoryPath = Path.Combine(homeDirectory, ".codeconv");
        return Path.Combine(directoryPath, "user_info.json");
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

    static async Task SetUserInfo(UserInfoRequest userInfoRequest)
    {
        HttpResponseMessage response = await apiClient.PostAsJsonAsync(
            "/api/Login/GetUserInfo", userInfoRequest);

        response.EnsureSuccessStatusCode();
     
        var userInfoResponse = await ParseJson<UserInfoResponse>(response);

        userInfo!.UserName = userInfoResponse.UserName;
        userInfo!.Email = userInfoResponse.Email;
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

    static void StoreUserInfo()
    {
        string jsonString = JsonSerializer.Serialize(userInfo);
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string directoryPath = Path.Combine(homeDirectory, ".codeconv");
        string filePath = Path.Combine(directoryPath, "user_info.json");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);   
        }

        File.WriteAllText(filePath, jsonString);
    }

    static async Task<Developer> GetUserProfile()
    {
        HttpResponseMessage getDevelopersResponse = await apiClient.GetAsync(
                "/api/Developers");
        var developers = await ParseJson<List<Developer>>(getDevelopersResponse);

        Developer? developer = developers.Find(developer => developer.Username.Equals(userInfo!.UserName));

        if (developer == null)
        {
            HttpResponseMessage postDeveloperResponse = await apiClient.PostAsJsonAsync(
                "/api/Developers", new { username = userInfo!.UserName! });

            postDeveloperResponse.EnsureSuccessStatusCode();

            return await ParseJson<Developer>(postDeveloperResponse);
        }

        return developer;
    }

    static async Task ExecLogin()
    {
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userInfo!.AccessToken);

        await SetUserInfo(new UserInfoRequest() { AccessToken = userInfo!.AccessToken! });

        var userProfile = await GetUserProfile(); // queries by username (assuming uniqueness)

        userInfo.Id = userProfile.DevId;

        StoreUserInfo();
    }

    static async Task LoginHandler(Dictionary<string, object?>? optionArgs)
    {
        string userInfoFilePath = GetUserInfoFilePath();
        bool allRequiredFieldsExist = false;

        if (File.Exists(userInfoFilePath))
        {
            string userInfoString = File.ReadAllText(userInfoFilePath);

            userInfo = JsonSerializer.Deserialize<UserInfo>(userInfoString);
            allRequiredFieldsExist = userInfo?.Id != null && userInfo?.UserName != null && userInfo?.AccessToken != null;

            if (allRequiredFieldsExist)
            {
                Console.WriteLine("Logged in successfully");
            }
        }

        if (!allRequiredFieldsExist)
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

                userInfo = new UserInfo() { AccessToken = accessTokenResponse.AccessToken };

                await ExecLogin();

                Console.WriteLine("Logged in successfully");
            }
            catch (Exception e)
            {
                throw new CmdException(ErrorCode.APP_ERROR, $"An error occured ({e.Message}) while fetching the access token");
            }
        } 
    }

    static void AddMainCLICommands(ref RootCommand rootCommand)
    {
        var cliCommands = new CommandHandlers(GetUserInfoFilePath(), apiClient);

        var script = new Command("script", ["sc"], "Base command for working with scripts", true);
        var showProfile = new Command("show-profile", [], "Show profile");
        var convert = new Command("convert", ["conv"], "Convert the given script to another language", true);

        convert.AddOptions(
                new CommandOption<string?>(["--file", "-f"], "The file path of the script", true),
                new CommandOption<string?>(["--from"], "The script language/type", true),
                new CommandOption<string?>(["--to"], "The target language", true),
                new CommandOption<string?>(["--output", "-o"], "The output file"),
                new CommandOption<string?>(["--dir"], "The output directory (defaults to the current directory)")
                );

        convert.SetHandler(cliCommands.ConvertHandler);
        showProfile.SetHandler(cliCommands.showProfileHandler);

        script.AddSubcommands(convert);

        rootCommand.AddSubcommands(script, showProfile);
    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("A CLI tool for language conversion");
        var login = new Command("login", [], "Authenticate with the backend and cache the access token");
        var logout = new Command("logout", [], "Delete the access token");

        ConfigureHttpClient(ref apiClient, API_URL);

        rootCommand.AddOptions(
            new CommandOption<bool?>(
                ["--version", "-v"],
                "Show version information"));
        
        rootCommand.SetHandler((Dictionary<string, object?>? optionArgs) =>
        {
            if (optionArgs == null) 
            {
                rootCommand.DisplayHelp("");
            }
            else
            {
                bool showVersion = (bool?)optionArgs?.GetValueOrDefault("version", default) ?? false;

                if (showVersion == true)
                {
                    Console.WriteLine($"{rootCommand.Name} v1.0.0");
                }
            }
                
        });

        login.SetHandler(LoginHandler);

        logout.SetHandler((Dictionary<string, object?>? optionArgs) =>
        {
            string userInfoFilePath = GetUserInfoFilePath();
            if (File.Exists(userInfoFilePath)) 
            {
                File.Delete(userInfoFilePath);
                Console.WriteLine("Logged out successfully");
            }
            else
            {
                throw new CmdException(ErrorCode.APP_ERROR, "You were not logged in");
            }
        });

        rootCommand.AddSubcommands(login, logout);

        AddMainCLICommands(ref rootCommand);

        return await rootCommand.Execute(args);
    }
}