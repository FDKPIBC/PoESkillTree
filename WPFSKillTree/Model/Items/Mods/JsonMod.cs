﻿using System.Collections.Generic;
using Newtonsoft.Json;
using POESKillTree.Model.Items.Enums;

namespace POESKillTree.Model.Items.Mods
{
    // The classes used to deserialize RePoE's mods

    public class JsonMod
    {
        // string[] "adds_tags": not used
        [JsonProperty("domain")]
        public ModDomain Domain { get; set; }
        [JsonProperty("generation_type")]
        public ModType GenerationType { get; set; }
        // "grants_buff": not used
        // "grants_effect": not used
        [JsonProperty("group")]
        public string Group { get; set; }
        [JsonProperty("is_essence_only")]
        public bool IsEssenceOnly { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("required_level")]
        public int RequiredLevel { get; set; }
        [JsonProperty("spawn_tags")]
        public Dictionary<string, bool>[] SpawnTags { get; set; }
        [JsonProperty("stats")]
        public JsonStat[] Stats { get; set; }
    }

    public class JsonStat
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("max")]
        public int Max { get; set; }
        [JsonProperty("min")]
        public int Min { get; set; }
    }
}