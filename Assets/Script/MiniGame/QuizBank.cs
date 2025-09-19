using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    [CreateAssetMenu(menuName="MiniGame/Quiz/Bank")]
    public class QuizBank : BaseSO
    {
        [System.Serializable]
        public class QuizItem
        {
            public string Question;
            public string[] Options; // A,B,C,D
            public int CorrectIndex;
            public string Category;  // Math/English/Science
            public int Difficulty;   // 1..5
        }
        public QuizItem[] Items;
    }
}