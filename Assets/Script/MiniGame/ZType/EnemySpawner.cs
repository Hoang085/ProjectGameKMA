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

        private int _currentMinWordLength;
        private int _currentMaxWordLength;

        private List<float> _recentSpawnX = new List<float>();
        private const float MinXPadding = 1f; // padding nhỏ giữa các enemy
        private const int MaxSpawnTries = 10;
        private float _enemyWidth = 1.0f; // default, sẽ lấy từ prefab

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticVariables()
        {
            EnemyCount = 0;
        }
        public override void Initialize()
        {
            base.Initialize();
            _currentMinWordLength = GetMinWordLength();
            _currentMaxWordLength = GetMaxWordLength();
            _words = wordList ? wordList.GetWords(_currentMinWordLength, _currentMaxWordLength) : new List<string> { "test", "word", "alpha", "beta" };
            currentSpawnInterval = spawnInterval;
            // Lấy kích thước thực tế của enemy (nếu có SpriteRenderer)
            if (enemyPrefab)
            {
                var sr = enemyPrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr)
                    _enemyWidth = sr.bounds.size.x;
            }
        }

        public override void Tick()
        {
            base.Tick();
            _gameTime += Time.deltaTime;
            _t += Time.deltaTime;

            // Tăng độ khó: giảm spawn interval mỗi spawnIntervalIncreaseRate
            currentSpawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - (_gameTime / spawnIntervalIncreaseRate) * difficultyIncreaseRate);
            currentMaxEnemiesOnScene = maxEnemiesOnScene + (int)(_gameTime / maxEnemiesIncreaseRate);

            // Cập nhật độ dài từ nếu thay đổi
            int minLen = GetMinWordLength();
            int maxLen = GetMaxWordLength();
            if (minLen != _currentMinWordLength || maxLen != _currentMaxWordLength)
            {
                _currentMinWordLength = minLen;
                _currentMaxWordLength = maxLen;
                if (wordList)
                    _words = wordList.GetWords(_currentMinWordLength, _currentMaxWordLength);
            }

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

            float x = 0f, y = 0f;
            int tries = 0;
            bool found = false;
            float minDistance = _enemyWidth + MinXPadding;
            while (tries < MaxSpawnTries && !found)
            {
                x = Mathf.Lerp(spawnXRange.x, spawnXRange.y, (float)_rng.NextDouble());
                found = true;
                foreach (var recentX in _recentSpawnX)
                {
                    if (Mathf.Abs(x - recentX) < minDistance)
                    {
                        found = false;
                        break;
                    }
                }
                tries++;
            }
            y = Mathf.Lerp(spawnYRange.x, spawnYRange.y, (float)_rng.NextDouble());
            var pos = new Vector3(x, y, 0f);

            var e = enemyPrefab.GetObjectInPool<ZTypeEnemy>(pos, parent.transform);
            EnemyCount++;
            e.Init(word, isPowerUp);
            OnSpawned?.Invoke(e);

            // Lưu lại vị trí X vừa spawn, chỉ giữ số lượng bằng số enemy tối đa trên scene
            _recentSpawnX.Add(x);
            if (_recentSpawnX.Count > currentMaxEnemiesOnScene) _recentSpawnX.RemoveAt(0);
        }

        int GetMinWordLength() => Mathf.Min(3 + (int)(_gameTime / 30f), 5); // Tăng độ dài tối thiểu mỗi 30s
        int GetMaxWordLength() => Mathf.Min(5 + (int)(_gameTime / 20f), 10); // Tăng độ dài tối đa mỗi 20s
    }
}