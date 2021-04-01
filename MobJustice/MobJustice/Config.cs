using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.IO;

namespace MobJustice {
	public static class Config {
		#region Defaults

		private const bool PLUGIN_ENABLED = true;
		private const string DENY_DISABLE_PVP_MESSAGE = "You are not allowed to toggle PvP!";
		private const byte DENY_DISABLE_PVP_MESSAGE_RED = 255;
		private const byte DENY_DISABLE_PVP_MESSAGE_GREEN = 255;
		private const byte DENY_DISABLE_PVP_MESSAGE_BLUE = 255;
		private const string DENY_CHANGE_TEAM_MESSAGE = "You are not allowed to join a team!";
		private const byte DENY_CHANGE_TEAM_MESSAGE_RED = 255;
		private const byte DENY_CHANGE_TEAM_MESSAGE_GREEN = 255;
		private const byte DENY_CHANGE_TEAM_MESSAGE_BLUE = 255;
		private const string LYNCH_PLAYER_MESSAGE = "{PLAYER_NAME} is now lynchable";
		private const byte LYNCH_PLAYER_MESSAGE_RED = 255;
		private const byte LYNCH_PLAYER_MESSAGE_GREEN = 255;
		private const byte LYNCH_PLAYER_MESSAGE_BLUE = 0;
		private const string UNLYNCH_PLAYER_MESSAGE = "{PLAYER_NAME} is no longer lynchable";
		private const byte UNLYNCH_PLAYER_MESSAGE_RED = 255;
		private const byte UNLYNCH_PLAYER_MESSAGE_GREEN = 255;
		private const byte UNLYNCH_PLAYER_MESSAGE_BLUE = 0;
		private const double LYNCHING_PLAYERS_RATIO = 0.5;
		private const int MIN_PLAYERS_FOR_LYNCH = 2;
		private const int LYNCH_DURATION = 5 * 60;
		private const int LYNCH_COOLDOWN = 15 * 60;

		#endregion

		public static string ConfigFileName = @"tshock\Mob Justice\config.xml";

		[Serializable()]
		public class ConfigData {
			public bool pluginEnabled = PLUGIN_ENABLED;
			public string denyDisablePvPMessage = DENY_DISABLE_PVP_MESSAGE;
			public byte denyDisablePvPMessageRed = DENY_DISABLE_PVP_MESSAGE_RED;
			public byte denyDisablePvPMessageGreen = DENY_DISABLE_PVP_MESSAGE_GREEN;
			public byte denyDisablePvPMessageBlue = DENY_DISABLE_PVP_MESSAGE_BLUE;
			public string denyChangeTeamMessage = DENY_CHANGE_TEAM_MESSAGE;
			public byte denyChangeTeamMessageRed = DENY_CHANGE_TEAM_MESSAGE_RED;
			public byte denyChangeTeamMessageGreen = DENY_CHANGE_TEAM_MESSAGE_GREEN;
			public byte denyChangeTeamMessageBlue = DENY_CHANGE_TEAM_MESSAGE_BLUE;
			public string lynchPlayerMessage = LYNCH_PLAYER_MESSAGE;
			public byte lynchPlayerMessageRed = LYNCH_PLAYER_MESSAGE_RED;
			public byte lynchPlayerMessageGreen = LYNCH_PLAYER_MESSAGE_GREEN;
			public byte lynchPlayerMessageBlue = LYNCH_PLAYER_MESSAGE_BLUE;
			public string unlynchPlayerMessage = UNLYNCH_PLAYER_MESSAGE;
			public byte unlynchPlayerMessageRed = UNLYNCH_PLAYER_MESSAGE_RED;
			public byte unlynchPlayerMessageGreen = UNLYNCH_PLAYER_MESSAGE_GREEN;
			public byte unlynchPlayerMessageBlue = UNLYNCH_PLAYER_MESSAGE_BLUE;
			public double lynchingPlayersRatio = LYNCHING_PLAYERS_RATIO;
			public uint minPlayersForLynch = MIN_PLAYERS_FOR_LYNCH;
			public uint lynchDuration = LYNCH_DURATION;
			public uint lynchCooldown = LYNCH_COOLDOWN;
			[NonSerialized()]
			private readonly object forcedLynchablesLockObj = new object();
			public List<string> serializableLynchables = new List<string>();
			[NonSerialized()]
			private HashSet<string> savedLynchables = new HashSet<string>();

			public List<string> CurrentForcedLynchables() {
				List<string> output;
				lock (this.forcedLynchablesLockObj) {
					output = this.savedLynchables.ToList();
				}
				return output;
			}

			public void UpdateLynchablesFromSerialized() {
				lock (this.forcedLynchablesLockObj) {
					this.serializableLynchables.ForEach(currLynchable => this.savedLynchables.Add(currLynchable));
				}
			}

			public void UpdateSerializedLynchablesFromLive() {
				this.serializableLynchables = this.CurrentForcedLynchables();
			}

			public bool IsForcedLynchable(string targetName) {
				bool output;
				lock (this.forcedLynchablesLockObj) {
					output = this.savedLynchables.Contains(targetName);
				}
				return output;
			}

			public LynchVoteResult ToggleLynchForce(string targetName) {
				LynchVoteResult voteResult;
				lock (this.forcedLynchablesLockObj) {
					if (this.savedLynchables.Contains(targetName)) {
						this.savedLynchables.Remove(targetName);
						voteResult = LynchVoteResult.VotedProtect;
					}
					else {
						this.savedLynchables.Add(targetName);
						voteResult = LynchVoteResult.StartedLynch;
					}
				}
				return voteResult;
			}
		}

		public static ConfigData GetConfigData() {
			ConfigData currData = new ConfigData();
			if (!File.Exists(ConfigFileName)) {
				Directory.CreateDirectory(Path.GetDirectoryName(ConfigFileName));
				using (FileStream fs = new FileStream(ConfigFileName, FileMode.Create)) {
					XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
					xs.Serialize(fs, currData);
				}
			}
			else {
				using (FileStream fs = new FileStream(ConfigFileName, FileMode.Open)) {
					XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
					currData = (ConfigData)xs.Deserialize(fs);
				}
			}
			currData.UpdateLynchablesFromSerialized();
			return currData;
		}

		public static void SaveConfigData(ConfigData config) {
			config.UpdateLynchablesFromSerialized();
			if (!File.Exists(ConfigFileName)) {
				return;
			}

			using (FileStream fs = new FileStream(ConfigFileName, FileMode.Truncate)) {
				XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
				xs.Serialize(fs, config);
				return;
			}
		}
	}
}
