using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.IO;

namespace MobJustice {
	public class Config {
		#region Defaults

		private const bool PLUGIN_ENABLED = true;
		private const string MESSAGE = "You are not allowed to toggle PvP!";
		private const byte MESSAGE_RED = 255;
		private const byte MESSAGE_GREEN = 255;
		private const byte MESSAGE_BLUE = 255;
		private const string TEAM_MESSAGE = "You are not allowed to join a team!";
		private const byte TEAM_MESSAGE_RED = 255;
		private const byte TEAM_MESSAGE_GREEN = 255;
		private const byte TEAM_MESSAGE_BLUE = 255;
		private const string UNLYNCH_PLAYER_MESSAGE = "{PLAYER_NAME} is no longer lynchable";
		private const byte UNLYNCH_PLAYER_MESSAGE_RED = 255;
		private const byte UNLYNCH_PLAYER_MESSAGE_GREEN = 255;
		private const byte UNLYNCH_PLAYER_MESSAGE_BLUE = 0;
		private const string LYNCH_PLAYER_MESSAGE = "{PLAYER_NAME} is now lynchable";
		private const byte LYNCH_PLAYER_MESSAGE_RED = 255;
		private const byte LYNCH_PLAYER_MESSAGE_GREEN = 255;
		private const byte LYNCH_PLAYER_MESSAGE_BLUE = 0;
		private const int LYNCH_DURATION = 5 * 60;
		private const int LYNCH_COOLDOWN = 15 * 60;

		#endregion

		public static string ConfigFileName = @"tshock\Mob Justice\config.xml";

		public class ConfigData {
			public bool pluginenabled;

			public string message;
			public byte messageRed;
			public byte messageGreen;
			public byte messageBlue;
			public string teamMessage;
			public byte teamMessageRed;
			public byte teamMessageGreen;
			public byte teamMessageBlue;
			public string lynchPlayerMessage;
			public byte lynchPlayerMessageRed;
			public byte lynchPlayerMessageGreen;
			public byte lynchPlayerMessageBlue;
			public string unlynchPlayerMessage;
			public byte unlynchPlayerMessageRed;
			public byte unlynchPlayerMessageGreen;
			public byte unlynchPlayerMessageBlue;
			// XXX: Serialization seems to work for the HashSet, so do we even still need this List?
			public List<string> serializableLynchables = new List<string>();
			public HashSet<string> savedLynchables;
			public uint lynchDuration;
			public uint lynchCooldown;

			public ConfigData() {
				this.pluginenabled = PLUGIN_ENABLED;
				this.message = MESSAGE;
				this.messageRed = MESSAGE_RED;
				this.messageGreen = MESSAGE_GREEN;
				this.messageBlue = MESSAGE_BLUE;
				this.teamMessage = TEAM_MESSAGE;
				this.teamMessageRed = TEAM_MESSAGE_RED;
				this.teamMessageGreen = TEAM_MESSAGE_GREEN;
				this.teamMessageBlue = TEAM_MESSAGE_BLUE;
				this.lynchPlayerMessage = LYNCH_PLAYER_MESSAGE;
				this.lynchPlayerMessageRed = LYNCH_PLAYER_MESSAGE_RED;
				this.lynchPlayerMessageGreen = LYNCH_PLAYER_MESSAGE_GREEN;
				this.lynchPlayerMessageBlue = LYNCH_PLAYER_MESSAGE_BLUE;
				this.unlynchPlayerMessage = UNLYNCH_PLAYER_MESSAGE;
				this.unlynchPlayerMessageRed = UNLYNCH_PLAYER_MESSAGE_RED;
				this.unlynchPlayerMessageGreen = UNLYNCH_PLAYER_MESSAGE_GREEN;
				this.unlynchPlayerMessageBlue = UNLYNCH_PLAYER_MESSAGE_BLUE;
				this.savedLynchables = new HashSet<string>();
				this.serializableLynchables = new List<string>();
				this.lynchDuration = LYNCH_DURATION;
				this.lynchCooldown = LYNCH_COOLDOWN;
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
			currData.serializableLynchables.ForEach(currLynchable => currData.savedLynchables.Add(currLynchable));
			return currData;
		}

		public static void SaveConfigData(ConfigData config) {
			config.serializableLynchables = config.savedLynchables.ToList();
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
