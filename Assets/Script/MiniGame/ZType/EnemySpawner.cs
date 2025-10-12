using System.Collections.Generic;
using UnityEngine;
using HHH.Common;
using Sirenix.OdinInspector;

namespace HHH.MiniGame
{
    public class EnemySpawner : BaseMono
    {
        [Header("Config")] 
        [SerializeField] private GameObject parent;
        [SerializeField] private WordListSO wordList;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int maxEnemiesOnScene = 5;
        [SerializeField] private float maxEnemiesIncreaseRate = 20f;
        [SerializeField] private float spawnInterval = 1.6f;
        [SerializeField] private float minSpawnInterval = 0.8f;
        [SerializeField] private float spawnIntervalIncreaseRate = 10f;
        [SerializeField] private float difficultyIncreaseRate = 0.1f; // Giảm spawnInterval mỗi 10s
        [SerializeField] private Vector2 spawnYRange = new Vector2(-4f, 4f);
        [SerializeField] private Vector2 spawnXRange = new Vector2(0f, 12f); // Spawn ở mép phải

        [Header("Power-up")]
        [SerializeField] private float powerUpChance = 0.1f; // 10% kẻ thù mang power-up
        [SerializeField] private string powerUpWord = "emp"; // Từ đặc biệt cho power-up

        [ReadOnly] public float currentSpawnInterval;
        [ReadOnly] public float currentMaxEnemiesOnScene;
        
        private float _t;
        private List<string> _words;
        private System.Random _rng = new System.Random();
        private float _gameTime;
        
        internal static int EnemyCount;

        public System.Action<ZTypeEnemy> OnSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticVariables()
        {
            EnemyCount = 0;
        }
        public override void Initialize()
        {
            base.Initialize();
            _words = wordList ? wordList.GetWords(GetMinWordLength(), GetMaxWordLength()) : new List<string> { "test", "word", "alpha", "beta" };
            currentSpawnInterval = spawnInterval;
        }

        public override void Tick()
        {
            base.Tick();
            _gameTime += Time.deltaTime;
            _t += Time.deltaTime;

            // Tăng độ khó: giảm spawn interval mỗi spawnIntervalIncreaseRate
            currentSpawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - (_gameTime / spawnIntervalIncreaseRate) * difficultyIncreaseRate);
            currentMaxEnemiesOnScene = maxEnemiesOnScene + (int)(_gameTime / maxEnemiesIncreaseRate);

            if (_t >= currentSpawnInterval)
            {
                _t = 0f;
                SpawnOne();
            }
        }

        void SpawnOne()
        {
            if (!enemyPrefab || EnemyCount >= currentMaxEnemiesOnScene) return;
            
            bool isPowerUp = _rng.NextDouble() < powerUpChance;
            string word = isPowerUp ? powerUpWord : _words[_rng.Next(_words.Count)];
            
            var x = Mathf.Lerp(spawnXRange.x, spawnXRange.y, (float)_rng.NextDouble());
            var y = Mathf.Lerp(spawnYRange.x, spawnYRange.y, (float)_rng.NextDouble());
            var pos = new Vector3(x, y, 0f);
            
            //var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var e = enemyPrefab.GetObjectInPool<ZTypeEnemy>(pos, parent.transform);
            EnemyCount++;
            e.Init(word, isPowerUp);
            OnSpawned?.Invoke(e);
        }

        int GetMinWordLength() => Mathf.Min(3 + (int)(_gameTime / 30f), 5); // Tăng độ dài tối thiểu mỗi 30s
        int GetMaxWordLength() => Mathf.Min(5 + (int)(_gameTime / 20f), 10); // Tăng độ dài tối đa mỗi 20s
    }
}