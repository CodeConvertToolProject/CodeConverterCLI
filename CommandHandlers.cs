using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeConverterCLI.CommandLib;
using System.Text.Json;
using System.IO;
    
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Net.Http;

namespace CodeConverterCLI;

internal class ConvertResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
}

internal class CommandHandlers
{
    private string userInfoFilePath;
    private UserInfo? userInfo;
    private HttpClient apiClient;

    private static readonly int MAX_CONTENT_LENGTH = 8192;

    public CommandHandlers(string userInfoFilePath, HttpClient apiClient)
    {
        this.userInfoFilePath = userInfoFilePath;
        this.userInfo = GetUserInfo();
        this.apiClient = apiClient;
    }

    private void CheckAndSetAuthentication()
    {
        if (userInfo?.AccessToken == null | userInfo?.Id == null | userInfo?.UserName == null)
        {
            throw new Exception("You have to be logged in to run this command");
        }
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userInfo!.AccessToken);
    }

    private UserInfo? GetUserInfo()
    {
        if (File.Exists(userInfoFilePath))
        {
            string userInfoString = File.ReadAllText(userInfoFilePath);

            return JsonSerializer.Deserialize<UserInfo>(userInfoString);
        }
        return default;
    }

    private async Task<int> Upload(string actionUrl, Stream paramFileStream, string fileName)
    {
        HttpContent fileStreamContent = new StreamContent(paramFileStream);

        using var formData = new MultipartFormDataContent
        {
            { fileStreamContent, "file", fileName }
        };

        var response = await apiClient.PostAsync(actionUrl, formData);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }
        return 1;
    }

    private async Task<string?> ScriptConvertHandler(string scriptContent, string source, string target)
    {
        CheckAndSetAuthentication();

        var response = await apiClient.PostAsJsonAsync("api/ScriptConvertGemini", 
            new
            {
                source,
                target,
                content = scriptContent
            });

        if (response.IsSuccessStatusCode)
        {
            using var jsonResponse = await response.Content.ReadAsStreamAsync();

            var convertResponse = JsonSerializer.Deserialize<ConvertResponse>(jsonResponse);

            return convertResponse!.Response;
        }
        return default;
    }

    private static string ReadFile(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);

            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            throw new CmdException(ErrorCode.APP_ERROR, ex.Message);
        }
    }

    public async Task ConvertHandler(Dictionary<string, object?>? optionArgs)
    {
        CheckAndSetAuthentication();

        apiClient.DefaultRequestHeaders.Authorization = null;

        string filePath = (string)optionArgs!["file"]!;
        string source = (string)optionArgs!["from"]!;
        string target = (string)optionArgs!["to"]!;
        string? output = (string?)optionArgs!["output"]!;
        string? dir = (string?)optionArgs!["dir"];

        if (dir != null && !Directory.Exists(dir)) throw new CmdException(ErrorCode.APP_ERROR, "Directory specified does not exist");

        string content = ReadFile(filePath);

        if (content.Length > MAX_CONTENT_LENGTH) throw new CmdException(ErrorCode.APP_ERROR, "Maximum content length exceeded");

        string? result = await ScriptConvertHandler(content, source, target);

        if (String.IsNullOrEmpty(result)) throw new CmdException(ErrorCode.APP_ERROR, "Error converting script");

        try
        {
            if (output != null)
            {
                string completePath = (dir != null) ? Path.Combine(dir, output) : Path.Combine(Directory.GetCurrentDirectory(), output);
                File.WriteAllText(completePath, result.Trim());
                Console.WriteLine($"Converted {target} script written to {completePath}");
            } 
            else
            {
                Console.WriteLine(result.Trim());
            }
        }
        catch (Exception ex)
        {
            throw new CmdException(ErrorCode.APP_ERROR, ex.Message);
        }
    }

    public void ShowProfileHandler(Dictionary<string, object?>? optionArgs)
    {
        CheckAndSetAuthentication();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        Console.WriteLine(JsonSerializer.Serialize(userInfo, options));
    }
}