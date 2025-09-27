using System.Collections.Generic;
using HHH.Common;
using NUnit.Framework;
using UnityEngine;

namespace HHH.MiniGame
{
    [CreateAssetMenu(menuName = "MiniGame/ZType", fileName = "WordListSO")]
    public class WordListSO : BaseSO
    {
        [TextArea(2, 10)] public string wordsRaw;

        public List<string> GetWords()
        {
            var list = new List<string>();
            foreach (var line in wordsRaw.Split())
            {
                var w = line.Trim();
                if (!string.IsNullOrEmpty(w)) list.Add(w.ToLower());
            }

            return list;
        }
    }
}