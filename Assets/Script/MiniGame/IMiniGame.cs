using System;

namespace HHH.MiniGame
{
    public interface IMiniGame
    {
        event System.Action<MiniGameResult> OnGameEnded;
        
        void Initialize(MiniGameContext ctx);     // inject services/data
        void StartGame();
        void Pause();
        void Resume();
        void EndGame();   
    }
}