using System;
using System.Linq;

using TShockAPI;

namespace TShockExtensions {
	public static class TShockExtensions {
		// Negative means "infinite"
		// 0 will make commands with no arguments refuse to function if arguments are supplied
		public static bool ArgCountCheck(CommandArgs args, int argCount, string usage) {
			// Not enough arguments
			int minArgs = (0 > argCount) ? 1 : argCount;
			if (minArgs > args.Parameters.Count) {
				args.Player.SendErrorMessage("More arguments are required! Proper syntax:\n\t" + usage);
				return false;
			}
			// Too many arguments
			if (0 <= argCount && argCount < args.Parameters.Count) {
				args.Player.SendErrorMessage("Too many arguments! Proper syntax:\n\t" + usage);
				return false;
			}
			return true;
		}

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
