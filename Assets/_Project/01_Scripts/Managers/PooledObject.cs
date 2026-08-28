using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    private IPoolable[] _callbacks = Array.Empty<IPoolable>();
    private bool _isSpawned;

    internal PoolingManager Owner { get; private set; }
    internal GameObject SourcePrefab { get; private set; }

    internal void Initialize(PoolingManager owner, GameObject sourcePrefab)
    {
        Owner = owner;
        SourcePrefab = sourcePrefab;
        CacheCallbacks();
    }

    internal void MarkSpawned()
    {
        _isSpawned = true;
    }

    internal bool TryMarkReleased()
    {
        if (!_isSpawned)
        {
            return false;
        }

        _isSpawned = false;
        return true;
    }

    internal void NotifySpawned()
    {
        for (int i = 0; i < _callbacks.Length; i++)
        {
            _callbacks[i].OnSpawned();
        }
    }

    internal void NotifyDespawned()
    {
        for (int i = 0; i < _callbacks.Length; i++)
        {
            _callbacks[i].OnDespawned();
        }
    }

    public void Release()
    {
        if (Owner != null)
        {
            Owner.Release(this);
        }
    }

    private void CacheCallbacks()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        int callbackCount = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPoolable)
            {
                callbackCount++;
            }
        }

        _callbacks = new IPoolable[callbackCount];
        int callbackIndex = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPoolable callback)
            {
                _callbacks[callbackIndex++] = callback;
            }
        }
    }
}
