using HHH.Common;

namespace HHH.MiniGame
{
    using UnityEngine;
    [CreateAssetMenu(menuName="MiniGame/Ztype/LevelData")]
    public class ZtypeLevelData : BaseSO
    {
        public string[] WordBank;
        public float BaseSpeed = 1.0f;
        public int Waves = 5;
        public int EnemiesPerWave = 6;
        public float SpawnInterval = 0.8f;
        public float EnemySpeedIncrement = 0.2f;
    }
}