using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    [CreateAssetMenu(fileName="MiniGameDefinition", menuName="MiniGame/Definition")]
    public class MiniGameDefinition : BaseSO
    {
        public string Id;                 // "ZTYPE", "CPQ", "FILLBLANK"
        public string DisplayName;
        public string SceneName;          // Additive scene to load
        public Sprite Icon;
        public DifficultyCurve Difficulty; // tốc độ, số lượng, vv
    }
    
    [System.Serializable]
    public class DifficultyCurve
    {
        public AnimationCurve SpeedByLevel;
        public AnimationCurve CountByLevel;
        public AnimationCurve WordLengthByLevel;
    }
}