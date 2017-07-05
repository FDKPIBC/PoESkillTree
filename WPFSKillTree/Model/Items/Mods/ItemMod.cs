﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace POESKillTree.Model.Items.Mods
{
    /// <summary>
    /// Represents a single mod line on an item
    /// </summary>
    public class ItemMod
    {
        public enum ValueColoring
        {
            White = 0,
            LocallyAffected = 1,

            Fire = 4,
            Cold = 5,
            Lightning = 6,
            Chaos = 7
        }

        public static readonly Regex Numberfilter = new Regex(@"[0-9]*\.?[0-9]+");

        public string Attribute { get; }

        public IReadOnlyList<float> Values { get; set; }

        public IReadOnlyList<ValueColoring> ValueColors { get; set; }

        public bool IsLocal { get; }

        /// <summary>
        /// Creates an ItemMod using the numbers in <paramref name="attribute"/> as values
        /// </summary>
        public ItemMod(string attribute, bool isLocal, ValueColoring valueColor = ValueColoring.White)
        {
            IsLocal = isLocal;
            Attribute = Numberfilter.Replace(attribute, "#");
            Values = (
                from Match match in Numberfilter.Matches(attribute)
                select float.Parse(match.Value, CultureInfo.InvariantCulture)
            ).ToList();
            ValueColors = Values.Select(_ => valueColor).ToList();
        }

        /// <summary>
        /// Creates an ItemMod using the given values
        /// </summary>
        public ItemMod(string attribute, bool isLocal, IEnumerable<float> values, 
            IEnumerable<ValueColoring> valueColors)
        {
            IsLocal = isLocal;
            Attribute = attribute;
            Values = values.ToList();
            ValueColors = valueColors?.ToList() ?? new List<ValueColoring>();
        }

        private ItemMod(ItemMod other)
        {
            IsLocal = other.IsLocal;
            Attribute = other.Attribute;
            ValueColors = other.ValueColors.ToList();
        }

        public ItemMod Sum(ItemMod m)
        {
            return new ItemMod(m)
            {
                Values = Values.Zip(m.Values, (f1, f2) => f1 + f2).ToList()
            };
        }

        private string InsertValues(string into, ref int index)
        {
            var indexCopy = index;
            var result = Regex.Replace(into, "#", m => Values[indexCopy++].ToString("###0.##", CultureInfo.InvariantCulture));
            index = indexCopy;
            return result;
        }

        private JArray[] ValueTokensToJArrays(IEnumerable<string> tokens)
        {
            var valueIndex = 0;
            return tokens.Select(t => new JArray(InsertValues(t, ref valueIndex), ValueColors[valueIndex - 1])).ToArray();
        }

        public JToken ToJobject(bool asMod = false)
        {
            if (asMod)
            {
                var index = 0;
                return new JValue(InsertValues(Attribute, ref index));
            }

            const string allowedTokens = @"(#|#%|\+#%|#-#|#/#)";
            string name;
            var tokens = new List<string>();
            int displayMode;
            if (Values.Count == 0)
            {
                name = Attribute;
                displayMode = 0;
            }
            else if (Regex.IsMatch(Attribute, @"^[^#]*: (" + allowedTokens + @"(, |$))+"))
            {
                // displayMode 0 is for the form `Attribute = name + ": " + values.Join(", ")`
                name = Regex.Replace(Attribute, @"(: |, )" + allowedTokens + @"(?=, |$)", m =>
                {
                    tokens.Add(m.Value.TrimStart(',', ':', ' '));
                    return "";
                });
                displayMode = 0;
            }
            else
            {
                // displayMode 3 is for the form `Attribute = name.Replace("%i" with values[i])`
                var matchIndex = 0;
                name = Regex.Replace(Attribute, @"(?<=^|\s)" + allowedTokens + @"(?=$|\s|,)", m =>
                {
                    tokens.Add(m.Value);
                    return "%" + matchIndex++;
                });
                displayMode = 3;
            }
            return new JObject
            {
                {"name", name}, {"values", new JArray(ValueTokensToJArrays(tokens))}, {"displayMode", displayMode}
            };
        }
    }

    /// <summary>
    /// Extension methods for <see cref="IEnumerable{T}"/>s of <see cref="ItemMod"/>s.
    /// </summary>
    public static class ItemModExtensions
    {
        /// <summary>
        /// Returns the value at index <paramref name="valueIndex"/> of the first ItemMod in <paramref name="mods"/> whose
        /// attribute equals <paramref name="attribute"/>, or <paramref name="defaultValue"/> if there is no such ItemMod.
        /// </summary>
        public static float First(this IEnumerable<ItemMod> mods, string attribute, int valueIndex, float defaultValue)
        {
            return mods.Where(p => p.Attribute == attribute).Select(p => p.Values[valueIndex]).DefaultIfEmpty(defaultValue).First();
        }

        /// <summary>
        /// Returns true and writes the value at index <paramref name="valueIndex"/> of the first ItemMod in <paramref name="mods"/> whose
        /// attribute equals <paramref name="attribute"/> into <paramref name="value"/>, or returns false and writes 0 into <paramref name="value"/>
        /// if there is no such ItemMod.
        /// </summary>
        public static bool TryGetValue(this IEnumerable<ItemMod> mods, string attribute, int valueIndex, out float value)
        {
            var mod = mods.FirstOrDefault(p => p.Attribute == attribute);
            if (mod == null)
            {
                value = default(float);
                return false;
            }
            else
            {
                value = mod.Values[valueIndex];
                return true;
            }
        }
    }
}