using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Configuration;
using System.IO;

namespace MobJustice {
	public class Config {
		#region Defaults

		private const bool PLUGIN_ENABLED = true;
		private const string MESSAGE = "You are not allowed to toggle pvp";
		private const byte MESSAGE_RED = 255;
		private const byte MESSAGE_GREEN = 255;
		private const byte MESSAGE_BLUE = 255;
		private const string TEAM_MESSAGE = "You are not allowed to join a team";
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

		#endregion

		public static string ConfigFileName = @"tshock\Mob Justice\config.xml";

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

		public class ConfigData {
			public bool pluginenabled;

			public string message;
			public byte messagered;
			public byte messagegreen;
			public byte messageblue;
			public string teamMessage;
			public byte teamMessageRed;
			public byte teamMessageGreen;
			public byte teamMessageBlue;
			public string unlynchplayermessage;
			public byte unlynchplayermessagered;
			public byte unlynchplayermessagegreen;
			public byte unlynchplayermessageblue;
			public string lynchplayermessage;
			public byte lynchplayermessagered;
			public byte lynchplayermessagegreen;
			public byte lynchplayermessageblue;
			public List<string> serializableLynchables = new List<string>();
			public HashSet<string> savedLynchables;


			public ConfigData() {
				this.pluginenabled = PLUGIN_ENABLED;
				this.message = MESSAGE;
				this.messagered = MESSAGE_RED;
				this.messagegreen = MESSAGE_GREEN;
				this.messageblue = MESSAGE_BLUE;
				this.teamMessage = TEAM_MESSAGE;
				this.teamMessageRed = TEAM_MESSAGE_RED;
				this.teamMessageGreen = TEAM_MESSAGE_GREEN;
				this.teamMessageBlue = TEAM_MESSAGE_BLUE;
				this.unlynchplayermessage = UNLYNCH_PLAYER_MESSAGE;
				this.unlynchplayermessagered = UNLYNCH_PLAYER_MESSAGE_RED;
				this.unlynchplayermessagegreen = UNLYNCH_PLAYER_MESSAGE_GREEN;
				this.unlynchplayermessageblue = UNLYNCH_PLAYER_MESSAGE_BLUE;
				this.lynchplayermessage = LYNCH_PLAYER_MESSAGE;
				this.lynchplayermessagered = LYNCH_PLAYER_MESSAGE_RED;
				this.lynchplayermessagegreen = LYNCH_PLAYER_MESSAGE_GREEN;
				this.lynchplayermessageblue = LYNCH_PLAYER_MESSAGE_BLUE;
				this.savedLynchables = new HashSet<string>();
				this.serializableLynchables = new List<string>();
			}
		}
	}
}
