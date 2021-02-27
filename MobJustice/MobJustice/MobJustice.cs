

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;

namespace MobJustice
{

    [ApiVersion(2, 1)]
	public class MobJustice : TerrariaPlugin
	{

		public override string Author  {
			get{ return "Hextator and MrCactus"; }
		}

		public override string Description  {
			get{ return "Plugin that allows the players to lynch other players"; }
		}

		public override string Name  {
			get { return "Mob Justice"; }
		}

		
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, Game_Initialize);
				ServerApi.Hooks.ServerJoin.Deregister(this, OnPlayerJoin);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnPlayerLeave);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetNetData);
				TShockAPI.Hooks.GeneralHooks.ReloadEvent -= OnReload;
				// Methods to perform when the Plugin is disposed i.e. unhooks
			}
		}

		public MobJustice(Main game) : base(game)
		{
			// Load priority. smaller numbers loads earlier
			Order = 1;
		}
		private static Config.ConfigData config;
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, Game_Initialize);
			ServerApi.Hooks.ServerJoin.Register(this, OnPlayerJoin);
			ServerApi.Hooks.ServerLeave.Register(this, OnPlayerLeave);
			ServerApi.Hooks.NetGetData.Register(this, OnGetNetData);
			TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
			config = Config.GetConfigData();
			UpdateLynchRefs();
			// Methods to perform when plugin is initzialising i.e. hookings
		}
		//private HashSet<string> lynchables = new HashSet<string>();
		private HashSet<TSPlayer> lynchableRefs = new HashSet<TSPlayer>();
		private void OnReload(TShockAPI.Hooks.ReloadEventArgs args)
		{
			config = Config.GetConfigData();
			UpdateLynchRefs();
			args?.Player?.SendSuccessMessage("[MobJustice] Successfully reloaded config.");
		}
		private void SetTeam(TSPlayer player)
		{
			if (0 != player.Team)
			{
				player.SetTeam(0);
			}
		}
		private void SetPVP(TSPlayer player)
		{
			if (!player.TPlayer.hostile)
			{
				player.TPlayer.hostile = true;
				player.SendData(PacketTypes.TogglePvp, "", player.Index);
				TSPlayer.All.SendData(PacketTypes.TogglePvp, "", player.Index);
			}
		}
		private void OnPlayerJoin(JoinEventArgs args)
		{
			//HashSet<string> lynchableRefs should also be modified by login/logout, and should match lynchables (if name is in lynchables then the player with that name should be in lynchableRefs)
			TSPlayer player = TShock.Players[args.Who];
			if (null == player)
			{
				return;
			}
			if (config.savedLynchables.Contains(player.Name))
			{
				lynchableRefs.Add(player);
			}
		}
		private void UpdateLynchRefs()
		{
			lynchableRefs = new HashSet<TSPlayer>();
			foreach (TSPlayer player in TShock.Players)
			{
				if (null == player)
				{
					continue;
				}
				if (config.savedLynchables.Contains(player.Name))
				{
					lynchableRefs.Add(player);
					SetTeam(player);
					SetPVP(player);
				}
			}
		}
		private void OnPlayerLeave(LeaveEventArgs args)
		{
			TSPlayer player = TShock.Players[args.Who];
			if (null == player)
			{
				return;
			}
			if (!config.savedLynchables.Contains(player.Name))
			{
				lynchableRefs.Remove(player);
			}
		}
		private void OnGetNetData(GetDataEventArgs args)
		{
			PacketTypes packetType = args.MsgID;
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			if (null == player)
			{
				return;
			}
			if (!lynchableRefs.Contains(player))
			{
				return;
			}
			switch (packetType)
			{
				case PacketTypes.TogglePvp:
					SetPVP(player);
					player.SendMessage(String.Format("{0}", config.message), config.messagered, config.messagegreen, config.messageblue);
					break;
				case PacketTypes.ToggleParty:
					SetTeam(player);
					player.SendMessage(String.Format("{0}", config.teamMessage), config.teamMessageRed, config.teamMessageGreen, config.teamMessageBlue);
					break;
			}
		}
		private void Game_Initialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.force" }, ForceLynch, "forcelynchable"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.list" }, LynchList, "listlynch"));
		}
		public void LynchList(CommandArgs args)
		{
			if (!config.pluginenabled)
			{
				return;
			}
			args.Player.SendMessage(String.Format("Currently lynchable players: {0}", String.Join(", ", config.savedLynchables.ToList())), 255, 255, 0);
		}
		public void ForceLynch(CommandArgs args)
		{
			//HashSet<string> lynchables should only be modified by commands
			if (!config.pluginenabled)
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
			bool lynchable = config.savedLynchables.Contains(playerMatches[0].Name);
			if (!lynchable) 
			{
				config.savedLynchables.Add(playerMatches[0].Name);
				lynchableRefs.Add(playerMatches[0]);
				TSPlayer.All.SendMessage(String.Format("{0}", config.lynchplayermessage.Replace("{PLAYER_NAME}", playerMatches[0].Name)), config.lynchplayermessagered, config.lynchplayermessagegreen, config.lynchplayermessageblue);
				SetPVP(playerMatches[0]);
				SetTeam(playerMatches[0]);
			}
			else
			{
				config.savedLynchables.Remove(playerMatches[0].Name);
				lynchableRefs.Remove(playerMatches[0]);
				TSPlayer.All.SendMessage(String.Format("{0}", config.unlynchplayermessage.Replace("{PLAYER_NAME}", playerMatches[0].Name)), config.unlynchplayermessagered, config.unlynchplayermessagegreen, config.unlynchplayermessageblue);
			}
		}
	}
}
