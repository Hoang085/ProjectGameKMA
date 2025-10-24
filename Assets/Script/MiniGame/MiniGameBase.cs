using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    public abstract class MiniGameBase : BaseMono, IMiniGame
    {
        protected MiniGameContext Ctx;
        public event System.Action<MiniGameResult> OnGameEnded;

        public virtual void Initialize(MiniGameContext ctx) => Ctx = ctx;
        public abstract void StartGame();

        public virtual void Pause() => Time.timeScale = 0f;
        public virtual void Resume() => Time.timeScale = 1f;

        /// <summary> Kết thúc minigame và quay về GameScene. </summary>
        protected void Finish(MiniGameResult result)
        {
            OnGameEnded?.Invoke(result);
            PlayerPrefs.SetInt("ShouldRestoreStateAfterMiniGame", 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        public virtual void EndGame() { }
    }
}
