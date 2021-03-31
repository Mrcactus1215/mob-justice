using System;
using System.Linq;

using TShockAPI;

namespace TShockExtensions {
	public static class TShockExtensions {
		public static TSPlayer GetPlayerByName(string playerName) {
			var playerMatches = TSPlayer.FindByNameOrID(playerName);
			if (0 == playerMatches.Count) {
				throw new ArgumentException(String.Format("Invalid player! {0} not found", playerName));
			}
			if (1 < playerMatches.Count) {
				throw new ArgumentException(String.Format("More than one match found: {0}", String.Join(", ", playerMatches.Select(currPlayer => currPlayer.Name))));
			}
			return playerMatches[0];
		}
	}
}
