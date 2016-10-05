using log4net;
using POGOLib.Net;
using POGOProtos.Networking.Responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Demo
{
    internal class PokemonSpawnVerifier
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PokemonSpawnVerifier));
        private bool Running = false;
        private ConcurrentDictionary<Session, bool> SessionPool = new ConcurrentDictionary<Session, bool>();

        public PokemonSpawnVerifier(List<Session> sessions)
        {
            foreach (var session in sessions)
            {
                SessionPool.TryAdd(session, false);
            }
        }

        public VerificationQueue Queue { get; } = new VerificationQueue();

        public Task EventLoop()
        {
            if (Running)
                return Task.FromResult(false); // return a completed task

            Running = true;

            return Task.Run(async () =>
            {
                while (true)
                {
                    // if we have a free worker
                    var sessions = SessionPool.Where(pair => !pair.Value);
                    if (sessions.Count() > 0)
                    {
                        var session = sessions.FirstOrDefault();
                        // get one item out of queue
                        var item = Queue.GetNext();
                        if (item != null)
                        {
                            Log.Warn($"({session.Key.Player.Data.Username}) got {item.PokemonSpawn.PokemonId} from the QUEUE");
                            ProcessQueueItem(item, session.Key); //async work
                        }
                    }
                    await Task.Delay(500);
                }
            });
        }

        public void IgnoreExceptions(Action act)
        {
            try
            {
                act.Invoke();
            }
            catch { }
        }

        public async void ProcessQueueItem(VerificationQueueItem item, Session session)
        {
            // mark the session as busy
            SessionPool[session] = true;
            // do work
            Log.Warn($"({session.Player.Data.Username}) trying to snipe {item.PokemonSpawn.PokemonId}");

            IEnumerable<EncounterResponse> responses = null;
            try
            {
                responses = await Task.Run(() =>
                {
                    //supress any exception or lose the session forever (flagged as inactive)
                    return PogoActions.Snipe(item.PokemonSpawn.PokemonId, item.PokemonSpawn.Latitude, item.PokemonSpawn.Longitude, session);
                });
            }
            catch (Exception) { }

            Log.Warn($"({session.Player.Data.Username}) finished sniping {item.PokemonSpawn.PokemonId}");

            Task.Run(async () =>
            {
                await Task.Delay(10000);
                // mark the session as free in 5 seconds (because GetMapObjects is rate limited)
                SessionPool[session] = false;
            });

            if (responses == null || responses.Count() < 1)
                return;

            var response = responses.First();
            if (response.Status != EncounterResponse.Types.Status.EncounterSuccess)
            {
                Log.Warn($"ENCOUNTER {item.PokemonSpawn.PokemonId} FAILED: {response.Status}");
                return;
            }

            var pokemonData = response.WildPokemon?.PokemonData;

            if (pokemonData == null)
            {
                return;
            }
            Log.Warn($"({session.Player.Data.Username}) sniping of {item.PokemonSpawn.PokemonId} was successfull!");

            // success!
            Queue.Remove(item.PokemonSpawn); //try to remove frome queue (it could already have been removed)
            if (!item.Resolved) //ensure only one thread actually calls onverified handler (race condition)
            {
                item.Resolved = true;
                Log.Warn($"({session.Player.Data.Username}) removing {item.PokemonSpawn.PokemonId} from QUEUE, firing OnVerified callback!");
                item.OnVerified?.Invoke(response);
            }
            // trigger onverified handler if not too late
            //if (stillQueued)
            //{
            //item.OnVerified?.Invoke(response);
            //}
        }
    }
}