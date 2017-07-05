﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using POESKillTree.Utils;

namespace UpdateDB.DataLoading
{
    /// <summary>
    /// Downloads json data files from RePoE and saves them to the file system.
    /// </summary>
    public class RePoELoader : DataLoader
    {
        private static readonly string[] Files =
        {
            "mods", "crafting_bench_options", "npc_master", "stat_translations"
        };

        public override bool SavePathIsFolder => true;

        protected override async Task LoadAsync()
        {
            await Task.WhenAll(Files.Select(LoadAsync));
        }

        private async Task LoadAsync(string file)
        {
            var fileName = file + RePoEUtils.FileSuffix;
            var response = await HttpClient.GetAsync(RePoEUtils.RePoEDataUrl + fileName);
            using (var writer = File.Create(Path.Combine(SavePath, fileName)))
            {
                await response.Content.CopyToAsync(writer).ConfigureAwait(false);
            }
        }
    }
}