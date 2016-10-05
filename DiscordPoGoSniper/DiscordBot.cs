using Demo.Configuration;
using Discord;
using log4net;
using POGOProtos.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Demo
{
    internal class DiscordBot
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DiscordBot));
        private static List<PokemonSpawn> SpawnsCache = new List<PokemonSpawn>();
        private DiscordClient Client = new DiscordClient();
        private BotConfig Config;
        private PokemonSpawnVerifier PokemonSpawnVerifier;

        public DiscordBot(BotConfig config, PokemonSpawnVerifier pokemonSpawnVerifier)
        {
            Config = config;
            PokemonSpawnVerifier = pokemonSpawnVerifier;
        }

        public static double CalcIVPct(POGOProtos.Data.PokemonData p)

        {
            if (p == null)
                return 0d;
            //max A/D/S is 15/15/15 which corresponds to 100% IV
            //return ((p.IndividualAttack + p.IndividualDefense + p.IndividualStamina) / 45d) * 100d;
            //same as pokesniper *sigh*
            return Math.Round((double)(p.IndividualAttack + p.IndividualDefense + p.IndividualStamina) / 45.0 * 100.0);
        }

        public async Task Start()
        {
            if (Config.DiscordAccount.Token != null)
            {
                await Client.Connect(Config.DiscordAccount.Token);
            }
            else
            {
                await Client.Connect(Config.DiscordAccount.User, Config.DiscordAccount.Password);
            }
            Client.MessageReceived += OnMessageReceived;

            if (Config.DiscordSpyAccount != null)
            {
                var spyClient = new DiscordClient();
                if (Config.DiscordSpyAccount.Token != null)
                {
                    await spyClient.Connect(Config.DiscordSpyAccount.Token);
                }
                else
                {
                    await spyClient.Connect(Config.DiscordSpyAccount.User, Config.DiscordSpyAccount.Password);
                }
                spyClient.MessageReceived += OnMessageReceived;
            }

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(120000);
                    SpawnsCache = new List<PokemonSpawn>();
                }
            });
        }

        private bool AuthorizedForDeletion(Message message, ConfigDiscordChannel configListenDiscordChannel)
        {
            var result = false;

            if (configListenDiscordChannel.CensorMessages == true)
            {
                var me = message.Channel.GetUser(Client.CurrentUser.Id);
                var myChannelPermissions = me.GetPermissions(message.Channel);
                if (myChannelPermissions.ManageMessages)
                {
                    result = true;
                }

                var him = message.User;
                var hisChannelPermissions = him.GetPermissions(message.Channel);
                if (hisChannelPermissions.ManageMessages)
                {
                    result = false;
                }
            }
            return result;
        }

        private void CensorMessage(Message message)
        {
            // check if message is from a listen channel
            if (!IsListenChannel(message.Channel))
                return;

            var configListenDiscordChannel = GetConfigListenDiscordChannel(message.Channel);

            if (AuthorizedForDeletion(message, configListenDiscordChannel))
            {
                Task.Run(async () =>
                {
                    await Task.Delay((configListenDiscordChannel.CensorDelay ?? 10) * 1000);
                    message.Delete();
                });
            }
        }

        private ConfigDiscordChannel GetConfigListenDiscordChannel(Channel channel)
        {
            if (channel.Server == null)
            {
                //its a private message
                return null;
            }

            var configListenDiscordChannel = Config.ListenDiscords
                .Where(listenDiscordServer => listenDiscordServer.Id == channel.Server.Id.ToString())
                .SelectMany(listenDiscordServer => listenDiscordServer.Channels)
                .Where(listenDiscordChannel => listenDiscordChannel.Id == channel.Id.ToString())
                .FirstOrDefault();

            return configListenDiscordChannel;
        }

        private bool IsListenChannel(Channel discordChannel)
        {
            if (discordChannel.Server == null)
            {
                return false;
            }

            return (Config.ListenDiscords.Any(ds =>
                ds.Id == discordChannel.Server.Id.ToString()
                 && ds.Channels.Any(dc =>
                    dc.Id == discordChannel.Id.ToString())));
        }

        private void OnMessageReceived(object s, MessageEventArgs e)
        {
            // ignore my own messages
            if (e.Message.IsAuthor)
                return;

            ProcessCommands(e.Message);

            // check if message is from a listen channel
            if (!IsListenChannel(e.Channel))
                return;

            Log.Warn(e.Channel.Name + ">>" + e.Message.User.Name + "  " + e.Message.Text);

            var configListenDiscordChannel = GetConfigListenDiscordChannel(e.Channel);

            var pokemonSpawn = ParsePokemonSpawn(e.Message.Text);
            if (pokemonSpawn == null && (s as DiscordClient) == Client) //ignore spy
            {
                CensorMessage(e.Message);
                return;
            }

            if (pokemonSpawn == null)
                return;

            // check if not already resolved
            if (SpawnsCache.Any(ps => ps.Equals(pokemonSpawn)))
            {
                return;
            }

            // check if there are any publish channels
            var configDiscordPublishChannels = Config.PublishDiscords.SelectMany(ds => ds.Channels);
            if (configDiscordPublishChannels.Count() < 1)
                return;

            var pokemonName = Enum.GetNames(typeof(PokemonId)).FirstOrDefault(p => e.Message.Text.ToLower().Contains(p.ToLower()));
            if (pokemonName == null)
            {
                return;
            }

            // time to verify the spawn
            Log.Warn($"Adding {pokemonSpawn.PokemonId} to the QUEUE");
            PokemonSpawnVerifier.Queue.Enqueue(pokemonSpawn, (encounterResponse) =>
            {
                // encounter was successful
                var pokemonData = encounterResponse.WildPokemon?.PokemonData;

                if (pokemonData == null)
                {
                    return; // is it okay to silently fail? (todo)
                }

                // check if not already resolved
                if (SpawnsCache.Any(ps => ps.Equals(pokemonSpawn)))
                {
                    return;
                }

                // implement some kind of publishing cache (TODO)
                SpawnsCache.Add(pokemonSpawn);

                var iv = (int)CalcIVPct(pokemonData);
                var lat = pokemonSpawn.Latitude.ToString(CultureInfo.InvariantCulture);
                var lon = pokemonSpawn.Longitude.ToString(CultureInfo.InvariantCulture);
                var despawnSeconds = encounterResponse.WildPokemon.TimeTillHiddenMs / 1000;
                var message = $"{(iv >= 90 ? iv == 100 ? ":100:" : ":ok_hand:" : "")} {pokemonName} {lat}, {lon} IV:{iv}% ({despawnSeconds}s) Verified {lat} {lon} {pokemonData.Move1}/{pokemonData.Move2}";

                // we assume channel id's are unique in discord
                configDiscordPublishChannels.ToList().ForEach(configDiscordPublishChannel =>
                {
                    var discordChannel = Client.GetChannel(Convert.ToUInt64(configDiscordPublishChannel.Id));
                    if (discordChannel == null)
                        return;

                    if (configDiscordPublishChannel.MinimumIV != null && configDiscordPublishChannel.MinimumIV > iv)
                    {
                        return;
                    }

                    //send the message
                    Log.Warn($">>>>{configDiscordPublishChannel.Name} {message}");

                    if (discordChannel == e.Channel) //if we got the data from the same chan we are replying to
                    {
                        discordChannel.SendMessage($"{message} {e.User.Mention}");
                    }
                    else
                    {
                        discordChannel.SendMessage(message);
                    }
                });
            });
        }

        private PokemonSpawn ParsePokemonSpawn(string messageText)
        {
            var matches = Regex.Matches(messageText, @"((-)?\d{1,3}\.\d+)(,| ,|, | )((-)?\d{1,3}\.\d*)");
            if (matches.Count > 0 && matches[0].Groups.Count > 4)
            {
                var lat = Convert.ToDouble(matches[0].Groups[1].Value, CultureInfo.InvariantCulture);
                var lon = Convert.ToDouble(matches[0].Groups[4].Value, CultureInfo.InvariantCulture);

                if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
                    return null;

                var pokemons = Enum.GetNames(typeof(PokemonId));
                var pokemonName = pokemons.FirstOrDefault(p => messageText.ToLower().Contains(p.ToLower()));
                if (pokemonName == null)
                {
                    return null;
                }

                var pokemon = (PokemonId)Enum.Parse(typeof(PokemonId), pokemonName);

                var pokemonSpawn = new PokemonSpawn(pokemon, lat, lon, -1);
                return pokemonSpawn;
            }

            return null;
        }

        private void ProcessCommands(Message message)
        {
            if (message.Text.StartsWith("!identify"))
            {
                message.Channel.SendMessage("Discord PoGo Spawn bot V1.0.  Author pingec@pingec.si Non-commercial purposes only.");
            }
        }
    }
}