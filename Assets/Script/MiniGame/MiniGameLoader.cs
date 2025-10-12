using System.Collections;
using HHH.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HHH.MiniGame
{
    public class MiniGameLoader : BaseMono
    {
        public CoreServices Services;

        public IEnumerator Load(MiniGameDefinition def, System.Action<IMiniGame> onReady)
        {
            var op = SceneManager.LoadSceneAsync(def.SceneName, LoadSceneMode.Additive);
            yield return op;
            
            // Tìm một MiniGameBase trong scene
            var root = SceneManager.GetSceneByName(def.SceneName).GetRootGameObjects();
            IMiniGame mg = null;
            foreach(var go in root)
                if(go.TryGetComponent<IMiniGame>(out var c)) { mg = c; break; }

            // Tạo MiniGameContext
            var ctx = new MiniGameContext{
                Definition = def, 
                Services = Services,
                UIRoot = FindObjectOfType<Canvas>().transform
            };
            mg.Initialize(ctx);
            onReady?.Invoke(mg);
        }

        public IEnumerator Unload(string sceneName)
        {
            yield return SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}