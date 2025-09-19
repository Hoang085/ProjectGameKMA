using System;
using UnityEngine;

namespace HHH.Common
{
    class ObjectInPool : MonoBehaviour, IObjectInPool
    {
        public int PoolID { get; private  set; }

        private IObjectPool _pool;
        private IOnRecycle[] _recycleCallbacks =  Array.Empty<IOnRecycle>();

        public void InitPool(int poolID, IObjectPool poolHandle)
        {
            PoolID = poolID;
            _pool = poolHandle;
            
            _recycleCallbacks =  GetComponents<IOnRecycle>() ?? Array.Empty<IOnRecycle>();
        }

        public void Recycle()
        {
            // gameObject.SetActive(false);
            _pool?.Recycle(PoolID, gameObject);
         
            foreach (var cb in _recycleCallbacks)
            {
                cb?.OnRecycle();
            }
        }
    }
}