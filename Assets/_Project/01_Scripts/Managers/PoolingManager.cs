using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class PoolingManager : MonoBehaviour
{
    [Serializable]
    private sealed class PoolDefinition
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int initialSize = 32;
        [SerializeField, Min(1)] private int maxSize = 256;

        public GameObject Prefab => prefab;
        public int InitialSize => initialSize;
        public int MaxSize => Mathf.Max(initialSize, maxSize);
    }

    private sealed class PoolRuntime
    {
        public PoolRuntime(ObjectPool<PooledObject> pool)
        {
            Pool = pool;
        }

        public ObjectPool<PooledObject> Pool { get; }
    }

    public static PoolingManager Instance { get; private set; }

    [Header("Registered Pools")]
    [SerializeField] private PoolDefinition[] pools = Array.Empty<PoolDefinition>();

    [Header("Fallback Pool")]
    [SerializeField, Min(0)] private int defaultInitialSize = 16;
    [SerializeField, Min(1)] private int defaultMaxSize = 256;

    private readonly Dictionary<GameObject, PoolRuntime> _runtimePools =
        new Dictionary<GameObject, PoolRuntime>(8);

    private Transform _inactiveRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateInactiveRoot();

        for (int i = 0; i < pools.Length; i++)
        {
            PoolDefinition definition = pools[i];
            if (definition == null || definition.Prefab == null)
            {
                continue;
            }

            if (_runtimePools.ContainsKey(definition.Prefab))
            {
                Debug.LogWarning($"Duplicate pool definition ignored: {definition.Prefab.name}", this);
                continue;
            }

            PoolRuntime runtime = CreatePool(
                definition.Prefab,
                definition.InitialSize,
                definition.MaxSize);

            _runtimePools.Add(definition.Prefab, runtime);
            Prewarm(runtime.Pool, definition.InitialSize);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gets an instance from the pool and activates it after its transform is configured.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("Cannot get a null prefab from the pool.", this);
            return null;
        }

        PoolRuntime runtime = GetOrCreatePool(prefab);
        PooledObject pooledObject = runtime.Pool.Get();
        GameObject instance = pooledObject.gameObject;
        Transform instanceTransform = instance.transform;

        instanceTransform.SetParent(transform, false);
        instanceTransform.SetPositionAndRotation(position, rotation);

        pooledObject.MarkSpawned();
        instance.SetActive(true);
        pooledObject.NotifySpawned();

        return instance;
    }

    /// <summary>
    /// Returns a spawned instance to its originating pool.
    /// </summary>
    public void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledObject pooledObject = instance.GetComponent<PooledObject>();
        if (pooledObject == null || pooledObject.Owner != this)
        {
            Debug.LogWarning($"{instance.name} does not belong to this pool manager.", instance);
            return;
        }

        Release(pooledObject);
    }

    internal void Release(PooledObject pooledObject)
    {
        if (pooledObject == null || pooledObject.Owner != this || !pooledObject.TryMarkReleased())
        {
            return;
        }

        if (_runtimePools.TryGetValue(pooledObject.SourcePrefab, out PoolRuntime runtime))
        {
            runtime.Pool.Release(pooledObject);
        }
    }

    private void CreateInactiveRoot()
    {
        GameObject root = new GameObject("[Inactive Pool]");
        _inactiveRoot = root.transform;
        _inactiveRoot.SetParent(transform, false);
        root.SetActive(false);
    }

    private PoolRuntime GetOrCreatePool(GameObject prefab)
    {
        if (_runtimePools.TryGetValue(prefab, out PoolRuntime runtime))
        {
            return runtime;
        }

        int maxSize = Mathf.Max(defaultInitialSize, defaultMaxSize);
        runtime = CreatePool(prefab, defaultInitialSize, maxSize);
        _runtimePools.Add(prefab, runtime);
        Prewarm(runtime.Pool, defaultInitialSize);
        return runtime;
    }

    private PoolRuntime CreatePool(GameObject prefab, int initialSize, int maxSize)
    {
        var pool = new ObjectPool<PooledObject>(
            createFunc: () => CreateInstance(prefab),
            actionOnGet: null,
            actionOnRelease: StoreInstance,
            actionOnDestroy: DestroyInstance,
            collectionCheck: false,
            defaultCapacity: Mathf.Max(1, initialSize),
            maxSize: Mathf.Max(1, maxSize));

        return new PoolRuntime(pool);
    }

    private PooledObject CreateInstance(GameObject prefab)
    {
        // The inactive parent prevents OnEnable from running at an invalid spawn position.
        GameObject instance = Instantiate(prefab, _inactiveRoot);
        instance.name = prefab.name;
        instance.SetActive(false);

        PooledObject pooledObject = instance.GetComponent<PooledObject>();
        if (pooledObject == null)
        {
            pooledObject = instance.AddComponent<PooledObject>();
        }

        pooledObject.Initialize(this, prefab);
        return pooledObject;
    }

    private void StoreInstance(PooledObject pooledObject)
    {
        pooledObject.NotifyDespawned();
        GameObject instance = pooledObject.gameObject;
        instance.SetActive(false);
        instance.transform.SetParent(_inactiveRoot, false);
    }

    private static void DestroyInstance(PooledObject pooledObject)
    {
        Destroy(pooledObject.gameObject);
    }

    private static void Prewarm(ObjectPool<PooledObject> pool, int count)
    {
        if (count <= 0)
        {
            return;
        }

        // All instances must be held until creation is complete; otherwise the same
        // top item would be fetched and returned repeatedly.
        var instances = new PooledObject[count];
        for (int i = 0; i < count; i++)
        {
            instances[i] = pool.Get();
        }

        for (int i = 0; i < count; i++)
        {
            pool.Release(instances[i]);
        }
    }
}
