using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Configuration;
using System.IO;

namespace MobJustice
{
	public class Config
	{
		#region Defaults

		private const bool PLUGIN_ENABLED = true;
		private const string MESSAGE = "Your PvP has been forcefully enabled for doing warcrimes";
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
		private const byte UNLYNCH_PLAYER_MESSAGE_BLUE = 255;
		private const string LYNCH_PLAYER_MESSAGE = "{PLAYER_NAME} is now lynchable";
		private const byte LYNCH_PLAYER_MESSAGE_RED = 255;
		private const byte LYNCH_PLAYER_MESSAGE_GREEN = 255;
		private const byte LYNCH_PLAYER_MESSAGE_BLUE = 0;

		#endregion

		public static string ConfigFileName = @"tshock\Mob Justice\config.xml";

		public static ConfigData GetConfigData()
		{
			if (!File.Exists(ConfigFileName))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ConfigFileName));
				using (FileStream fs = new FileStream(ConfigFileName, FileMode.Create))
				{
					XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
					ConfigData sxml = new ConfigData();
					xs.Serialize(fs, sxml);
					return sxml;
				}
			}
			else
			{
				using (FileStream fs = new FileStream(ConfigFileName, FileMode.Open))
				{
					XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
					ConfigData sc = (ConfigData)xs.Deserialize(fs);
					return sc;
				}
			}
		}
		public static bool SaveConfigData(ConfigData config)
		{
			if (!File.Exists(ConfigFileName)) return false;

			using (FileStream fs = new FileStream(ConfigFileName, FileMode.Truncate))
			{
				XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
				xs.Serialize(fs, config);
				return true;
			}
		}

		public class ConfigData
		{
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
			//public List<string> savedLynchables = new List<string>();
			public HashSet<string> savedLynchables;


			public ConfigData()
			{
				pluginenabled = PLUGIN_ENABLED;
				message = MESSAGE;
				messagered = MESSAGE_RED;
				messagegreen = MESSAGE_GREEN;
				messageblue = MESSAGE_BLUE;
				teamMessage = TEAM_MESSAGE;
				teamMessageRed = TEAM_MESSAGE_RED;
				teamMessageGreen = TEAM_MESSAGE_GREEN;
				teamMessageBlue = TEAM_MESSAGE_BLUE;
				unlynchplayermessage = UNLYNCH_PLAYER_MESSAGE;
				unlynchplayermessagered = UNLYNCH_PLAYER_MESSAGE_RED;
				unlynchplayermessagegreen = UNLYNCH_PLAYER_MESSAGE_GREEN;
				unlynchplayermessageblue = UNLYNCH_PLAYER_MESSAGE_BLUE;
				lynchplayermessage = LYNCH_PLAYER_MESSAGE;
				lynchplayermessagered = LYNCH_PLAYER_MESSAGE_RED;
				lynchplayermessagegreen = LYNCH_PLAYER_MESSAGE_GREEN;
				lynchplayermessageblue = LYNCH_PLAYER_MESSAGE_BLUE;
				savedLynchables = new HashSet<string>();
			}
		}
	}
}
