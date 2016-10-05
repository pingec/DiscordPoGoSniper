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
using System.Threading.Tasks;

namespace Demo
{
    public static class PogoActions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PogoActions));

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

        public static Task<Session[]> InitPogoSessions(List<PTC> ptcAccounts)
        {
            List<Task<Session>> tasks = new List<Task<Session>>();
            foreach (var ptcAccount in ptcAccounts)
            {
                var task = Task.Run<Session>(() =>
                {
                    var latitude = 51.507351; // Somewhere in London
                    var longitude = -0.127758;
                    var ptcUser = ptcAccount.Username;
                    var ptcPass = ptcAccount.Password;

                    var session = GetSession(ptcUser, ptcPass, "PTC", latitude,
                        longitude, true);

                    SaveAccessToken(session.AccessToken);

                    session.AccessTokenUpdated += SessionOnAccessTokenUpdated;
                    session.Player.Inventory.Update += InventoryOnUpdate;
                    session.Map.Update += MapOnUpdate;

                    // Send initial requests and start HeartbeatDispatcher
                    //session.Startup();
                    if (!session.RpcClient.Startup())
                    {
                        throw new Exception("Failed to init session for " + ptcAccount.Username);
                    }

                    // session.
                    session.Player.SetCoordinates(latitude, longitude, 10d);
                    session.RpcClient.RefreshMapObjects();

                    Log.Warn($"{ptcAccount.Username} ready");

                    return session;
                });
                tasks.Add(task);
            }

            return Task.WhenAll<Session>(tasks);
        }

        public static IEnumerable<EncounterResponse> Snipe(PokemonId pokemon, double lat, double lon, Session session)
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
            var loginProvider = LoginProvider.PokemonTrainerClub;
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

        private static void InventoryOnUpdate(object sender, EventArgs eventArgs)
        {
            Log.Info("Inventory was updated.");
        }

        private static void MapOnUpdate(object sender, EventArgs eventArgs)
        {
            Log.Info("Map was updated.");
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
    }
}