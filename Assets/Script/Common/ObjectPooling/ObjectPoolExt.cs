using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HHH.Common
{
    public static class ObjectPoolExt
    {
        private static List<IRecycleHandle> _recycleHandles = new List<IRecycleHandle>(32);

        private static IObjectPool _objectPool;

        public static void Init(IObjectPool objectPool)
        {
            _objectPool = objectPool;
            if (objectPool != null)
                Debug.Log("CreatePool");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Recycle(this GameObject gameObject) => gameObject.GetComponent<IObjectInPool>()?.Recycle();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitRecycleHandle(this GameObject gameObject, float lifeTime)
        {
            _recycleHandles.Clear();
            gameObject.GetComponentsInChildren(false, _recycleHandles);
            foreach (var handle in _recycleHandles)
            {
                handle.SetRecycle(lifeTime);
            }

            if (_recycleHandles.Count == 0)
            {
                var recycleOnTime = gameObject.AddComponent<RecycleOnTime>();
                recycleOnTime.SetRecycle(lifeTime);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        
        private static GameObject LoadAndUse( Object prefab)
        {
            prefab.CreatePool();
            if (prefab.Use(out GameObject go) == false)
            {
                Debug.LogError($"Use asset addressable key: {prefab} fail!!!");
                return default;
            }
			
            return go;
        }
        
        public static void CreatePool(this Object prefab, uint cap = 1)=> _objectPool.CreatPool(prefab, cap);

        public static GameObject GetObjectInPool(this Object prefab, Transform parent, bool initialize = true, uint cap = 1)
        {
            var obj = LoadAndUse(prefab);
            obj.transform.SetParent(parent);
            obj.SetActive(initialize);
            return obj;
        }

        public static GameObject GetObjectInPool<T>(this Object prefab, Transform parent, bool initialize = true, uint cap = 1)
        {
            var obj = LoadAndUse(prefab);
            obj.transform.SetParent(parent);
            obj.SetActive(initialize);
            return obj;
        }

        public static GameObject GetObjectInPool(this Object prefab, Transform parent = null, bool worldPositionStays = false, bool initialize = true, uint cap = 1)
        {
            var obj = LoadAndUse(prefab);
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(initialize);
            return obj;
        }
        
        public static GameObject GetObjectInPool(this Object prefab, Vector3 pos, Transform parent = null, bool worldPositionStays = false ,bool initialize = true, uint cap = 1)
        {
            var obj = LoadAndUse(prefab);
            obj.transform.position = pos;
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(initialize);
            return obj;
        }
        
        public static T GetObjectInPool <T>(this Object prefab, Vector3 pos, Transform parent = null, bool worldPositionStays = false ,bool initialize = true, uint cap = 1) where T : UnityEngine.Component
        {
            var obj = LoadAndUse(prefab);
            obj.transform.position = pos;
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(initialize);
            return obj.GetComponent<T>();
        }
        
        public static GameObject GetObjectInPool(this Object prefab, Vector3 pos, Quaternion rotation,Transform parent = null, bool worldPositionStays = false,bool initialize = true, uint cap = 1)
        {
            var obj = LoadAndUse(prefab);
            obj.transform.position = pos;
            obj.transform.rotation = rotation;
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(initialize);
            return obj;
        }
        
        public static T GetObjectInPool<T>(this Object prefab, Vector3 pos, Quaternion rotation,Transform parent = null, bool worldPositionStays = false ,bool initialize = true, uint cap = 1) where T : UnityEngine.Component
        {
            var obj = LoadAndUse(prefab);
            obj.transform.position = pos;
            obj.transform.rotation = rotation;
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(initialize);
            return obj.GetComponent<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Use(this Object prefab, out GameObject go) => _objectPool.Use(prefab, out go);
		
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Use(this Object prefab, float lifeTime, out GameObject go) => _objectPool.Use(prefab, lifeTime, out go);


        public static void Instantiate(this IObjectPool pool, GameObject prefab, Vector3 position, Quaternion rotation, out GameObject go)
        {
            pool.CreatPool(prefab);
            pool.Use(prefab, out go);

            go.transform.position = position;
            go.transform.rotation = rotation;
        }

        public static void Instantiate(this IObjectPool pool, GameObject prefab, Transform parent, out GameObject go)
        {
            pool.CreatPool(prefab);
            pool.Use(prefab, out go);

            go.transform.SetParent(parent);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}