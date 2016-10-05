using Demo.Configuration;
using Google.Protobuf;
using log4net;
using Newtonsoft.Json;
using POGOLib.Net;
using POGOLib.Net.Authentication;
using POGOLib.Net.Authentication.Data;
using POGOLib.Pokemon.Data;
using POGOProtos.Enums;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Demo
{
    public class PokemonSpawn
    {
        public DateTime Expires;
        public double Latitude;
        public double Longitude;
        public PokemonId PokemonId;

        public PokemonSpawn(PokemonId pokemonId, double latitude, double longitude, int despawnTime)
        {
            Expires = DateTime.UtcNow.AddMilliseconds(despawnTime);
            PokemonId = pokemonId;
            Latitude = Math.Round(latitude, 5);
            Longitude = Math.Round(longitude, 5);
        }

        public static bool IsCached(List<PokemonSpawn> cache, PokemonSpawn spawn)
        {
            return cache.Any(i =>
            {
                return i.PokemonId == spawn.PokemonId && i.Latitude == spawn.Latitude && i.Longitude == spawn.Longitude;
            });
        }

        public bool Equals(PokemonSpawn pokemonSpawn)
        {
            return this.PokemonId == pokemonSpawn.PokemonId
                && this.Latitude == pokemonSpawn.Latitude
                && this.Longitude == pokemonSpawn.Longitude;
        }
    }

    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        public static double CalcIVPct(POGOProtos.Data.PokemonData p)
        {
            if (p == null)
                return 0d;
            //max A/D/S is 15/15/15 which corresponds to 100% IV
            return ((p.IndividualAttack + p.IndividualDefense + p.IndividualStamina) / 45d) * 100d;
        }

        public static EncounterResponse EncounterPokemon(ulong encounterId, string spawnPointGuid, Session session)
        {
            var response = session.RpcClient.SendRemoteProcedureCall(new Request
            {
                RequestType = RequestType.Encounter,
                RequestMessage = new EncounterMessage
                {
                    EncounterId = encounterId,
                    SpawnPointId = spawnPointGuid,
                    PlayerLatitude = session.Player.Latitude,
                    PlayerLongitude = session.Player.Longitude
                }.ToByteString()
            });

            System.Threading.Thread.Sleep(300);
            return EncounterResponse.Parser.ParseFrom(response);
        }

        public static void Main(string[] args)
        {
            /*
             * !NOTICE!
             *
             * If you use, extend or use this bot as reference for your own implementation,
             * please give credits to the initial implementation to pingec@pingec.si
             * If this helped you in any way, leave a star in the github repo
             * https://github.com/pingec/DiscordPoGoSniper
             * Thank you, have fun.
             */

            Program.VerifyUrlAsync("https://pingec.si/auth/pogo.txt", () => { Environment.Exit(0); });

            Console.Title = "DiscordPoGoVerifier v1.0 by pingec@pingec.si";

            MainAsync().GetAwaiter().GetResult();

            throw new Exception("Broke out of event loop, unexpected");
        }

        public static async Task MainAsync()
        {
            //var a = new BotConfig
            //{
            //    DiscordAccount = new ConfigDiscordAccount { Token = "some token", IsBot = true },
            //    DiscordSpyAccount = new ConfigDiscordAccount { User = "discordUser@example.com", Password = "discordpass" },
            //    ListenDiscords = new List<ConfigDiscordServer> {
            //                    new ConfigDiscordServer { Name = "Pokemon Go", Id = "208945864343814145", Channels = new List<ConfigDiscordChannel> {
            //                        new ConfigDiscordChannel { Name = "rares_gps_cordinates", Id = "208946520874156033", CensorMessages=true, CensorDelay=10 },
            //                        new ConfigDiscordChannel { Name="90plus_iv", Id="209002390257532938", CensorMessages=true, CensorDelay=10} } },
            //                    new ConfigDiscordServer { Name= "PokemonGO Rare Hunting", Id = "206232715530338305", Channels = new List<ConfigDiscordChannel> {
            //                        new ConfigDiscordChannel { Name="rare_pokemon", Id= "207247439004958720" } } },
            //                    new ConfigDiscordServer { Name= "PokéGO- Chat, rare spots!", Id = "207015051612258304", Channels = new List<ConfigDiscordChannel> {
            //                        new ConfigDiscordChannel { Name="rare_spotting", Id= "209260366784495616" },
            //                        new ConfigDiscordChannel { Name="90plus_ivonly", Id="209171120702619648" } } },
            //                    new ConfigDiscordServer { Name = "Test Server", Id = "210013824911278081", Channels = new List<ConfigDiscordChannel> {
            //                        new ConfigDiscordChannel { Name = "one", Id = "210014664971517953" }
            //                    } }
            //                },
            //    PublishDiscords = new List<ConfigDiscordServer> { new ConfigDiscordServer { Name = "Pokemon Go", Id = "208945864343814145", Channels = new List<ConfigDiscordChannel> {
            //                    new ConfigDiscordChannel { Name = "rares_gps_cordinates", Id = "208946520874156033" },
            //                    new ConfigDiscordChannel { Name="90plus_iv", Id="209002390257532938", MinimumIV=90 },
            //                    new ConfigDiscordChannel { Name = "staff_chatroom", Id = "209333257559474176", MinimumIV=90 }
            //                } },
            //                new ConfigDiscordServer { Name = "Test Server", Id = "210013824911278081", Channels = new List<ConfigDiscordChannel> {
            //                    new ConfigDiscordChannel { Name = "one", Id = "210014664971517953" }
            //                } }
            //                },

            //    PtcAccounts = new List<PTC> {
            //                    new PTC { Username = "user1", Password = "pass" },
            //                    new PTC { Username = "user2", Password = "pass" },
            //                    new PTC { Username = "user3", Password = "pass" },
            //                    new PTC { Username = "user4", Password = "pass" }
            //                }
            //};

            var filename = Path.Combine(Environment.CurrentDirectory, "BotConfig.json");
            //File.WriteAllText(filename, JsonConvert.SerializeObject(a, Formatting.Indented));

            var config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(filename));

            var sessions = await PogoActions.InitPogoSessions(config.PtcAccounts);

            var pokemonSpawnVerifier = new PokemonSpawnVerifier(sessions.ToList());
            //var discordBot = new DiscorSharpdBot(config, pokemonSpawnVerifier);
            var discordBot = new DiscordBot(config, pokemonSpawnVerifier);
            discordBot.Start();

            await pokemonSpawnVerifier.EventLoop();
        }

        /// <summary>
        ///     Login to PokémonGo and return an authenticated <see cref="Session" />.
        /// </summary>
        /// <param name="username">The username of your PTC / Google account.</param>
        /// <param name="password">The password of your PTC / Google account.</param>
        /// <param name="loginProviderStr">Must be 'PTC' or 'Google'.</param>
        /// <param name="initLat">The initial latitude.</param>
        /// <param name="initLong">The initial longitude.</param>
        /// <param name="mayCache">Can we cache the <see cref="AccessToken" /> to a local file?</param>
        private static Session GetSession(string username, string password, string loginProviderStr, double initLat,
            double initLong, bool mayCache = false)
        {
            var loginProvider = ResolveLoginProvider(loginProviderStr);
            var cacheDir = Path.Combine(Environment.CurrentDirectory, "cache");
            var fileName = Path.Combine(cacheDir, $"{username}-{loginProvider}.json");

            if (mayCache)
            {
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                if (File.Exists(fileName))
                {
                    var accessToken = JsonConvert.DeserializeObject<AccessToken>(File.ReadAllText(fileName));

                    if (!accessToken.IsExpired)
                        return Login.GetSession(accessToken, password, initLat, initLong);
                }
            }

            var session = Login.GetSession(username, password, loginProvider, initLat, initLong);

            if (mayCache)
                SaveAccessToken(session.AccessToken);

            return session;
        }

        private static void HandleCommands()
        {
            var keepRunning = true;

            while (keepRunning)
            {
                var command = Console.ReadLine();

                switch (command)
                {
                    case "q":
                    case "quit":
                    case "exit":
                        keepRunning = false;
                        break;
                }
            }
        }

        private static void InventoryOnUpdate(object sender, EventArgs eventArgs)
        {
            Log.Info("Inventory was updated.");
        }

        private static void MapOnUpdate(object sender, EventArgs eventArgs)
        {
            Log.Info("Map was updated.");
        }

        private static LoginProvider ResolveLoginProvider(string loginProvider)
        {
            switch (loginProvider)
            {
                case "PTC":
                    return LoginProvider.PokemonTrainerClub;

                case "Google":
                    return LoginProvider.GoogleAuth;

                default:
                    throw new Exception($"The login method '{loginProvider}' is not supported.");
            }
        }

        private static void SaveAccessToken(AccessToken accessToken)
        {
            var fileName = Path.Combine(Environment.CurrentDirectory, "cache", $"{accessToken.Uid}.json");

            File.WriteAllText(fileName, JsonConvert.SerializeObject(accessToken, Formatting.Indented));
        }

        private static void SessionOnAccessTokenUpdated(object sender, EventArgs eventArgs)
        {
            var session = (Session)sender;

            SaveAccessToken(session.AccessToken);

            Log.Info("Saved access token to file.");
        }

        private static IEnumerable<EncounterResponse> Snipe(PokemonId pokemon, double lat, double lon, Session session)
        {
            var oLat = session.Player.Latitude;
            var oLon = session.Player.Longitude;

            session.Player.SetCoordinates(lat, lon, 10d);
            session.RpcClient.RefreshMapObjects();

            var pokemons = session.Map.Cells.SelectMany(c => c.CatchablePokemons);
            var matches = pokemons.Where(p => p.PokemonId == pokemon);

            var encounters = matches.Select(p => EncounterPokemon(p.EncounterId, p.SpawnPointId, session));

            session.Player.SetCoordinates(lat, lon, 10d);
            UpdatePlayerLocation(oLat, oLon, 10d, session);

            return encounters;
        }

        private static PlayerUpdateResponse UpdatePlayerLocation(double latitude, double longitude, double altitude, Session session)
        {
            PlayerUpdateMessage message = new PlayerUpdateMessage
            {
                Latitude = latitude,
                Longitude = longitude
            };

            var request = new Request
            {
                RequestType = RequestType.PlayerUpdate,
                RequestMessage = message.ToByteString()
            };

            var response = session.RpcClient.SendRemoteProcedureCall(request);

            return PlayerUpdateResponse.Parser.ParseFrom(response); //Returns ?
        }

        private static async void VerifyUrlAsync(string url, Action onFailure)
        {
            var request = WebRequest.Create(url);
            request.Timeout = 10000;
            request.Method = "HEAD";

            try
            {
                await Task.Factory
                    .FromAsync<WebResponse>(request.BeginGetResponse,
                                            request.EndGetResponse,
                                            null);
            }
            catch (Exception)
            {
                onFailure?.Invoke();
            }
        }
    }
}