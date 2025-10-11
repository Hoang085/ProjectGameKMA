using HHH.Common;

namespace HHH.MiniGame
{
    using UnityEngine;
    public abstract class MiniGameBase : BaseMono, IMiniGame
    {
        protected MiniGameContext Ctx;
        public event System.Action<MiniGameResult> OnGameEnded;

        public virtual void Initialize(MiniGameContext ctx) => Ctx = ctx;
        public abstract void StartGame();
        public virtual void Pause()  => Time.timeScale = 0f;
        public virtual void Resume() => Time.timeScale = 1f;

        protected void Finish(MiniGameResult result)
        {
            //Ctx?.Services.Save.PushResult(Ctx.Definition.Id, result);
            OnGameEnded?.Invoke(result);
        }

        public virtual void EndGame() { }
    }
}