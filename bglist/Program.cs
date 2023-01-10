using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace bglist;
class Program
{
    public class Options
    {
        [Option('u', "update", Required = false, HelpText = "Initiate sync with BGG data")]
        public bool Update { get; set; }
    }

    private const string Filename = "/bg-list.txt";

    static async Task Main(string[] args)
    {
        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async opt =>
            {
                if (opt.Update)
                {
                    await UpdateList();
                }
                
                await ReadList();
            });
    }

    private static async Task ReadList()
    {
        var file = GetPath() + Filename;
        if (!File.Exists(file))
        {
            await UpdateList();
        }

        var games = await File.ReadAllLinesAsync(file);

        foreach (var game in games)
        {
            Console.WriteLine(game);
        }
    }

    private static async Task UpdateList()
    {
        Console.WriteLine("Calling BGG...");

        var client = new HttpClient();
        var response = CallBgg(client);

        if (response.Result.StatusCode == HttpStatusCode.Accepted)
        {
            Console.WriteLine("Awaiting BGG readiness...");
            await Task.Delay(10000);
        }

        response = CallBgg(client);
        Console.WriteLine("Response received!");

        var xml = XDocument.Parse(await response.Result.Content.ReadAsStringAsync());
        var games = GetGames(xml);
        await SaveToFile(games);

        Console.WriteLine("Update done!");
        Console.WriteLine("");
    }

    private static async Task SaveToFile(IReadOnlyCollection<string> games)
    {
        var file = GetPath() + Filename;

        Console.WriteLine($"Saving list to: {file}");

        if (File.Exists(file))
        {
            File.Delete(file);
        }

        await File.WriteAllLinesAsync(file, games);
    }

    private static string GetPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    private static IReadOnlyCollection<string> GetGames(XDocument xml)
    {
        var games = new List<string>();

        foreach (var elem in xml.Descendants("item"))
        {
            var rating = elem?.Element("stats")?.Element("rating")?.Attribute("value")?.Value;

            if (string.IsNullOrWhiteSpace(rating) || rating != "N/A")
            {
                continue;
            }

            var name = elem?.Element("name")?.Value;

            if (!string.IsNullOrWhiteSpace(name))
            {
                games.Add(name);
            }
        }

        return games;
    }

    private static async Task<HttpResponseMessage> CallBgg(HttpClient httpClient) => 
        await httpClient.GetAsync("https://api.geekdo.com/xmlapi/collection/ffsjake?own=1");
}