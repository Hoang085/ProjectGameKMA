using HHH.Common;

namespace HHH.MiniGame
{
    public class MiniGameManager : BaseMono
    {
        public MiniGameDefinition Definition;
        public MiniGameLoader Loader;

        private IMiniGame _game;

        public void Play(System.Action<MiniGameResult> onEnded)
        {
            StartCoroutine(Loader.Load(Definition, mg => {
                _game = mg;
                _game.OnGameEnded += r => {
                    onEnded?.Invoke(r);
                    StartCoroutine(Loader.Unload(Definition.SceneName));
                };
                _game.StartGame();
            }));
        }
    }
}