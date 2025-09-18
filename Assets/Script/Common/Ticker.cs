using System;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HHH.Common
{
    [CreateAssetMenu]
    public class Ticker : ScriptableObject
    {
        [SerializeField] TickerMono tickerMonoPrefab;

        event Action OnEarlyTickEvent;
        event Action OnTickEvent;
        event Action OnFixedTickEvent;
        event Action OnLateTickEvent;

        TickerMono tickerMono;

        public void SubEarlyTick(IEntity entity)
        {
            Validate();
            OnEarlyTickEvent += entity.EarlyTick;
        }

        public void UnsubEarlyTick(IEntity entity)
        {
            OnEarlyTickEvent -= entity.EarlyTick;
        }

        public void SubTick(IEntity entity)
        {
            Validate();
            OnTickEvent += entity.Tick;
        }

        public void UnsubTick(IEntity entity)
        {
            OnTickEvent -= entity.Tick;
        }

        public void SubLateTick(IEntity entity)
        {
            Validate();
            OnLateTickEvent += entity.LateTick;
        }

        public void UnsubLateTick(IEntity entity)
        {
            OnLateTickEvent -= entity.LateTick;
        }

        public void SubFixedTick(IEntity entity)
        {
            Validate();
            OnFixedTickEvent += entity.FixedTick;
        }

        public void UnsubFixedTick(IEntity entity)
        {
            OnFixedTickEvent -= entity.FixedTick;
        }

        public void EarlyTick()
        {
            OnEarlyTickEvent?.Invoke();
        }

        public void Tick()
        {
            OnTickEvent?.Invoke();
            DOTween.ManualUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        }

        public void LateTick()
        {
            OnLateTickEvent?.Invoke();
        }

        public void FixedTick()
        {
            OnFixedTickEvent?.Invoke();
        }

        void Validate()
        {
            if (!tickerMono)
            {
                tickerMono = Instantiate(tickerMonoPrefab);
                tickerMono.name = "Ticker";
                tickerMono.Ticker = this;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Bind")]
        public void AutoBind()
        {
            var soes = AssetUtils.FindAssetAtFolder<BaseSO>(new string[] {"Assets"});
            foreach (var so in soes)
            {
                so.ticker = this;
                EditorUtility.SetDirty(so);
            }
            
            var goes = AssetUtils.FindAssetAtFolder<GameObject>(new string[] {"Assets"});
            foreach (var go in goes)
            {
                var monoes = go.GetComponentsInChildren<BaseMono>(true);
                foreach (var mono in monoes)
                {
                    mono.ticker = this;
                    EditorUtility.SetDirty(mono);
                }
            }
        }
#endif
    }
}