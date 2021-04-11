using System;

using Terraria;
using TerrariaApi.Server;

namespace MobJustice {
	[ApiVersion(2, 1)]
	public class MobJusticePlugin : TerrariaPlugin {
		// ------------------------------
		// Meta data
		// ------------------------------

		public override string Author {
			get { return "Hextator and MrCactus"; }
		}

		public override string Description {
			get { return "Plugin that allows the players to lynch other players"; }
		}

		public override string Name {
			get { return MobJusticeEnforcer.PLUGIN_NAME; }
		}

		public override Version Version => new Version(2, 1, 20210410, 1);

		public MobJusticePlugin(Main game) : base(game) {
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

		public override void Initialize() {
			// Methods to perform when plugin is initializing i.e. hooks
			ServerApi.Hooks.GameInitialize.Register(this, this.mobJusticeEnforcer.Game_Initialize);
			TShockAPI.Hooks.GeneralHooks.ReloadEvent += this.mobJusticeEnforcer.OnReload;
			ServerApi.Hooks.ServerJoin.Register(this, this.mobJusticeEnforcer.OnPlayerJoin);
			ServerApi.Hooks.ServerLeave.Register(this, this.mobJusticeEnforcer.OnPlayerLeave);
			ServerApi.Hooks.NetGetData.Register(this, this.mobJusticeEnforcer.OnGetNetData);
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				// Methods to perform when the Plugin is disposed i.e. unhooks
				ServerApi.Hooks.GameInitialize.Deregister(this, this.mobJusticeEnforcer.Game_Initialize);
				TShockAPI.Hooks.GeneralHooks.ReloadEvent -= this.mobJusticeEnforcer.OnReload;
				ServerApi.Hooks.ServerJoin.Deregister(this, this.mobJusticeEnforcer.OnPlayerJoin);
				ServerApi.Hooks.ServerLeave.Deregister(this, this.mobJusticeEnforcer.OnPlayerLeave);
				ServerApi.Hooks.NetGetData.Deregister(this, this.mobJusticeEnforcer.OnGetNetData);
			}
		}
	}
}
