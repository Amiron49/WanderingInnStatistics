using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;

namespace WanderingInnStats
{
    public static class StatisticsHelper
    {
        public static void Increment<T>(this Dictionary<T, int> dictionary, T key, int amount = 1, string hint = "") where T : notnull
        {
            if (key is string test)
            {
                if (hint == "class" && test.Contains("fireball", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"we ballin");
                }
                
                var isNullOrEmpty = string.IsNullOrEmpty(test);

                if (isNullOrEmpty)
                {
                    Console.WriteLine($"Singularized is empty: {test}");
                }
                
                if (isNullOrEmpty || test != test.Singularize(false))
                {
                    Console.WriteLine($"Singularized: {test}");
                }
            }

            if (!dictionary.ContainsKey(key))
                dictionary[key] = 0;

            dictionary[key] += amount;
        }
		
        public static void AddOrCreate<T, TContent>(this Dictionary<T, List<TContent>> dictionary, T key, params TContent[] thingies) where T : notnull
        {
            if (key is string test)
            {
                if (string.IsNullOrEmpty(test) || test != test.Singularize(false))
                {
                    Console.WriteLine("lel");
                }
            }

            if (!dictionary.ContainsKey(key))
                dictionary[key] = new List<TContent>();

            dictionary[key].AddRange(thingies);
        }
		
        public static List<string> Merge<T>(this List<string> list, IDictionary<string, T> dictionary)
        {
            return list.Concat(dictionary.Keys.Except(list)).ToList();
        }

        public static List<string> Merge(this List<string> list, IEnumerable<string> other)
        {
            return list.Concat(other.Except(list)).ToList();
        }

        public static Dictionary<T, int> Accumulate<T>(this IEnumerable<Dictionary<T, int>> a) where T : notnull
        {
            var result = a.SelectMany(x => x)
                .GroupBy(x => x.Key)
                .Select(x => new KeyValuePair<T,int>(x.Key, x.Sum(y => y.Value)))
                .ToDictionary(x => x.Key, x => x.Value);

            return result;
        }
        
        public static Dictionary<T, int> Add<T>(this Dictionary<T, int> a, Dictionary<T, int> b) where T : notnull
        {
            var shallowCopy = new Dictionary<T, int>(a);
			
            foreach (var (key, value) in b)
            {
                if (shallowCopy.ContainsKey(key))
                    shallowCopy[key] = shallowCopy[key] + value;
                else
                    shallowCopy[key] = value;
            }

            return shallowCopy;
        }
		
        public static Dictionary<T, int> Sort<T>(this Dictionary<T, int> a) where T : notnull
        {
            return a.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
        }

        public static List<string> RemovePlurals(this List<string> list)
        {
            return list.Select(x => x.Singularize(false)).ToList();
        }
    }
}