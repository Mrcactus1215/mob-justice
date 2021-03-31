using System;
using System.Collections.Generic;

namespace Extensions {
	public static class DictionaryExtensions {
		// Emulate Python's .get method for dictionaries
		public static V Get<K, V>(this IDictionary<K, V> dict, K key, V @default) {
			V value;
			if (!dict.TryGetValue(key, out value)) {
				dict.Add(key, @default);
				return @default;
			}
			else {
				return value;
			}
		}

		// Making .ForEach work on Dictionaries to make it easy to right one liners
		// is just lazy, but laziness is a virtue.

		public static void ForEach<T, U>(this Dictionary<T, U> d, Action<KeyValuePair<T, U>> a) {
			foreach (KeyValuePair<T, U> p in d) { a(p); }
		}

		public static void ForEach<T, U>(this Dictionary<T, U>.KeyCollection k, Action<T> a) {
			foreach (T t in k) { a(t); }
		}

		public static void ForEach<T, U>(this Dictionary<T, U>.ValueCollection v, Action<U> a) {
			foreach (U u in v) { a(u); }
		}
	}
}
