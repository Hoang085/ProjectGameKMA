using System;
using System.Linq;
using System.Threading;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HHH.Common
{
    public abstract class BaseMono : SerializedMonoBehaviour, IEntity
    {
        [Header("Base")]
        [SerializeField, NamedId] string id;
        [SerializeField] public Ticker ticker;
        [SerializeField] bool earlyTick;
        [SerializeField] bool tick;
        [SerializeField] bool lateTick;
        [SerializeField] bool fixedTick;

        [SerializeField] bool initOnStart;
        protected CancellationTokenSource cancellationTokenSource;
        public string Id => id;

#if UNITY_EDITOR
        [ContextMenu("ResetId")]
        public void ResetId()
        {
            id = StringUtils.ToSnakeCase(name);
            EditorUtility.SetDirty(this);
        }
#endif

        void OnEnable()
        {
            BindVariable();
            ListenEvents();
            SubTick();
            DoEnable();
        }

        void Start()
        {
            if (initOnStart) Initialize();
        }

        private void Update()
        {
            Tick();
        }

        void OnDisable()
        {
            DoDisable();
            UnsubTick();
            StopListenEvents();
            UnbindVariable();
        }

        void SubTick()
        {
            if (earlyTick) ticker.SubEarlyTick(this);
            if (tick) ticker.SubTick(this);
            if (lateTick) ticker.SubLateTick(this);
            if (fixedTick) ticker.SubFixedTick(this);
        }

        void UnsubTick()
        {
            if (earlyTick) ticker.UnsubEarlyTick(this);
            if (tick) ticker.UnsubTick(this);
            if (lateTick) ticker.UnsubLateTick(this);
            if (fixedTick) ticker.UnsubFixedTick(this);
        }

        #region Implement IEntity
        public virtual void BindVariable()
        {
        }

        public virtual void ListenEvents()
        {
        }

        public virtual void DoEnable()
        {
        }

        public virtual void Initialize()
        {
        }

        public virtual void EarlyTick()
        {
        }

        public virtual void Tick()
        {
        }

        public virtual void LateTick()
        {
        }

        public virtual void FixedTick()
        {
        }

        public virtual void CleanUp()
        {
        }

        public virtual void DoDisable()
        {
        }

        public virtual void StopListenEvents()
        {
        }

        public virtual void UnbindVariable()
        {
        }
        #endregion

#if UNITY_EDITOR
        void Reset()
        {
            ticker = AssetUtils.FindAssetAtFolder<Ticker>(new string[] { "Assets" }).FirstOrDefault();
            ResetId();
            EditorUtility.SetDirty(this);
        }
#endif
    }
}