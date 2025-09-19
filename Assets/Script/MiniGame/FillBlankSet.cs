using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    [CreateAssetMenu(menuName="MiniGame/FillBlank/Set")]
    public class FillBlankSet : BaseSO
    {
        [System.Serializable]
        public class BlankItem
        {
            [TextArea] public string Prompt; // "The ___ jumps over ___"
            public string[] Answers;         // theo thứ tự ô
            public string[] Distractors;     // cho WordBank/MCQ
            public bool FreeInput;           // true => nhập tự do
        }
        public BlankItem[] Items;
    }
}