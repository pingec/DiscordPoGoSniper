//using Demo.Configuration;
//using DiscordSharp;
//using DiscordSharp.Events;
//using DiscordSharp.Objects;
//using log4net;
//using POGOProtos.Enums;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Demo
//{
//    internal class DiscorSharpdBot
//    {
//        private DiscordClient Client;
//        private BotConfig Config;
//        private PokemonSpawnVerifier PokemonSpawnVerifier;
//        private static readonly ILog Log = LogManager.GetLogger(typeof(DiscordBot));
//        private static List<PokemonSpawn> SpawnsCache = new List<PokemonSpawn>();

//        public DiscorSharpdBot(BotConfig config, PokemonSpawnVerifier pokemonSpawnVerifier)
//        {
//            Config = config;
//            PokemonSpawnVerifier = pokemonSpawnVerifier;
//        }

//        public async Task Start()
//        {
//            var spyClient = new DiscordClient();
//            spyClient.ClientPrivateInformation.Email = "user@example.com";
//            spyClient.ClientPrivateInformation.Password = "pass";
//            spyClient.MessageReceived += OnMessageReceived;
//            spyClient.Connected += (sender, e) =>
//            {
//                Console.WriteLine($"Connected! Spy User: {e.User.Username}");
//            };
//            spyClient.SendLoginRequest();

//            Client = new DiscordClient("token here", true);

//            Client.MessageReceived += OnMessageReceived;
//            Client.Connected += (sender, e) =>
//            {
//                Console.WriteLine($"Connected! User: {e.User.Username}");
//            };

//            Client.SendLoginRequest();

//            Task.Run(async () =>
//            {
//                while (true)
//                {
//                    await Task.Delay(60000);
//                    SpawnsCache = new List<PokemonSpawn>();
//                }
//            });

//            await Task.Run(() =>
//            {
//                Client.Connect();
//                spyClient.Connect();
//            });
//        }

//        private bool IsListenChannel(DiscordChannel discordChannel)
//        {
//            var server = discordChannel.Parent;
//            if (server == null)
//            {
//                return false;
//            }

//            return Config.ListenDiscords.Any(ds => ds.Id == server.ID
//                && ds.Channels.Any(dc =>
//                    dc.Id == discordChannel.ID));
//        }

//        private ConfigDiscordChannel GetConfigListenDiscordChannel(DiscordChannel channel)
//        {
//            var server = channel.Parent;
//            if (server == null)
//            {
//                return null;
//            }

//            var configListenDiscordChannel = Config.ListenDiscords
//                .Where(listenDiscordServer => listenDiscordServer.Id == server.ID)
//                .SelectMany(listenDiscordServer => listenDiscordServer.Channels)
//                .Where(listenDiscordChannel => listenDiscordChannel.Id == channel.ID)
//                .FirstOrDefault();

//            return configListenDiscordChannel;
//        }

//        private bool AuthorizedForDeletion(DiscordMessage message, ConfigDiscordChannel configListenDiscordChannel)
//        {
//            //todo
//            return true;
//        }

//        public void IgnoreExceptions(Action act)
//        {
//            try
//            {
//                act.Invoke();
//            }
//            catch { }
//        }

//        private void CensorMessage(DiscordMessage message)
//        {
//            var channel = message.Channel() as DiscordChannel;
//            // check if message is from a listen channel
//            if (channel == null || !IsListenChannel(channel))
//                return;

//            var configListenDiscordChannel = GetConfigListenDiscordChannel(channel);
//            if (configListenDiscordChannel == null)
//                return;

//            if (AuthorizedForDeletion(message, configListenDiscordChannel))
//            {
//                Task.Run(async () =>
//                {
//                    await Task.Delay(10000);
//                    IgnoreExceptions(() => Client.DeleteMessage(message));
//                });
//            }
//        }

//        private PokemonSpawn ParsePokemonSpawn(string messageText)
//        {
//            var matches = Regex.Matches(messageText, @"((-)?\d{1,3}\.\d+)(,| ,|, | )((-)?\d{1,3}\.\d*)");
//            if (matches.Count > 0 && matches[0].Groups.Count > 4)
//            {
//                var lat = Convert.ToDouble(matches[0].Groups[1].Value, CultureInfo.InvariantCulture);
//                var lon = Convert.ToDouble(matches[0].Groups[4].Value, CultureInfo.InvariantCulture);

//                var pokemons = Enum.GetNames(typeof(PokemonId));
//                var pokemonName = pokemons.FirstOrDefault(p => messageText.ToLower().Contains(p.ToLower()));
//                if (pokemonName == null)
//                {
//                    return null;
//                }

//                var pokemon = (PokemonId)Enum.Parse(typeof(PokemonId), pokemonName);

//                var pokemonSpawn = new PokemonSpawn(pokemon, lat, lon, -1);
//                return pokemonSpawn;
//            }

//            return null;
//        }

//        private void OnMessageReceived(object s, DiscordMessageEventArgs e)
//        {
//            Log.Warn(e.Channel.Parent.Name + ">>" + e.Channel.Name + ">>" + e.Author.Username + "  " + e.MessageText);

//            // ignore my own messages
//            if (e.Message.Author == Client.Me)
//                return;

//            // check if message is from a listen channel
//            if (!IsListenChannel(e.Channel))
//                return;

//            var configListenDiscordChannel = GetConfigListenDiscordChannel(e.Channel);

//            var pokemonSpawn = ParsePokemonSpawn(e.MessageText);
//            if (pokemonSpawn == null)
//            {
//                CensorMessage(e.Message);
//                return;
//            }

//            // check if not already resolved
//            if (SpawnsCache.Any(ps => ps.Equals(pokemonSpawn)))
//            {
//                return;
//            }

//            // check if there are any publish channels
//            var configDiscordPublishChannels = Config.PublishDiscords.SelectMany(ds => ds.Channels);
//            if (configDiscordPublishChannels.Count() < 1)
//                return;

//            var pokemonName = Enum.GetNames(typeof(PokemonId)).FirstOrDefault(p => e.MessageText.ToLower().Contains(p.ToLower()));
//            if (pokemonName == null)
//            {
//                return;
//            }

//            // time to verify the spawn
//            Log.Warn($"Adding {pokemonSpawn.PokemonId} to the QUEUE");

//            PokemonSpawnVerifier.Queue.Enqueue(pokemonSpawn, (encounterResponse) =>
//            {
//                // encounter was successful
//                var pokemonData = encounterResponse.WildPokemon?.PokemonData;

//                if (pokemonData == null)
//                {
//                    return; // is it okay to silently fail? (todo)
//                }

//                // implement some kind of publishing cache (TODO)
//                SpawnsCache.Add(pokemonSpawn);

//                var iv = (int)CalcIVPct(pokemonData);
//                var lat = pokemonSpawn.Latitude.ToString(CultureInfo.InvariantCulture);
//                var lon = pokemonSpawn.Longitude.ToString(CultureInfo.InvariantCulture);
//                var despawnSeconds = encounterResponse.WildPokemon.TimeTillHiddenMs / 1000;
//                var message = $"{pokemonName} {lat} {lon} IV:{iv}% ({despawnSeconds}s) Verified {lat},{lon} {pokemonData.Move1}/{pokemonData.Move2}";

//                // we assume channel id's are unique in discord
//                configDiscordPublishChannels.ToList().ForEach(configDiscordPublishChannel =>
//                {
//                    var discordChannel = Client.GetChannelByID(Convert.ToInt64(configDiscordPublishChannel.Id));

//                    if (discordChannel == null)
//                        return;

//                    if (configDiscordPublishChannel.MinimumIV != null && configDiscordPublishChannel.MinimumIV > iv)
//                    {
//                        return;
//                    }

//                    //send the message
//                    Log.Warn($">>>>{configDiscordPublishChannel.Name} {message}");
//                    discordChannel.SendMessage(message);
//                });
//            });
//        }

//        public static double CalcIVPct(POGOProtos.Data.PokemonData p)

//        {
//            if (p == null)
//                return 0d;
//            //max A/D/S is 15/15/15 which corresponds to 100% IV
//            return ((p.IndividualAttack + p.IndividualDefense + p.IndividualStamina) / 45d) * 100d;
//        }
//    }
//}