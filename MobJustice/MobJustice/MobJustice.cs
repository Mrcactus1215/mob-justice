using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Terraria.Localization;
using Extensions;
using Microsoft.Xna.Framework;

namespace MobJustice
{
	[ApiVersion(2, 1)]
	public class MobJustice : TerrariaPlugin
	{
		// ------------------------------
		// Meta data
		// ------------------------------

		public override string Author
		{
			get { return "Hextator and MrCactus"; }
		}

		public override string Description
		{
			get { return "Plugin that allows the players to lynch other players"; }
		}

		public override string Name
		{
			get { return "Mob Justice"; }
		}

		public MobJustice(Main game) : base(game)
		{
			// Load priority; smaller numbers load earlier
			this.Order = 5;
		}

		// ------------------------------
		// Init and unload
		// ------------------------------

		public override void Initialize()
		{
			// Methods to perform when plugin is initializing i.e. hooks
			ServerApi.Hooks.GameInitialize.Register(this, this.Game_Initialize);
			TShockAPI.Hooks.GeneralHooks.ReloadEvent += this.OnReload;
			ServerApi.Hooks.ServerJoin.Register(this, this.OnPlayerJoin);
			ServerApi.Hooks.ServerLeave.Register(this, this.OnPlayerLeave);
			ServerApi.Hooks.NetGetData.Register(this, this.OnGetNetData);

			// Config init
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Methods to perform when the Plugin is disposed i.e. unhooks
				ServerApi.Hooks.GameInitialize.Deregister(this, this.Game_Initialize);
				TShockAPI.Hooks.GeneralHooks.ReloadEvent -= this.OnReload;
				ServerApi.Hooks.ServerJoin.Deregister(this, this.OnPlayerJoin);
				ServerApi.Hooks.ServerLeave.Deregister(this, this.OnPlayerLeave);
				ServerApi.Hooks.NetGetData.Deregister(this, this.OnGetNetData);
			}
		}

		// ------------------------------
		// Hooked events
		// ------------------------------

		private void Game_Initialize(EventArgs args) {
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.force" }, this.ForceLynch, "forcelynchable"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.lynch" }, this.VoteLynch, "lynch"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.list" }, this.LynchList, "lynchlist"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.listvotes" }, this.LynchVoteList, "lynchvotelist"));
			// TODO: Make /lynch display all commands but only display the commands the being that ran the command has permissions for
		}

		private void OnReload(TShockAPI.Hooks.ReloadEventArgs args) {
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
			args?.Player?.SendSuccessMessage("[MobJustice] Successfully reloaded config.");
		}

		private void OnPlayerJoin(JoinEventArgs args) {
			// HashSet<string> lynchableRefs should also be modified by login/logout,
			// and should match lynchables (if name is in lynchables then the player with that name should be in lynchableRefs)
			TSPlayer player = TShock.Players[args.Who];
			if (null == player) {
				return;
			}
			// TODO: Also make them lynchable if they rejoin while voted for
			if (this.config.savedLynchables.Contains(player.Name)) {
				this.SetPvP(player);
				TSPlayer.All.SendMessage(String.Format("{0}'s PvP has been forcefully turned on", player.Name), 255, 255, 255);
			}
		}

		public void OnPlayerLeave(LeaveEventArgs args) {
			// If a player leaves, they forfeit their lynch votes
			string voterName = TShock.Players[args.Who].Name;
			foreach (string targetName in this.playerLynchVotes.Get(voterName, new HashSet<string>())) {
				this.votesCounter[targetName] -= 1;
			}
			this.playerLynchVotes.Remove(voterName);

		}

		private void OnGetNetData(GetDataEventArgs args) {
			PacketTypes packetType = args.MsgID;
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			if (null == player) {
				return;
			}
			if (!this.config.savedLynchables.Contains(player.Name) && !this.IsVotedLynchTarget(player.Name)) {
				return;
			}
			// Player is lynchable; override attempts to set PvP or team state accordingly
			switch (packetType) {
				case PacketTypes.TogglePvp:
					this.SetPvP(player);
					player.SendMessage(
						String.Format("{0}", this.config.message),
						this.config.messagered, this.config.messagegreen, this.config.messageblue
					);
					args.Handled = true;
					break;
				// This packet is stupidly weird and bad. Why would it be called PlayerTeam when there's a packet called ToggleParty that doesn't work the way that the name suggests..
				case PacketTypes.PlayerTeam:
					this.SetTeam(player);
					player.SendMessage(
						String.Format("{0}", this.config.teamMessage),
						this.config.teamMessageRed, this.config.teamMessageGreen, this.config.teamMessageBlue
					);
					// These handled thingies really worked out, it doesn't spam the chat anymore when you toggle...10/10
					args.Handled = true;
					break;
			}
		}

		// ------------------------------
		// Plugin vars
		// ------------------------------

		private Config.ConfigData config;
		// HashSet<string> lynchables should only be modified by commands
		// Actually using config.savedLynchables instead now
		//private HashSet<string> lynchables = new HashSet<string>();
		Dictionary<string, HashSet<string>> playerLynchVotes = new Dictionary<string, HashSet<string>>();
		Dictionary<string, int> votesCounter = new Dictionary<string, int>();
		Dictionary<string, DateTime> lynchableTimes = new Dictionary<string, DateTime>();

		// ------------------------------
		// Plugin funcs
		// ------------------------------

		private void UpdateLynchRefs() {
			foreach (TSPlayer player in TShock.Players) {
				if (null == player) {
					continue;
				}
				if (this.config.savedLynchables.Contains(player.Name) || this.IsVotedLynchTarget(player.Name)) {
					this.SetTeam(player);
					this.SetPvP(player);
				}
			}
		}

		private void SendDataHelper(TSPlayer player, PacketTypes packetType) {
			player.SendData(packetType, "", player.Index);
			TSPlayer.All.SendData(packetType, "", player.Index);
		}

		private void SetPvP(TSPlayer player) {
			player.TPlayer.hostile = true;
			this.SendDataHelper(player, PacketTypes.TogglePvp);
		}

		private void ClearPvP(TSPlayer player) {
			player.TPlayer.hostile = false;
			this.SendDataHelper(player, PacketTypes.TogglePvp);
		}

		private void SetTeam(TSPlayer player)
		{
			player.SetTeam(0);
			this.SendDataHelper(player, PacketTypes.ToggleParty);
		}

		private void RemoveLynchVotesFor(string targetName)
		{
			this.playerLynchVotes.ForEach(kvpLyncher => kvpLyncher.Value.Remove(targetName));
			this.votesCounter.Remove(targetName);
		}

		public bool IsVictimLynchCooling(string targetName)
		{
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchableTimes.Get(targetName, DateTime.Now - lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime >= TimeSpan.FromSeconds(this.config.lynchDuration) && !this.IsVictimLynchingCooled(targetName);
		}

		public bool IsVictimLynchingCooled(string targetName)
		{
			// By default, the player is assumed to be able to be voted into the lynchable state
			TimeSpan lynchCycleDuration = TimeSpan.FromSeconds(this.config.lynchDuration + this.config.lynchCooldown);
			DateTime lastLynchTime = this.lynchableTimes.Get(targetName, DateTime.Now - lynchCycleDuration);
			TimeSpan elapsedLynchTime = DateTime.Now - lastLynchTime;
			return elapsedLynchTime > lynchCycleDuration;
		}

		bool IsVotedLynchTarget(string targetName)
		{
			int currVotes = this.votesCounter.Get(targetName, 0);
			int currPlayercount = TShock.Utils.GetActivePlayerCount();
			//TSPlayer.All.SendMessage("Target " + targetName + " has " + currVotes + " votes against them currently.", 255, 255, 0);
			//TSPlayer.All.SendMessage("There are presently " + currPlayercount + " players connected.", 255, 255, 0);
			bool enoughVotes = (3 <= currPlayercount) && (currVotes > currPlayercount/2);
			return enoughVotes && !this.IsVictimLynchCooling(targetName);
		}

		private void LynchManagementThreadAction(string targetName) {
			Thread.Sleep((int)this.config.lynchDuration * 1000);
			var player = TSPlayer.FindByNameOrID(targetName);
			this.RemoveLynchVotesFor(targetName);
			if (!this.config.savedLynchables.Contains(targetName)) {
				TSPlayer.All.SendMessage(targetName + " is no longer lynchable", Color.Yellow);
				this.ClearPvP(player[0]);
			}

		}

		public TSPlayer GetPlayerByName(string playerName) {
			var playerMatches = TSPlayer.FindByNameOrID(playerName);
			if (0 == playerMatches.Count) {
				throw new ArgumentException(String.Format("Invalid player! {0} not found", playerName));
			}
			if (1 < playerMatches.Count) {
				throw new ArgumentException(String.Format("More than one match found: {0}", String.Join(", ", playerMatches.Select(currPlayer => currPlayer.Name))));
			}
			return playerMatches[0];
		}

		// Negative means "infinite"
		// 0 will make commands with no arguments refuse to function if arguments are supplied
		public bool ArgCountCheck(CommandArgs args, int argCount, string usage) {
			// Not enough arguments
			int minArgs = (0 > argCount)? 1 : argCount;
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
				victimCandidate = this.GetPlayerByName(args.Parameters[0]);
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
					String.Format("{0}", this.config.lynchplayermessage.Replace("{PLAYER_NAME}", targetName)),
					this.config.lynchPlayerMessageRed, this.config.lynchPlayerMessageGreen, this.config.lynchPlayerMessageBlue
				);
				this.SetPvP(victimCandidate);
				this.SetTeam(victimCandidate);
			}
			else {
				this.config.savedLynchables.Remove(targetName);
				TSPlayer.All.SendMessage(
					String.Format("{0}", this.config.unlynchplayermessage.Replace("{PLAYER_NAME}", targetName)),
					this.config.unlynchPlayerMessageRed, this.config.unlynchPlayerMessageGreen, this.config.unlynchPlayerMessageBlue
				);
				if (!isVotedLynchable) {
					this.ClearPvP(victimCandidate);
				}
			}
			Config.SaveConfigData(this.config);
		}

		// Function for command: /lynch targetName
		public void VoteLynch(CommandArgs args)
		{
			if (!this.config.pluginenabled)
			{
				return;
			}

			if (!args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("You need to be logged in to do that!");
				return;
			}

			if (!this.ArgCountCheck(args, 1, "/lynch targetName")) {
				return;
			}

			TSPlayer victimCandidate;
			try {
				victimCandidate = this.GetPlayerByName(args.Parameters[0]);
			}
			catch (ArgumentException ae) {
				args.Player.SendErrorMessage(ae.Message);
				return;
			}

			string targetName = victimCandidate.Name;
			if (!this.IsVictimLynchingCooled(targetName))
			{
				args.Player.SendErrorMessage("You can't change your vote for someone once they become lynchable.");
				return;
			}
			HashSet<string> currPlayerVotes = this.playerLynchVotes.Get(args.Player.Name, new HashSet<string>());
			int currVoteCount = this.votesCounter.Get(targetName, 0);
			bool wasVotedLynchable = this.IsVotedLynchTarget(targetName);
			if (currPlayerVotes.Contains(targetName))
			{
				currPlayerVotes.Remove(targetName);
				currVoteCount -= 1;
				args.Player.SendSuccessMessage("Your vote for {0} to be lynched was removed.", targetName);
			}
			else
			{
				currPlayerVotes.Add(targetName);
				currVoteCount += 1;
				args.Player.SendSuccessMessage("You voted for {0} to be lynched.", targetName);
			}
			this.playerLynchVotes[args.Player.Name] = currPlayerVotes;
			this.votesCounter[targetName] = currVoteCount;
			bool isVotedLynchTarget = this.IsVotedLynchTarget(targetName);
			if (isVotedLynchTarget)
			{
				if (!wasVotedLynchable)
				{
					this.lynchableTimes[targetName] = DateTime.Now;
					Thread lynchExpirationThread = new Thread(() => this.LynchManagementThreadAction(targetName));
					lynchExpirationThread.Start();
					TSPlayer.All.SendMessage(targetName + " has been voted to be lynched. You have " + this.config.lynchDuration + " seconds to lynch them as much as possible.", Color.Yellow);
				}
				this.SetPvP(victimCandidate);
				this.SetTeam(victimCandidate);
			}
		}

		// Function for command: /lynchlist
		// Command added per request of Thiefman also known as Medium Roast Steak or Stealownz
		public void LynchList(CommandArgs args) {
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

		// Function for command: /lynchvotelist
		public void LynchVoteList(CommandArgs args) {
			if (!this.config.pluginenabled) {
				return;
			}

			string victimList = String.Format("Victims: {0}", String.Join(", ", this.votesCounter.Where(kvpVictim => 0 != kvpVictim.Value).Select(kvpVictim => kvpVictim.Key + ": " + kvpVictim.Value)));
			string voterList = String.Format("Lynchers: {0}", String.Join(", ", this.playerLynchVotes.Where(kvpLyncher => 0 != kvpLyncher.Value.Count).Select(kvpLyncher => kvpLyncher.Key + ": " + kvpLyncher.Value.Count)));
			args.Player.SendMessage(String.Format("{0}", victimList), 255, 255, 0);
			args.Player.SendMessage(String.Format("{0}", voterList), 255, 255, 0);
		}
	}
}
