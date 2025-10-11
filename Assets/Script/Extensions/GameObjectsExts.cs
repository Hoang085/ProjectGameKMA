using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace HHH.Extensions
{
    static class GameObjectExts
    {
        /// <summary>
        /// Get Or Add A Component to game object
        /// </summary>
        /// <param name="gameObject"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : UnityEngine.Component
        {
            var t = gameObject.GetComponent<T>();
            if (t == default)
                t = gameObject.AddComponent<T>();

            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Gets<T>(this GameObject go, ref List<T> lstComponents, bool inChildren = false,
            bool includeInactive = true)
        {
            if (inChildren)
                go.GetComponentsInChildren<T>(includeInactive, lstComponents);
            else
                go.GetComponents<T>(lstComponents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventTrigger AddEventTrigger(this GameObject go, EventTriggerType triggerType, UnityAction<BaseEventData> callback )
        {
            var eventTrigger = GetOrAddComponent<EventTrigger>(go);

            var entry = new EventTrigger.Entry();
            entry.eventID = triggerType;
            entry.callback.AddListener(callback);

            eventTrigger.triggers.Add(entry);

            return eventTrigger;
        }
        
        // /// <summary>
        // /// Inject extension methods
        // /// </summary>
        // /// <param name="gameObject"></param>
        // /// <param name="inst"></param>
        // /// <param name="includeInactive"></param>
        // /// <typeparam name="T"></typeparam>
        // public static void InjectInstance<T>(this GameObject gameObject, T inst, bool includeInactive = true)
        //     where T : class
        // {
        //     ObjectInjection.InjectInstance(gameObject, inst);
        // }
    }
}