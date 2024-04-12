using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeConverterCLI.Models;
using CodeConverterCLI.CommandLib;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CodeConverterCLI;

public class Part
{
    public string? text { get; set; }
}

internal class Content
{
    public List<Part>? parts { get; set; }
}
internal class GenerateContentRequest
{
    public List<Content>? contents { get; set; }
}

internal class GenerateContentResponse
{
    public List<Candidate>? candidates { get; set; }
}

internal class Candidate
{
    public Content? content { get; set; }
}


internal class CommandHandlers
{
    private string userInfoFilePath;
    private UserInfo? userInfo;
    private HttpClient apiClient;
    private static string apiKey = "AIzaSyDG56kq42JQTYKNtWCbM4gjIQpi67cJw8g";

    private string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={apiKey}";
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

        using (var formData = new MultipartFormDataContent())
        {
            formData.Add(fileStreamContent, "file", fileName);
            var response = await apiClient.PostAsync(actionUrl, formData);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }
            return 1;
        }
    }

    private async Task<string?> ScriptConvertHandler(string content, string sourceScript, string targetScript, int maxTokens)
    {
        var generateContentRequest = new GenerateContentRequest
        {
            contents = new List<Content>
            {
                new Content
                {
                    parts = new List<Part>
                    {
                        new Part { text = $"Convert this script {content} from {sourceScript} to {targetScript} (pure text without tidle characters)" }
                    }
                }
            }
        };

        var jsonReq = new StringContent(JsonSerializer.Serialize(generateContentRequest), Encoding.UTF8, "application/json");

        var response = await apiClient.PostAsync(url, jsonReq);

        if (response.IsSuccessStatusCode)
        {
            using var jsonResponse = await response.Content.ReadAsStreamAsync();

            var generateContentResponse = JsonSerializer.Deserialize<GenerateContentResponse>(jsonResponse);

            return generateContentResponse?.candidates?[0].content?.parts?[0].text;
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

        string filePath = (string)optionArgs!.GetValueOrDefault("file")!;
        string source = (string)optionArgs!.GetValueOrDefault("from")!;
        string target = (string)optionArgs!.GetValueOrDefault("to")!;
        string? output = (string?)optionArgs!.GetValueOrDefault("output")!;
        string? dir = (string?)optionArgs!.GetValueOrDefault("dir");

        if (dir != null && !Directory.Exists(dir)) throw new CmdException(ErrorCode.APP_ERROR, "Directory specified does not exist");

        string content = ReadFile(filePath);

        if (content.Length > 1000) throw new CmdException(ErrorCode.APP_ERROR, "Maximum content length exceeded");

        string? result = await ScriptConvertHandler(content, source, target, content.Length);

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

    public void showProfileHandler(Dictionary<string, object?>? optionArgs)
    {
        CheckAndSetAuthentication();

        Console.WriteLine(JsonSerializer.Serialize(userInfo).ToString());
    }
}
