using log4net;
using POGOProtos.Networking.Responses;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo
{
    internal class VerificationQueue
    {
        public int MaxFailures = 5;
        private static readonly ILog Log = LogManager.GetLogger(typeof(VerificationQueue));
        private List<VerificationQueueItem> Queue = new List<VerificationQueueItem>();
        private Object queueLock = new Object();

        public void Enqueue(PokemonSpawn pokemonSpawn, Action<EncounterResponse> onVerified)
        {
            lock (queueLock)
            {
                if (Get(pokemonSpawn) == null)
                {
                    var item = new VerificationQueueItem
                    {
                        PokemonSpawn = pokemonSpawn,
                        Weight = 0,
                        OnVerified = onVerified
                    };
                    Queue.Add(item);
                }
            }
        }

        public VerificationQueueItem Get(PokemonSpawn pokemonSpawn)
        {
            VerificationQueueItem item = null;
            lock (queueLock)
            {
                item = Queue.Where(i => i.PokemonSpawn.Equals(pokemonSpawn)).FirstOrDefault();
            }
            return item;
        }

        public VerificationQueueItem GetNext()
        {
            lock (queueLock)
            {
                Queue.Sort((i1, i2) => i1.Weight.CompareTo(i2.Weight));
                var item = Queue.FirstOrDefault(i => !i.Resolved && i.Weight < MaxFailures);
                if (item == null)
                    return null;

                item.Weight++;
                //if (item.Weight == MaxFailures)
                //{
                //    Log.Warn($"This {item.PokemonSpawn.PokemonId} sux, tried to snipe it {MaxFailures} of times and still no luck!");
                //    //Queue.Remove(item);
                //}
                return item;
            }
        }

        public bool Remove(PokemonSpawn pokemonSpawn)
        {
            lock (queueLock)
            {
                var item = Get(pokemonSpawn);
                if (item != null)
                {
                    Queue.Remove(item);
                    Log.Warn($"Queue HIT: {item.PokemonSpawn.PokemonId}");
                    return true;
                }
                Log.Warn($"Queue MISSED: {pokemonSpawn.PokemonId}");
                return false;
            }
        }
    }

    internal class VerificationQueueItem
    {
        public Action<EncounterResponse> OnVerified;
        public PokemonSpawn PokemonSpawn;
        public bool Resolved = false;
        public int Weight;
    }
}