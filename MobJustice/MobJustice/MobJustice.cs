using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Extensions;

using Microsoft.Xna.Framework;

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

		public HashSet<string> GetPlayerVotes(string voterName) {
			return this.playerLynchVotes.Get(voterName, new HashSet<string>());
		}
		
		public int GetNumVotesAgainst(string targetName) {
			return this.votesCounter.Get(targetName, 0);
		}

		public DateTime GetLastLynchTime(string targetName, TimeSpan lynchCycleDuration) {
			return this.lynchableTimes.Get(targetName, DateTime.Now - lynchCycleDuration);
		}

		public bool IsVotingFor(string voterName, string targetName) {
			return this.GetPlayerVotes(voterName).Contains(targetName);
		}

		public bool VoteToLynch(string voterName, string targetName) {
			HashSet<string> currPlayerVotes = this.GetPlayerVotes(voterName);
			int currVoteCount = this.GetNumVotesAgainst(targetName);
			currPlayerVotes.Add(targetName);
			currVoteCount += 1;
			this.playerLynchVotes[voterName] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
			return false;
		}

		public void VoteToNotLynch(string voterName, string targetName) {
			HashSet<string> currPlayerVotes = this.GetPlayerVotes(voterName);
			int currVoteCount = this.GetNumVotesAgainst(targetName);
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

		public Config.ConfigData config;
		public MobJusticeStatus lynchState;

		// ------------------------------
		// Plugin funcs
		// ------------------------------

		/*
		// Nothing to do here
		public MobJusticeEnforcer() { }
		*/

		public void UpdateLynchRefs() {
			foreach (TSPlayer player in TShock.Players) {
				if (null == player) {
					continue;
				}
				if (this.config.savedLynchables.Contains(player.Name) || this.IsVotedLynchTarget(player.Name)) {
					TShockExtensions.TShockExtensions.SetTeam(player);
					TShockExtensions.TShockExtensions.SetPvP(player);
				}
			}
		}

		public bool IsVictimLynchCooling(string targetName) {
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchState.GetLastLynchTime(targetName, lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime >= TimeSpan.FromSeconds(this.config.lynchDuration) && !this.IsVictimLynchingCooled(targetName);
		}

		public bool IsVictimLynchingCooled(string targetName) {
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchState.GetLastLynchTime(targetName, lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime > lynchCycleDuration;
		}

		public bool IsVotedLynchTarget(string targetName) {
			int currVotes = this.lynchState.GetNumVotesAgainst(targetName);
			int currPlayercount = TShock.Utils.GetActivePlayerCount();
			//TSPlayer.All.SendMessage("Target " + targetName + " has " + currVotes + " votes against them currently.", 255, 255, 0);
			//TSPlayer.All.SendMessage("There are presently " + currPlayercount + " players connected.", 255, 255, 0);
			bool enoughVotes = (3 <= currPlayercount) && (currVotes > currPlayercount/2);
			return enoughVotes && !this.IsVictimLynchCooling(targetName);
		}

		private void LynchManagementThreadAction(string targetName) {
			Thread.Sleep((int)this.config.lynchDuration * 1000);

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
			bool isVotedLynchable = this.IsVotedLynchTarget(targetName);
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
			bool wasVotedLynchable = this.IsVotedLynchTarget(targetName);
			if (this.lynchState.IsVotingFor(voterName, targetName)) {
				this.lynchState.VoteToNotLynch(voterName, targetName);
				args.Player.SendSuccessMessage("Your vote for {0} to be lynched was removed.", targetName);
			}
			else {
				this.lynchState.VoteToLynch(voterName, targetName);
				args.Player.SendSuccessMessage("You voted for {0} to be lynched.", targetName);
			}
			bool isVotedLynchTarget = this.IsVotedLynchTarget(targetName);
			if (isVotedLynchTarget) {
				if (!wasVotedLynchable) {
					this.lynchState.SetLynchTime(targetName, DateTime.Now);
					Thread lynchExpirationThread = new Thread(() => this.LynchManagementThreadAction(targetName));
					lynchExpirationThread.Start();
					TSPlayer.All.SendMessage(targetName + " has been voted to be lynched. You have " + this.config.lynchDuration + " seconds to lynch them as much as possible.", Color.Yellow);
				}
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
	}
}
