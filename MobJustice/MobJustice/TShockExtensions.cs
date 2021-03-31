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

		public static void SendDataHelper(TSPlayer player, PacketTypes packetType) {
			try {
				player.SendData(packetType, "", player.Index);
			}
			catch (Exception e) {
				Console.WriteLine("TShockExtensions.SendDataHelper exception: " + e.Message);
			}
			TSPlayer.All.SendData(packetType, "", player.Index);
		}

		public static void SetPvP(TSPlayer player) {
			try {
				player.TPlayer.hostile = true;
			}
			catch (Exception e) {
				Console.WriteLine("TShockExtensions.SetPvP exception: " + e.Message);
			}
			SendDataHelper(player, PacketTypes.TogglePvp);
		}

		public static void ClearPvP(TSPlayer player) {
			try {
				player.TPlayer.hostile = false;
			}
			catch (Exception e) {
				Console.WriteLine("TShockExtensions.ClearPvP exception: " + e.Message);
			}
			SendDataHelper(player, PacketTypes.TogglePvp);
		}

		public static void SetTeam(TSPlayer player) {
			try {
				player.SetTeam(0);
			}
			catch (Exception e) {
				Console.WriteLine("TShockExtensions.SetTeam exception: " + e.Message);
			}
			SendDataHelper(player, PacketTypes.ToggleParty);
		}
	}
}
