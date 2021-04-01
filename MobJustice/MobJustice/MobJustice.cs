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

	public enum LynchVoteResult {
		VotedProtect,
		VotedLynch,
		StartedLynch
	}

	public class MobJusticeStatus {
		private readonly object lockObj = new object();
		private readonly Dictionary<string, LynchState> playerLynchStates = new Dictionary<string, LynchState>();
		private readonly Dictionary<string, HashSet<string>> playerLynchVotes = new Dictionary<string, HashSet<string>>();
		private readonly Dictionary<string, int> votesCounter = new Dictionary<string, int>();

		// All of the methods here that are marked with "Unsafe"
		// MUST NOT use any mutex locking, as they may be
		// called in sequence

		private LynchState UnsafeGetLynchState(string targetName) {
			return this.playerLynchStates.Get(targetName, LynchState.Lynchable);
		}

		private LynchState UnsafeSetLynchState(string targetName, LynchState newState) {
			return this.playerLynchStates[targetName] = newState;
		}

		private HashSet<string> UnsafeGetPlayerVotes(string voterName) {
			return this.playerLynchVotes.Get(voterName, new HashSet<string>());
		}

		private bool UnsafeIsVotingFor(string voterName, string targetName) {
			return this.UnsafeGetPlayerVotes(voterName).Contains(targetName);
		}

		private int UnsafeGetNumVotesAgainst(string targetName) {
			return this.votesCounter.Get(targetName, 0);
		}

		private LynchVoteResult UnsafeVoteToLynch(string voterName, string targetName, uint votesToLynch) {
			LynchVoteResult voteResult = LynchVoteResult.VotedLynch;
			HashSet<string> currPlayerVotes = this.UnsafeGetPlayerVotes(voterName);
			int currVoteCount = this.UnsafeGetNumVotesAgainst(targetName);
			bool wasContained = currPlayerVotes.Contains(targetName);
			if (!wasContained) {
				currPlayerVotes.Add(targetName);
				currVoteCount += 1;
			}
			bool enoughVotes = currVoteCount >= votesToLynch;
			if (enoughVotes && LynchState.Vulnerable == this.UnsafeGetLynchState(targetName)) {
				this.UnsafeSetLynchState(targetName, LynchState.Lynchable);
				voteResult = LynchVoteResult.StartedLynch;
			}
			this.playerLynchVotes[voterName] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
			return voteResult;
		}

		private LynchVoteResult UnsafeVoteToNotLynch(string voterName, string targetName) {
			HashSet<string> currPlayerVotes = this.UnsafeGetPlayerVotes(voterName);
			int currVoteCount = this.UnsafeGetNumVotesAgainst(targetName);
			bool wasContained = currPlayerVotes.Contains(targetName);
			if (wasContained) {
				currPlayerVotes.Remove(targetName);
				currVoteCount -= 1;
			}
			this.playerLynchVotes[voterName] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
			return LynchVoteResult.VotedProtect;
		}

		// All of the methods here that are not marked with "Unsafe"
		// need to have locks implemented for thread safety
		// As such, they also MUST NOT call each other

		public bool IsVotedLynchTarget(string targetName) {
			bool output;
			lock (this.lockObj) {
				output = LynchState.Lynchable == this.UnsafeGetLynchState(targetName);
			}
			return output;
		}

		public void AdvanceLynchState(string targetName) {
			lock (this.lockObj) {
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
		}

		public LynchVoteResult VoteToLynch(string voterName, string targetName, uint votesToLynch) {
			LynchVoteResult voteResult;
			lock (this.lockObj) {
				voteResult = this.UnsafeVoteToLynch(voterName, targetName, votesToLynch);
			}
			return voteResult;
		}

		public LynchVoteResult VoteToNotLynch(string voterName, string targetName) {
			LynchVoteResult voteResult;
			lock (this.lockObj) {
				voteResult = this.UnsafeVoteToNotLynch(voterName, targetName);
			}
			return voteResult;
		}

		public LynchVoteResult ToggleLynchVote(string voterName, string targetName, uint votesToLynch) {
			LynchVoteResult voteResult;
			lock (this.lockObj) {
				if (this.UnsafeIsVotingFor(voterName, targetName)) {
					voteResult = this.UnsafeVoteToNotLynch(voterName, targetName);
				}
				else {
					voteResult = this.UnsafeVoteToLynch(voterName, targetName, votesToLynch);
				}
			}
			return voteResult;
		}

		public void ForfeitVotes(string voterName) {
			lock (this.lockObj) {
				this.UnsafeGetPlayerVotes(voterName).ForEach(targetName => this.votesCounter[targetName] -= 1);
				this.playerLynchVotes.Remove(voterName);
			}
		}

		public void RemoveLynchVotesFor(string targetName) {
			lock (this.lockObj) {
				this.playerLynchVotes.ForEach(kvpLyncher => kvpLyncher.Value.Remove(targetName));
				this.votesCounter.Remove(targetName);
			}
		}

		public List<string> LynchVoteInfo() {
			List<string> output;
			lock (this.lockObj) {
				output = new List<string>() {
					String.Join(", ", this.votesCounter.Where(kvpVictim => 0 != kvpVictim.Value).Select(kvpVictim => kvpVictim.Key + ": " + kvpVictim.Value)),
					String.Join(", ", this.playerLynchVotes.Where(kvpLyncher => 0 != kvpLyncher.Value.Count).Select(kvpLyncher => kvpLyncher.Key + ": " + kvpLyncher.Value.Count))
				};
			}
			return output;
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
			this.UpdateForcedTeamAndPvP();
		}

		public bool IsLynchable(string targetName) {
			return this.config.IsForcedLynchable(targetName) || this.lynchState.IsVotedLynchTarget(targetName);
		}

		public void UpdateForcedTeamAndPvP() {
			foreach (TSPlayer player in TShock.Players) {
				if (null == player) {
					continue;
				}
				if (this.IsLynchable(player.Name)) {
					TShockExtensions.TShockExtensions.SetTeam(player);
					TShockExtensions.TShockExtensions.SetPvP(player);
				}
			}
		}

		private void LynchManagementThreadAction(string targetName) {
			TSPlayer.All.SendMessage(targetName + " has been voted to be lynched. You have " + this.config.lynchDuration + " seconds to lynch them as much as possible.", Color.Red);
			Thread.Sleep((int)this.config.lynchDuration * 1000);
			this.lynchState.AdvanceLynchState(targetName);

			if (0 < this.config.lynchCooldown) {
				TSPlayer.All.SendMessage(targetName + " is no longer able to be lynched. They will be safe for the next " + this.config.lynchCooldown + " seconds.", Color.Green);
			}
			Thread.Sleep((int)this.config.lynchCooldown * 1000);
			this.lynchState.AdvanceLynchState(targetName);

			TSPlayer victimCandidate = null;
			try {
				victimCandidate = TShockExtensions.TShockExtensions.GetPlayerByName(targetName);
			}
			catch (ArgumentException) { }

			this.lynchState.RemoveLynchVotesFor(targetName);
			if (!this.config.IsForcedLynchable(targetName)) {
				TSPlayer.All.SendMessage(targetName + " is no longer lynchable", Color.Yellow);
				if (null != victimCandidate) {
					TShockExtensions.TShockExtensions.ClearPvP(victimCandidate);
				}
			}
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

			if (!TShockExtensions.TShockExtensions.ArgCountCheck(args, 1, "/forcelynchable targetName")) {
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
			LynchVoteResult forceLynchResult = this.config.ToggleLynchForce(targetName);
			bool isVotedLynchable = this.lynchState.IsVotedLynchTarget(targetName);
			if (LynchVoteResult.StartedLynch == forceLynchResult) {
				TSPlayer.All.SendMessage(
					String.Format("{0}", this.config.lynchPlayerMessage.Replace("{PLAYER_NAME}", targetName)),
					this.config.lynchPlayerMessageRed, this.config.lynchPlayerMessageGreen, this.config.lynchPlayerMessageBlue
				);
				TShockExtensions.TShockExtensions.SetPvP(victimCandidate);
				TShockExtensions.TShockExtensions.SetTeam(victimCandidate);
			}
			else if (LynchVoteResult.VotedProtect == forceLynchResult) {
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
		// TODO: Make this display all of the commands that the command user has permissions for
		// if no arguments are given
		public void VoteLynch(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			if (!args.Player.IsLoggedIn) {
				args.Player.SendErrorMessage("You need to be logged in to do that!");
				return;
			}

			if (!TShockExtensions.TShockExtensions.ArgCountCheck(args, 1, "/lynch targetName")) {
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
			int currPlayerCount = TShock.Utils.GetActivePlayerCount();
			int ratioScaledPlayerCount = (int)Math.Ceiling(this.config.lynchingPlayersRatio * currPlayerCount);
			uint votesNeeded = Math.Max(this.config.minPlayersForLynch, (uint)ratioScaledPlayerCount);
			LynchVoteResult voteResult = this.lynchState.ToggleLynchVote(voterName, targetName, votesNeeded);
			if (LynchVoteResult.VotedProtect == voteResult) {
				args.Player.SendSuccessMessage("Your vote for {0} to be lynched was removed.", targetName);
			}
			else {
				args.Player.SendSuccessMessage("You voted for {0} to be lynched.", targetName);
				if (LynchVoteResult.StartedLynch == voteResult) {
					Thread lynchManagementThread = new Thread(() => this.LynchManagementThreadAction(targetName));
					lynchManagementThread.Start();
					TShockExtensions.TShockExtensions.SetPvP(victimCandidate);
					TShockExtensions.TShockExtensions.SetTeam(victimCandidate);
				}
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
					String.Join(", ", this.config.CurrentForcedLynchables())
				),
				255, 255, 0
			);
		}

		// Function for command: /showlynchvotes
		public void ReportLynchVoteStates(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			List<string> lynchVoteInfo = this.lynchState.LynchVoteInfo();
			string victimList = String.Format("Victims: {0}", lynchVoteInfo[0]);
			string voterList = String.Format("Lynchers: {0}", lynchVoteInfo[1]);
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
		}

		public void OnReload(TShockAPI.Hooks.ReloadEventArgs args) {
			this.config = Config.GetConfigData();
			this.UpdateForcedTeamAndPvP();
			args?.Player?.SendSuccessMessage("[" + PLUGIN_NAME + "] Successfully reloaded config.");
		}

		public void OnPlayerJoin(JoinEventArgs args) {
			TSPlayer player = TShock.Players[args.Who];
			if (null == player) {
				return;
			}
			if (this.IsLynchable(player.Name)) {
				TShockExtensions.TShockExtensions.SetPvP(player);
				TSPlayer.All.SendMessage(String.Format("{0}'s PvP has been forcefully turned on.", player.Name), 255, 255, 255);
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

			if (!this.IsLynchable(player.Name)) {
				return;
			}

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
