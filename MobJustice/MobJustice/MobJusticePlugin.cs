using System;
using System.Collections.Generic;

using Extensions;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace MobJustice
{
	[ApiVersion(2, 1)]
	public class MobJusticePlugin : TerrariaPlugin
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

		public override Version Version => new Version(1, 1, 0, 0);

		public MobJusticePlugin(Main game) : base(game)
		{
			// Load priority; smaller numbers load earlier
			this.Order = 5;
		}

		// ------------------------------
		// Implementation
		// ------------------------------

		MobJusticeEnforcer mobJusticeEnforcer = new MobJusticeEnforcer();

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
			this.mobJusticeEnforcer.config = Config.GetConfigData();
			this.mobJusticeEnforcer.UpdateLynchRefs();
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
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.force" }, this.mobJusticeEnforcer.ForceLynch, "forcelynchable"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.lynch" }, this.mobJusticeEnforcer.VoteLynch, "lynch"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.list" }, this.mobJusticeEnforcer.ReportForcedLynches, "showforcedlynches"));
			Commands.ChatCommands.Add(new Command(new List<string>() { "mobjustice.listvotes" }, this.mobJusticeEnforcer.ReportLynchVoteStates, "showlynchvotes"));
			// TODO: Make /lynch display all commands but only display the commands the being that ran the command has permissions for
		}

		private void OnReload(TShockAPI.Hooks.ReloadEventArgs args) {
			this.mobJusticeEnforcer.config = Config.GetConfigData();
			this.mobJusticeEnforcer.UpdateLynchRefs();
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
			if (this.mobJusticeEnforcer.config.savedLynchables.Contains(player.Name)) {
				TShockExtensions.TShockExtensions.SetPvP(player);
				TSPlayer.All.SendMessage(String.Format("{0}'s PvP has been forcefully turned on", player.Name), 255, 255, 255);
			}
		}

		public void OnPlayerLeave(LeaveEventArgs args) {
			// If a player leaves, they forfeit their lynch votes
			string voterName = TShock.Players[args.Who].Name;
			foreach (string targetName in this.mobJusticeEnforcer.playerLynchVotes.Get(voterName, new HashSet<string>())) {
				this.mobJusticeEnforcer.votesCounter[targetName] -= 1;
			}
			this.mobJusticeEnforcer.playerLynchVotes.Remove(voterName);

		}

		private void OnGetNetData(GetDataEventArgs args) {
			PacketTypes packetType = args.MsgID;
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			if (null == player) {
				return;
			}
			if (!this.mobJusticeEnforcer.config.savedLynchables.Contains(player.Name) && !this.mobJusticeEnforcer.IsVotedLynchTarget(player.Name)) {
				return;
			}
			// Player is lynchable; override attempts to set PvP or team state accordingly
			switch (packetType) {
				case PacketTypes.TogglePvp:
					TShockExtensions.TShockExtensions.SetPvP(player);
					player.SendMessage(
						String.Format("{0}", this.mobJusticeEnforcer.config.message),
						this.mobJusticeEnforcer.config.messagered, this.mobJusticeEnforcer.config.messagegreen, this.mobJusticeEnforcer.config.messageblue
					);
					args.Handled = true;
					break;
				// This packet is stupidly weird and bad. Why would it be called PlayerTeam when there's a packet called ToggleParty that doesn't work the way that the name suggests..
				case PacketTypes.PlayerTeam:
					TShockExtensions.TShockExtensions.SetTeam(player);
					player.SendMessage(
						String.Format("{0}", this.mobJusticeEnforcer.config.teamMessage),
						this.mobJusticeEnforcer.config.teamMessageRed, this.mobJusticeEnforcer.config.teamMessageGreen, this.mobJusticeEnforcer.config.teamMessageBlue
					);
					// These handled thingies really worked out, it doesn't spam the chat anymore when you toggle...10/10
					args.Handled = true;
					break;
			}
		}
	}
}
