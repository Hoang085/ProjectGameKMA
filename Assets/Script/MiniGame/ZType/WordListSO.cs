using System.Collections.Generic;
using UnityEngine;

namespace HHH.MiniGame
{
    [CreateAssetMenu(menuName = "MiniGame/ZType/WordData", fileName = "WordListSO")]
    public class WordListSO : ScriptableObject
    {
        [TextArea(2, 10)] public string wordsRaw;

        private Dictionary<int, List<string>> _wordsByLength;

        public void Initialize()
        {
            _wordsByLength = new Dictionary<int, List<string>>();

            // Tách theo dấu phẩy, xuống dòng, hoặc dấu cách
            var separators = new char[] { ',', '\n', '\r', ' ' };
            foreach (var raw in wordsRaw.Split(separators, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var w = raw.Trim().Trim('"').ToLower();
                if (string.IsNullOrEmpty(w)) continue;
                int len = w.Length;

                if (!_wordsByLength.ContainsKey(len))
                    _wordsByLength[len] = new List<string>();
                _wordsByLength[len].Add(w);
            }
        }

        public List<string> GetWords(int minLength = 3, int maxLength = 10)
        {
            if (_wordsByLength == null) Initialize();

            var result = new List<string>();
            for (int i = minLength; i <= maxLength; i++)
            {
                if (_wordsByLength.ContainsKey(i))
                    result.AddRange(_wordsByLength[i]);
            }
            return result.Count > 0 ? result : new List<string> { "test", "word", "alpha", "beta" };
        }
    }
}
