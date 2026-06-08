using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Lightweight particle pool for fire-and-forget world-space effects.
    /// Effects that need to outlive their spawner (e.g. ShooterBlock depletion burst)
    /// are played via this pool so they are never cut off by SetActive(false).
    ///
    /// Pool starts with a small number of pre-warmed instances and grows
    /// automatically if all are busy — no manual pool size configuration needed.
    /// </summary>
    public class ParticlePoolManager : MonoBehaviour
    {
        public static ParticlePoolManager Instance { get; private set; }

        // Number of instances to pre-create per entry to avoid first-frame GC spikes.
        private const int DefaultPreWarm = 3;

        [System.Serializable]
        public class PoolEntry
        {
            public string         id;
            public ParticleSystem prefab;
        }

        [Header("Pool Definitions")]
        public List<PoolEntry> entries = new();

        // id -> available instances
        private readonly Dictionary<string, Queue<ParticleSystem>> _pools   = new();
        // prefab lookup so we can instantiate extras on demand
        private readonly Dictionary<string, ParticleSystem>        _prefabs = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var entry in entries)
                PreWarm(entry);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a pooled particle at the given world position.
        /// If all instances are in use a new one is created automatically.
        /// </summary>
        public void Play(string id, Vector3 worldPosition)
        {
            var ps = Rent(id);
            if (ps == null) return;

            ps.transform.position = worldPosition;
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();

            StartCoroutine(ReturnWhenDone(ps, id));
        }

        /// <summary>
        /// Plays a pooled particle at the given world position after a delay.
        /// Runs on the manager's persistent coroutine system so it outlives the caller.
        /// </summary>
        public void PlayDelayed(string id, Vector3 worldPosition, float delay)
        {
            StartCoroutine(PlayDelayedRoutine(id, worldPosition, delay));
        }

        private System.Collections.IEnumerator PlayDelayedRoutine(string id, Vector3 worldPosition, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            Play(id, worldPosition);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void PreWarm(PoolEntry entry)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[ParticlePoolManager] Entry '{entry.id}' has no prefab assigned.");
                return;
            }

            _prefabs[entry.id] = entry.prefab;

            var queue = new Queue<ParticleSystem>(DefaultPreWarm);
            for (int i = 0; i < DefaultPreWarm; i++)
                queue.Enqueue(CreateInstance(entry.id, entry.prefab, i));

            _pools[entry.id] = queue;
        }

        private ParticleSystem Rent(string id)
        {
            if (!_pools.TryGetValue(id, out var queue))
            {
                Debug.LogWarning($"[ParticlePoolManager] Pool id '{id}' not found.");
                return null;
            }

            // Pool exhausted — create a new instance on the fly and grow automatically
            if (queue.Count == 0)
            {
                if (!_prefabs.TryGetValue(id, out var prefab) || prefab == null) return null;
                return CreateInstance(id, prefab, -1);
            }

            return queue.Dequeue();
        }

        private ParticleSystem CreateInstance(string id, ParticleSystem prefab, int index)
        {
            var ps = Instantiate(prefab, transform);
            ps.name = index >= 0 ? $"[Pool] {id}_{index}" : $"[Pool] {id}_dyn";
            ps.gameObject.SetActive(false);
            return ps;
        }

        private System.Collections.IEnumerator ReturnWhenDone(ParticleSystem ps, string id)
        {
            yield return new WaitWhile(() => ps != null && ps.IsAlive(true));

            if (ps == null) yield break;

            ps.gameObject.SetActive(false);

            if (_pools.TryGetValue(id, out var queue))
                queue.Enqueue(ps);
        }
    }
}
