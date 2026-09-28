using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonSong.Combat.Pooling
{
    /// <summary>
    /// Minimal prefab pool shared by projectiles, VFX and pooled enemies. Deliberately
    /// small: it exists so that nothing in the combat or enemy systems has to call
    /// Instantiate/Destroy in a hot path, not to be a general asset-management layer.
    /// Pools are dropped when a scene unloads, so nothing leaks across rooms.
    /// </summary>
    public static class PrefabPool
    {
        private const int DefaultCapacity = 8;

        private static readonly Dictionary<EntityId, Stack<GameObject>> Available = new Dictionary<EntityId, Stack<GameObject>>();
        private static readonly Dictionary<EntityId, EntityId> SourceByInstance = new Dictionary<EntityId, EntityId>();
        private static Transform root;
        private static bool hooked;

        /// <summary>Instances currently parked in the pool for <paramref name="prefab"/>.</summary>
        public static int CountInactive(GameObject prefab)
        {
            return prefab != null && Available.TryGetValue(prefab.GetEntityId(), out Stack<GameObject> stack)
                ? stack.Count
                : 0;
        }

        /// <summary>Creates instances up front so the first shot of a fight does not hitch.</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            EnsureHooks();
            Stack<GameObject> stack = GetStack(prefab.GetEntityId());

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Object.Instantiate(prefab, Root);
                instance.SetActive(false);
                SourceByInstance[instance.GetEntityId()] = prefab.GetEntityId();
                stack.Push(instance);
            }
        }

        /// <summary>
        /// Takes an instance from the pool, or instantiates one when the pool is empty.
        /// </summary>
        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            EnsureHooks();
            EntityId prefabId = prefab.GetEntityId();
            Stack<GameObject> stack = GetStack(prefabId);

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
            {
                // Null entries appear when a pooled object is destroyed behind our back.
                instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                SourceByInstance[instance.GetEntityId()] = prefabId;
            }

            Transform t = instance.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            var poolables = instance.GetComponents<IPoolable>();
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawnedFromPool();
            }

            return instance;
        }

        /// <summary>
        /// Returns an instance to its pool. Instances this pool did not create are
        /// destroyed instead, so callers can always release without checking.
        /// </summary>
        public static void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var poolables = instance.GetComponents<IPoolable>();
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnReturnedToPool();
            }

            if (!SourceByInstance.TryGetValue(instance.GetEntityId(), out EntityId prefabId))
            {
                Object.Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(Root, false);
            GetStack(prefabId).Push(instance);
        }

        /// <summary>Forgets every pooled instance. Called automatically on scene unload.</summary>
        public static void Clear()
        {
            Available.Clear();
            SourceByInstance.Clear();
            root = null;
        }

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var holder = new GameObject("~PrefabPool");
                    holder.hideFlags = HideFlags.NotEditable;
                    root = holder.transform;
                }

                return root;
            }
        }

        private static Stack<GameObject> GetStack(EntityId prefabId)
        {
            if (!Available.TryGetValue(prefabId, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(DefaultCapacity);
                Available[prefabId] = stack;
            }

            return stack;
        }

        private static void EnsureHooks()
        {
            if (hooked)
            {
                return;
            }

            hooked = true;
            SceneManager.sceneUnloaded += _ => Clear();
        }
    }
}
