using System;
using System.Collections.Generic;
using System.Linq;
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

		private Config.ConfigData config;
		// HashSet<string> lynchables should only be modified by commands
		// Actually using config.savedLynchables instead now
		//private HashSet<string> lynchables = new HashSet<string>();

		private void UpdateLynchRefs()
		{
			foreach (TSPlayer player in TShock.Players)
			{
				if (null == player)
				{
					continue;
				}
				if (this.config.savedLynchables.Contains(player.Name) || IsVotedLynchTarget(player.Name))
				{
					this.SetTeam(player);
					this.SetPVP(player);
				}
			}
		}

		public override void Initialize()
		{
			// Methods to perform when plugin is initializing i.e. hooks
			ServerApi.Hooks.GameInitialize.Register(this, this.Game_Initialize);
			ServerApi.Hooks.ServerJoin.Register(this, this.OnPlayerJoin);
			ServerApi.Hooks.NetGetData.Register(this, this.OnGetNetData);
			TShockAPI.Hooks.GeneralHooks.ReloadEvent += this.OnReload;
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
			ServerApi.Hooks.ServerLeave.Register(this, OnPlayerLeave);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Methods to perform when the Plugin is disposed i.e. unhooks
				ServerApi.Hooks.GameInitialize.Deregister(this, this.Game_Initialize);
				ServerApi.Hooks.ServerJoin.Deregister(this, this.OnPlayerJoin);
				ServerApi.Hooks.NetGetData.Deregister(this, this.OnGetNetData);
				TShockAPI.Hooks.GeneralHooks.ReloadEvent -= this.OnReload;
				ServerApi.Hooks.ServerLeave.Deregister(this, this.OnPlayerLeave);
			}
		}
		private void OnReload(TShockAPI.Hooks.ReloadEventArgs args)
		{
			this.config = Config.GetConfigData();
			this.UpdateLynchRefs();
			args?.Player?.SendSuccessMessage("[MobJustice] Successfully reloaded config.");
		}

		private void SetTeam(TSPlayer player)
		{
			player.SetTeam(0);
			player.SendData(PacketTypes.ToggleParty, "", player.Index);
			TSPlayer.All.SendData(PacketTypes.ToggleParty, "", player.Index);
		}

		private void SetPVP(TSPlayer player)
		{
			player.TPlayer.hostile = true;
			player.SendData(PacketTypes.TogglePvp, "", player.Index);
			TSPlayer.All.SendData(PacketTypes.TogglePvp, "", player.Index);
		}

		private void OnPlayerJoin(JoinEventArgs args)
		{
			// HashSet<string> lynchableRefs should also be modified by login/logout,
			// and should match lynchables (if name is in lynchables then the player with that name should be in lynchableRefs)
			TSPlayer player = TShock.Players[args.Who];
			if (null == player)
			{
				return;
			}
			if (this.config.savedLynchables.Contains(player.Name))
			{
				SetPVP(player);
				TSPlayer.All.SendMessage(String.Format("{0}'s PvP has been forcefully turned on", player.Name), 255, 255, 255);
			}
		}
		public void OnPlayerLeave(LeaveEventArgs args)
		{
			string voterName = TShock.Players[args.Who].Name;
			foreach (string targetName in playerLynchVotes.Get(voterName, new HashSet<string>()))
			{
				votesCounter[targetName] -= 1;
			}
			playerLynchVotes.Remove(voterName);

		}
		bool IsVotedLynchTarget(string targetName)
		{
			int currVotes = votesCounter.Get(targetName, 0);
			int currPlayercount = TShock.Utils.GetActivePlayerCount();
			//TSPlayer.All.SendMessage("Target " + targetName + " has " + currVotes + " votes against them currently.", 255, 255, 0);
			//TSPlayer.All.SendMessage("There are presently " + currPlayercount + " players connected.", 255, 255, 0);
			bool enoughVotes = currVotes > currPlayercount / 2;
			return enoughVotes;
		}
		private void OnGetNetData(GetDataEventArgs args)
		{
			PacketTypes packetType = args.MsgID;
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			if (null == player)
			{
				return;
			}
			if (!this.config.savedLynchables.Contains(player.Name) && !IsVotedLynchTarget(player.Name))
			{
				return;
			}
			switch (packetType)
			{
				case PacketTypes.TogglePvp:
					this.SetPVP(player);
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
					// These handled thingies really worked out, it doesn't spam the chat anymore when you toggle.. 10/10
					args.Handled = true;
					break;
			}
		}

		private void Game_Initialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.force" }, this.ForceLynch, "forcelynchable"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.list" }, this.LynchList, "lynchlist"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.lynch" }, this.VoteLynch, "lynch"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.listvotes" }, this.LynchVoteList, "lynchvotelist"));
			// TODO: Make /lynch display all commands but only display the commands the being that ran the command has permissions for
		}
		Dictionary<string, HashSet<string>> playerLynchVotes = new Dictionary<string, HashSet<string>>();
		Dictionary<string, int> votesCounter = new Dictionary<string, int>();
		// Command added per request of Thiefman also known as Medium Roast Steak or Stealownz
		public void LynchList(CommandArgs args)
		{
			if (!this.config.pluginenabled)
			{
				return;
			}
			args.Player.SendMessage(
				String.Format("Currently lynchable players: {0}",
					String.Join(", ", this.config.savedLynchables.ToList())
				),
				255, 255, 0
			);
		}
		public void LynchVoteList(CommandArgs args)
		{
			string victimList = String.Format("Victims: {0}", String.Join(", ", votesCounter.Where(kvpVictim => 0 != kvpVictim.Value).Select(kvpVictim => kvpVictim.Key + ": " + kvpVictim.Value)));
			string voterList = String.Format("Lynchers: {0}", String.Join(", ", playerLynchVotes.Where(kvpLyncher => 0 != kvpLyncher.Value.Count).Select(kvpLyncher => kvpLyncher.Key + ": " + kvpLyncher.Value.Count)));
			args.Player.SendMessage(String.Format("{0}", victimList), 255, 255, 0);
			args.Player.SendMessage(String.Format("{0}", voterList), 255, 255, 0);
		}
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
			if (1 < args.Parameters.Count)
			{
				args.Player.SendErrorMessage("Too many arguments! Proper syntax: /lynch <name>");
				return;
			}
			if (0 == args.Parameters.Count)
			{
				args.Player.SendErrorMessage("Arguments required! Proper syntax: /lynch <name>");
				return;
			}
			var playerMatches = TSPlayer.FindByNameOrID(args.Parameters[0]);
			string targetName = playerMatches[0].Name;
			if (0 == playerMatches.Count)
			{
				args.Player.SendErrorMessage("Invalid player! {0} not found", args.Parameters[0]);
				return;
			}
			if (1 < playerMatches.Count)
			{
				args.Player.SendErrorMessage(String.Format("More than one match found: {0}", String.Join(", ", playerMatches.Select(currPlayer => currPlayer.Name))));
				return;
			}
			HashSet<string> currPlayerVotes = playerLynchVotes.Get(args.Player.Name, new HashSet<string>());
			int currVoteCount = votesCounter.Get(targetName, 0);
			if (currPlayerVotes.Contains(targetName))
			{
				currPlayerVotes.Remove(targetName);
				currVoteCount -= 1;
				args.Player.SendSuccessMessage("Your vote for {0} to be lynched was removed", targetName);
			}
			else
			{
				currPlayerVotes.Add(targetName);
				currVoteCount += 1;
				args.Player.SendSuccessMessage("You voted for {0} to be lynched", targetName);
			}
			playerLynchVotes[args.Player.Name] = currPlayerVotes;
			votesCounter[targetName] = currVoteCount;
			if (IsVotedLynchTarget(playerMatches[0].Name))
			{
				TSPlayer.All.SendMessage(targetName + " has been voted to be lynched. You have 5 minutes", Color.Yellow);
				this.SetPVP(playerMatches[0]);
				this.SetTeam(playerMatches[0]);
			}
		}

		public void ForceLynch(CommandArgs args)
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

			if (1 < args.Parameters.Count)
			{
				args.Player.SendErrorMessage("Too many arguments! Proper syntax: /forcelynchable <name>");
				return;
			}
			if (0 == args.Parameters.Count)
			{
				args.Player.SendErrorMessage("Arguments required! Proper syntax: /forcelynchable <name>");
				return;
			}

			var playerMatches = TSPlayer.FindByNameOrID(args.Parameters[0]);
			if (0 == playerMatches.Count)
			{
				args.Player.SendErrorMessage("Invalid player! {0} not found", args.Parameters[0]);
				return;
			}
			if (1 < playerMatches.Count)
			{
				args.Player.SendErrorMessage(String.Format("More than one match found: {0}", String.Join(", ", playerMatches.Select(currPlayer => currPlayer.Name))));
				return;
			}
			bool lynchable = this.config.savedLynchables.Contains(playerMatches[0].Name);
			bool isVotedLynchable = IsVotedLynchTarget(playerMatches[0].Name);
			if (!lynchable)
			{
				this.config.savedLynchables.Add(playerMatches[0].Name);
				TSPlayer.All.SendMessage(String.Format("{0}", this.config.lynchplayermessage.Replace("{PLAYER_NAME}", playerMatches[0].Name)), this.config.lynchplayermessagered, this.config.lynchplayermessagegreen, this.config.lynchplayermessageblue);
				this.SetPVP(playerMatches[0]);
				this.SetTeam(playerMatches[0]);
			}
			else
			{
				this.config.savedLynchables.Remove(playerMatches[0].Name);
				TSPlayer.All.SendMessage(String.Format("{0}", this.config.unlynchplayermessage.Replace("{PLAYER_NAME}", playerMatches[0].Name)), this.config.unlynchplayermessagered, this.config.unlynchplayermessagegreen, this.config.unlynchplayermessageblue);
				if (!isVotedLynchable)
				{ 
					playerMatches[0].TPlayer.hostile = false;
					playerMatches[0].SendData(PacketTypes.TogglePvp, "", playerMatches[0].Index);
					TSPlayer.All.SendData(PacketTypes.TogglePvp, "", playerMatches[0].Index);
				}
			}
			Config.SaveConfigData(this.config);
		}
	}
}
