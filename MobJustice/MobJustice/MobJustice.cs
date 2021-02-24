

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
			get{ return "MrCactus and Hextator"; }
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
				ServerApi.Hooks.GameUpdate.Deregister(this, Game_Update);
				ServerApi.Hooks.GameInitialize.Deregister(this, Game_Initialize);
				// Methods to perform when the Plugin is disposed i.e. unhooks
			}
		}

		public MobJustice(Main game) : base(game)
		{
			// Load priority. smaller numbers loads earlier
			Order = 1;
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameUpdate.Register(this, Game_Update);
			ServerApi.Hooks.GameInitialize.Register(this, Game_Initialize);
			// Methods to perform when plugin is initzialising i.e. hookings
		}
		private Dictionary<string, bool> lynchableStates = new Dictionary<string, bool>();
		readonly Config.ConfigData config = Config.GetConfigData();
		private void Game_Initialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command(new List<string>() { "tshock.admin.ban", "permissions.ban" }, ForceLynch, "forcelynchable"));
		}
		public void ForceLynch(CommandArgs args)
		{
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
			bool lynchable = false;
			lynchableStates.TryGetValue(playerMatches[0].Name, out lynchable);
			if (!lynchable) 
			{ 
				lynchableStates[playerMatches[0].Name] = true;
			}
			else
			{
				TSPlayer.All.SendMessage(String.Format("{0}", config.unlynchplayermessage.Replace("{PLAYER_NAME}", playerMatches[0].Name)), config.unlynchplayermessagered, config.unlynchplayermessagegreen, config.unlynchplayermessageblue);
			}
			lynchableStates[playerMatches[0].Name] = !lynchable;
			
		}

		private void Game_Update(EventArgs args)
		{
			Config.ConfigData config = Config.GetConfigData();
			if (1 != config.pluginenabled)
			{
				return;
			}
			foreach (TSPlayer player in TShock.Players)
			{
				if (null == player)
				{
					continue;
				}
				bool lynchable = false;
				lynchableStates.TryGetValue(player.Name, out lynchable);
				if (!lynchable)
				{
					continue;
				}
				if (0 != player.Team)
				{
					try
					{
						player.SendMessage(String.Format("{0}", config.teamMessage), config.teamMessageRed, config.teamMessageGreen, config.teamMessageBlue);
						try { player.SetTeam(0); } catch (Exception) { Console.WriteLine("Exception from setting player team"); }
					} catch(Exception) { }
				}
				if (!player.TPlayer.hostile)
				{
					try
					{
						player.TPlayer.hostile = true;
						try { player.SendData(PacketTypes.TogglePvp, "", player.Index); } catch (Exception) { Console.WriteLine("Exception from TogglePvP try"); }
						TSPlayer.All.SendMessage(String.Format("{0}", config.message.Replace("{PLAYER_NAME}", player.Name)), config.messagered, config.messagegreen, config.messageblue);
					}
					catch (Exception) { Console.WriteLine("Exception from !player.TPlayer.hostile"); }
				}
			}
		}
	}
}
