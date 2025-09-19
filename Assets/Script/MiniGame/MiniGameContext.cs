namespace HHH.MiniGame
{
    using UnityEngine;
    [System.Serializable]
    public class MiniGameContext
    {
        public MiniGameDefinition Definition;
        public CoreServices Services;
        public Transform UIRoot;
        public object SessionData; // optional: pass data từ game lớn
    }

    public class CoreServices
    {
        // public EventBus Events;
        // public TimerService Timer;
        // public SaveService Save;
        // public LocalizationService Loc;
        // public AudioService Audio;
        // public InputRouter Input;
    }
}