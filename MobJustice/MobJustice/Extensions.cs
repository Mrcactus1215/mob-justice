using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extensions
{
	public static class DictionaryExtensions
	{
		public static V Get<K, V>(this IDictionary<K, V> dict, K key, V @default)
		{
			V value;
			if (!dict.TryGetValue(key, out value))
			{
				dict.Add(key, @default);
				return @default;
			}
			else
			{
				return value;
			}
		}
	}
}
