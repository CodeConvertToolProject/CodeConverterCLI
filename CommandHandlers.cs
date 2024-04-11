using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeConverterCLI.Models;
using CodeConverterCLI.CommandLib;

namespace CodeConverterCLI;

internal class CommandHandlers(string? accessToken, string apiUrl, UserInfoResponse? userInfo)
{
    private string? _accessToken = accessToken;
    private string? _apiUrl = apiUrl;
    private UserInfoResponse? _apiKey = userInfo;

    private void CheckAuthentication()
    {
        if (String.IsNullOrEmpty(_accessToken))
        {
            Console.WriteLine(_accessToken);
            throw new Exception("You have to be logged in to run this command");
        }
    }

    public async Task ConvertHandler(Dictionary<string, object?>? optionArgs)
    {
        CheckAuthentication();

        //string file 
    }

}
