using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public class ObjectPool<T> where T : MonoBehaviour
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _pool = new();

        public ObjectPool(T prefab, int initialSize, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < initialSize; i++)
                CreateNew();
        }

        private T CreateNew()
        {
            T obj = Object.Instantiate(_prefab, _parent);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
            return obj;
        }

        public T Get(Vector3 position, Quaternion rotation = default)
        {
            if (_pool.Count == 0) CreateNew();
            T obj = _pool.Dequeue();
            obj.transform.position = position;
            obj.transform.rotation = rotation == default ? Quaternion.identity : rotation;
            obj.gameObject.SetActive(true);
            return obj;
        }

        public void Return(T obj)
        {
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }
}
