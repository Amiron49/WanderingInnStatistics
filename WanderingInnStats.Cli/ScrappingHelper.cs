using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WanderingInnStats.Core;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.Cli
{
    public static class ScrappingHelper
    {
        public static async Task<List<Chapter>> ScrapeBlog(string path)
        {
            Console.WriteLine("Scrapping Blog");
            
            if (File.Exists(path))
            {
                Console.WriteLine($"Nvm got a cache for that @ {path}");
                return await DeSerialiseDump(path);
            }
            
            var toc = await Scrapper.GetToc();
            var chapters = await Scrapper.GetChapters(toc).ToListAsync();
            List<Chapter> workingChapters = chapters.Where(x => x != null).ToList()!;
            Console.WriteLine("Scrapping Done");
            await SerialiseDump(path, workingChapters);

            return workingChapters.ToList();
        }

        private static async Task SerialiseDump(string path, List<Chapter> workingChapters)
        {
            Console.WriteLine("Writing Json");
            await using var file = new FileStream(path, FileMode.Create);
            await JsonSerializer.SerializeAsync(file, workingChapters);
        }

        private static async Task<List<Chapter>> DeSerialiseDump(string path)
        {
            await using var file = new FileStream(path, FileMode.Open);
            return (await JsonSerializer.DeserializeAsync<List<Chapter>>(file))!;
        }

        public static async Task<List<CharacterRaw>> ScrapeWikiForCharacters(string path)
        {
            Console.WriteLine("Scrapping Wiki");

            if (File.Exists(path))
            {
                Console.WriteLine($"Nvm got a cache for that @ {path}");
                return await DeSerialiseCharacterDump(path);
            }
            
            using var wikiScrapper = new WikiScrapper();

            var characters = await wikiScrapper.GetAllCharacters();
            Console.WriteLine("Scrapping Wiki Done");

            await SerialiseDump(path, characters);
            return characters;
        }

        private static async Task SerialiseDump(string path, List<CharacterRaw> characters)
        {
            Console.WriteLine("Writing Json");
            await using var file = new FileStream(path, FileMode.Create);
            await JsonSerializer.SerializeAsync(file, characters);
        }

        private static async Task<List<CharacterRaw>> DeSerialiseCharacterDump(string path)
        {
            await using var file = new FileStream(path, FileMode.Open);
            return (await JsonSerializer.DeserializeAsync<List<CharacterRaw>>(file))!;
        }
    }
}