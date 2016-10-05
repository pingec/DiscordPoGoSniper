using System.Collections.Generic;

namespace Demo.Configuration
{
    public class BotConfig
    {
        public ConfigDiscordAccount DiscordAccount;
        public ConfigDiscordAccount DiscordSpyAccount;
        public List<ConfigDiscordServer> ListenDiscords;

        public List<PTC> PtcAccounts;
        public List<ConfigDiscordServer> PublishDiscords;
    }

    public class ConfigDiscordAccount
    {
        public bool IsBot;
        public string Password;
        public string Token;
        public string User;
    }

    public class ConfigDiscordChannel
    {
        public int? CensorDelay;
        public bool? CensorMessages;
        public string Id;
        public int? MinimumIV;
        public string Name;
    }

    public class ConfigDiscordServer
    {
        public List<ConfigDiscordChannel> Channels;
        public string Id;
        public string Name;
    }

    public class PTC
    {
        public string Password;
        public string Username;
    }
}