using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Extensions;

using Microsoft.Xna.Framework;

using TerrariaApi.Server;
using TShockAPI;

namespace MobJustice {
	// ------------------------------
	// Plugin types
	// ------------------------------

	public enum LynchState {
		Vulnerable,
		Lynchable,
		Protected
	}

	public class MobJusticeStatus {
		private readonly Dictionary<string, LynchState> playerLynchStates = new Dictionary<string, LynchState>();

		private readonly Dictionary<string, HashSet<string>> playerLynchVotes = new Dictionary<string, HashSet<string>>();
		private readonly Dictionary<string, int> votesCounter = new Dictionary<string, int>();

		private readonly Dictionary<string, DateTime> lynchableTimes = new Dictionary<string, DateTime>();

		private LynchState UnsafeGetLynchState(string targetName) {
			return this.playerLynchStates.Get(targetName, LynchState.Lynchable);
		}

		private LynchState UnsafeSetLynchState(string targetName, LynchState newState) {
			return this.playerLynchStates[targetName] = newState;
		}

		private HashSet<string> UnsafeGetPlayerVotes(string voterName) {
			return this.playerLynchVotes.Get(voterName, new HashSet<string>());
		}
		
		private int UnsafeGetNumVotesAgainst(string targetName) {
			return this.votesCounter.Get(targetName, 0);
		}

		public DateTime UnsafeGetLastLynchTime(string targetName, TimeSpan lynchCycleDuration) {
			return this.lynchableTimes.Get(targetName, DateTime.Now - lynchCycleDuration);
		}

		public void AdvanceLynchState(string targetName) {
			LynchState oldState = this.UnsafeGetLynchState(targetName);
			LynchState newState;
			switch (oldState) {
				case LynchState.Lynchable:
					newState = LynchState.Protected;
					break;
				case LynchState.Protected:
					newState = LynchState.Vulnerable;
					break;
				default:
					newState = LynchState.Lynchable;
					break;
			}
			this.UnsafeSetLynchState(targetName, newState);
		}

		public bool IsVotingFor(string voterName, string targetName) {
			return this.UnsafeGetPlayerVotes(voterName).Contains(targetName);
		}

		public bool IsVotedLynchTarget(string targetName) {
			return LynchState.Lynchable == this.UnsafeGetLynchState(targetName);
		}

		public bool VoteToLynch(string voterName, string targetName, bool allowLynch) {
			bool newlyLynchable = false;
			HashSet<string> currPlayerVotes = this.UnsafeGetPlayerVotes(voterName);
			int currVoteCount = this.UnsafeGetNumVotesAgainst(targetName);
			currPlayerVotes.Add(targetName);
			currVoteCount += 1;
			int currPlayercount = TShock.Utils.GetActivePlayerCount();
			bool enoughVotes = currVoteCount > (currPlayercount/2);
			if (allowLynch && enoughVotes && LynchState.Vulnerable == this.UnsafeGetLynchState(targetName)) {
				this.UnsafeSetLynchState(targetName, LynchState.Lynchable);
				newlyLynchable = true;
			}
			this.playerLynchVotes[voterName] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
			return newlyLynchable;
		}

		public void VoteToNotLynch(string voterName, string targetName) {
			HashSet<string> currPlayerVotes = this.UnsafeGetPlayerVotes(voterName);
			int currVoteCount = this.UnsafeGetNumVotesAgainst(targetName);
			currPlayerVotes.Remove(targetName);
			currVoteCount -= 1;
			this.playerLynchVotes[voterName] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
		}

		public void ForfeitVotes(string voterName) {
			foreach (string targetName in this.playerLynchVotes.Get(voterName, new HashSet<string>())) {
				this.votesCounter[targetName] -= 1;
			}
			this.playerLynchVotes.Remove(voterName);
		}

		public void RemoveLynchVotesFor(string targetName) {
			this.playerLynchVotes.ForEach(kvpLyncher => kvpLyncher.Value.Remove(targetName));
			this.votesCounter.Remove(targetName);
		}

		public void SetLynchTime(string targetName, DateTime lynchTime) {
			this.lynchableTimes[targetName] = DateTime.Now;
		}

		public string VictimInfo() {
			return String.Join(", ", this.votesCounter.Where(kvpVictim => 0 != kvpVictim.Value).Select(kvpVictim => kvpVictim.Key + ": " + kvpVictim.Value));
		}

		public string VoterInfo() {
			return String.Join(", ", this.playerLynchVotes.Where(kvpLyncher => 0 != kvpLyncher.Value.Count).Select(kvpLyncher => kvpLyncher.Key + ": " + kvpLyncher.Value.Count));
		}
	}

	public class MobJusticeEnforcer {
		// ------------------------------
		// Plugin vars
		// ------------------------------

		public const string PLUGIN_NAME = "Mob Justice"; 
		public Config.ConfigData config;
		public MobJusticeStatus lynchState;

		// ------------------------------
		// Plugin funcs
		// ------------------------------

		public MobJusticeEnforcer() {
			// Config init
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
		}

		public void UpdateLynchRefs() {
			foreach (TSPlayer player in TShock.Players) {
				if (null == player) {
					continue;
				}
				if (this.config.savedLynchables.Contains(player.Name) || this.lynchState.IsVotedLynchTarget(player.Name)) {
					TShockExtensions.TShockExtensions.SetTeam(player);
					TShockExtensions.TShockExtensions.SetPvP(player);
				}
			}
		}

		public bool IsVictimLynchCooling(string targetName) {
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchState.UnsafeGetLastLynchTime(targetName, lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime >= TimeSpan.FromSeconds(this.config.lynchDuration) && !this.IsVictimLynchingCooled(targetName);
		}

		public bool IsVictimLynchingCooled(string targetName) {
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchState.UnsafeGetLastLynchTime(targetName, lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime > lynchCycleDuration;
		}

		private void LynchManagementThreadAction(string targetName) {
			TSPlayer.All.SendMessage(targetName + " has been voted to be lynched. You have " + this.config.lynchDuration + " seconds to lynch them as much as possible.", Color.Red);
			Thread.Sleep((int)this.config.lynchDuration * 1000);
			this.lynchState.AdvanceLynchState(targetName);
			
			TSPlayer.All.SendMessage(targetName + " is no longer able to be lynched. They will be safe for the next " + this.config.lynchCooldown + " seconds.", Color.Green);
			Thread.Sleep((int)this.config.lynchCooldown * 1000);
			this.lynchState.AdvanceLynchState(targetName);

			TSPlayer victimCandidate = null;
			try {
				victimCandidate = TShockExtensions.TShockExtensions.GetPlayerByName(targetName);
			}
			catch (ArgumentException) { }

			this.lynchState.RemoveLynchVotesFor(targetName);
			if (!this.config.savedLynchables.Contains(targetName)) {
				TSPlayer.All.SendMessage(targetName + " is no longer lynchable", Color.Yellow);
				if (null != victimCandidate) {
					TShockExtensions.TShockExtensions.ClearPvP(victimCandidate);
				}
			}
		}

		// Negative means "infinite"
		// 0 will make commands with no arguments refuse to function if arguments are supplied
		public bool ArgCountCheck(CommandArgs args, int argCount, string usage) {
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

		// ------------------------------
		// Plugin commands
		// ------------------------------

		// Function for command: /forcelynchable targetName
		public void ForceLynch(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			if (!args.Player.IsLoggedIn) {
				args.Player.SendErrorMessage("You need to be logged in to do that!");
				return;
			}

			if (!this.ArgCountCheck(args, 1, "/forcelynchable targetName")) {
				return;
			}

			TSPlayer victimCandidate;
			try {
				victimCandidate = TShockExtensions.TShockExtensions.GetPlayerByName(args.Parameters[0]);
			}
			catch (ArgumentException ae) {
				args.Player.SendErrorMessage(ae.Message);
				return;
			}

			string targetName = victimCandidate.Name;
			bool lynchable = this.config.savedLynchables.Contains(targetName);
			bool isVotedLynchable = this.lynchState.IsVotedLynchTarget(targetName);
			if (!lynchable) {
				this.config.savedLynchables.Add(targetName);
				TSPlayer.All.SendMessage(
					String.Format("{0}", this.config.lynchPlayerMessage.Replace("{PLAYER_NAME}", targetName)),
					this.config.lynchPlayerMessageRed, this.config.lynchPlayerMessageGreen, this.config.lynchPlayerMessageBlue
				);
				TShockExtensions.TShockExtensions.SetPvP(victimCandidate);
				TShockExtensions.TShockExtensions.SetTeam(victimCandidate);
			}
			else {
				this.config.savedLynchables.Remove(targetName);
				TSPlayer.All.SendMessage(
					String.Format("{0}", this.config.unlynchPlayerMessage.Replace("{PLAYER_NAME}", targetName)),
					this.config.unlynchPlayerMessageRed, this.config.unlynchPlayerMessageGreen, this.config.unlynchPlayerMessageBlue
				);
				if (!isVotedLynchable) {
					TShockExtensions.TShockExtensions.ClearPvP(victimCandidate);
				}
			}
			Config.SaveConfigData(this.config);
		}

		// Function for command: /lynch targetName
		public void VoteLynch(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			if (!args.Player.IsLoggedIn) {
				args.Player.SendErrorMessage("You need to be logged in to do that!");
				return;
			}

			if (!this.ArgCountCheck(args, 1, "/lynch targetName")) {
				return;
			}

			TSPlayer victimCandidate;
			try {
				victimCandidate = TShockExtensions.TShockExtensions.GetPlayerByName(args.Parameters[0]);
			}
			catch (ArgumentException ae) {
				args.Player.SendErrorMessage(ae.Message);
				return;
			}

			string voterName = args.Player.Name;
			string targetName = victimCandidate.Name;
			if (!this.IsVictimLynchingCooled(targetName)) {
				args.Player.SendErrorMessage("You can't change your vote for someone once they become lynchable.");
				return;
			}
			bool newlyLynchable = false;
			if (this.lynchState.IsVotingFor(voterName, targetName)) {
				this.lynchState.VoteToNotLynch(voterName, targetName);
				args.Player.SendSuccessMessage("Your vote for {0} to be lynched was removed.", targetName);
			}
			else {
				newlyLynchable = this.lynchState.VoteToLynch(
					voterName, targetName,
					this.config.minPlayersForLynch <= (TShock.Utils.GetActivePlayerCount()/2)
				);
				args.Player.SendSuccessMessage("You voted for {0} to be lynched.", targetName);
			}
			bool isVotedLynchTarget = this.lynchState.IsVotedLynchTarget(targetName);
			if (newlyLynchable) {
				this.lynchState.SetLynchTime(targetName, DateTime.Now);
				Thread lynchManagementThread = new Thread(() => this.LynchManagementThreadAction(targetName));
				lynchManagementThread.Start();
				TShockExtensions.TShockExtensions.SetPvP(victimCandidate);
				TShockExtensions.TShockExtensions.SetTeam(victimCandidate);
			}
		}

		// Function for command: /showforcedlynches
		// Command added per request of Thiefman also known as Medium Roast Steak or Stealownz
		public void ReportForcedLynches(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			args.Player.SendMessage(
				String.Format("Players currently forced to be lynchable: {0}",
					String.Join(", ", this.config.savedLynchables.ToList())
				),
				255, 255, 0
			);
		}

		// Function for command: /showlynchvotes
		public void ReportLynchVoteStates(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			string victimList = String.Format("Victims: {0}", this.lynchState.VictimInfo());
			string voterList = String.Format("Lynchers: {0}", this.lynchState.VoterInfo());
			args.Player.SendMessage(String.Format("{0}", victimList), 255, 255, 0);
			args.Player.SendMessage(String.Format("{0}", voterList), 255, 255, 0);
		}

		// ------------------------------
		// Hooked events
		// ------------------------------

		public void Game_Initialize(EventArgs args) {
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.force" }, this.ForceLynch, "forcelynchable"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.lynch" }, this.VoteLynch, "lynch"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.reportforced" }, this.ReportForcedLynches, "showforcedlynches"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.reportvotes" }, this.ReportLynchVoteStates, "showlynchvotes"));
			// TODO: Make /lynch display all commands but only display the commands the being that ran the command has permissions for
		}

		public void OnReload(TShockAPI.Hooks.ReloadEventArgs args) {
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
			args?.Player?.SendSuccessMessage("[" + PLUGIN_NAME + "] Successfully reloaded config.");
		}

		public void OnPlayerJoin(JoinEventArgs args) {
			TSPlayer player = TShock.Players[args.Who];
			if (null == player) {
				return;
			}
			if (this.config.savedLynchables.Contains(player.Name) || this.lynchState.IsVotedLynchTarget(player.Name)) {
				TShockExtensions.TShockExtensions.SetPvP(player);
				TSPlayer.All.SendMessage(String.Format("{0}'s PvP has been forcefully turned on", player.Name), 255, 255, 255);
			}
		}

		public void OnPlayerLeave(LeaveEventArgs args) {
			// If a player leaves, they forfeit their lynch votes
			string voterName = TShock.Players[args.Who].Name;
			this.lynchState.ForfeitVotes(voterName);
		}

		public void OnGetNetData(GetDataEventArgs args) {
			PacketTypes packetType = args.MsgID;
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			if (null == player) {
				return;
			}
			if (!this.config.savedLynchables.Contains(player.Name) && !this.lynchState.IsVotedLynchTarget(player.Name)) {
				return;
			}
			// Player is lynchable; override attempts to set PvP or team state accordingly
			switch (packetType) {
				case PacketTypes.TogglePvp:
					TShockExtensions.TShockExtensions.SetPvP(player);
					player.SendMessage(
						String.Format("{0}", this.config.message),
						this.config.messageRed, this.config.messageGreen, this.config.messageBlue
					);
					args.Handled = true;
					break;
				// This packet is stupidly weird and bad. Why would it be called PlayerTeam when there's a packet called ToggleParty that doesn't work the way that the name suggests..
				case PacketTypes.PlayerTeam:
					TShockExtensions.TShockExtensions.SetTeam(player);
					player.SendMessage(
						String.Format("{0}", this.config.teamMessage),
						this.config.teamMessageRed, this.config.teamMessageGreen, this.config.teamMessageBlue
					);
					// These handled thingies really worked out, it doesn't spam the chat anymore when you toggle...10/10
					args.Handled = true;
					break;
			}
		}
	}
}
