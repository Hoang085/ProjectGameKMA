using System.Collections.Generic;
using UnityEngine;
using HHH.Common;

namespace HHH.MiniGame
{
    public class EnemySpawner : BaseMono
    {
        [Header("Config")] 
        public GameObject parent;
        public WordListSO wordList;
        public GameObject enemyPrefab;
        public float spawnInterval = 1.6f;
        public float minSpawnInterval = 0.8f;
        public float difficultyIncreaseRate = 0.1f; // Giảm spawnInterval mỗi 10s
        public Vector2 spawnYRange = new Vector2(-4f, 4f);
        public float spawnX = 8f; // Spawn ở mép phải

        [Header("Power-up")]
        public float powerUpChance = 0.1f; // 10% kẻ thù mang power-up
        public string powerUpWord = "emp"; // Từ đặc biệt cho power-up

        float _t;
        float _currentSpawnInterval;
        List<string> _words;
        System.Random _rng = new System.Random();
        float _gameTime;

        public System.Action<ZTypeEnemy> OnSpawned;

        public override void Initialize()
        {
            base.Initialize();
            _words = wordList ? wordList.GetWords(GetMinWordLength(), GetMaxWordLength()) : new List<string> { "test", "word", "alpha", "beta" };
            _currentSpawnInterval = spawnInterval;
        }

        public override void Tick()
        {
            base.Tick();
            _gameTime += Time.deltaTime;
            _t += Time.deltaTime;

            // Tăng độ khó: giảm spawn interval mỗi 10s
            _currentSpawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - (_gameTime / 10f) * difficultyIncreaseRate);

            if (_t >= _currentSpawnInterval)
            {
                _t = 0f;
                SpawnOne();
            }
        }

        void SpawnOne()
        {
            if (!enemyPrefab) return;
            bool isPowerUp = _rng.NextDouble() < powerUpChance;
            string word = isPowerUp ? powerUpWord : _words[_rng.Next(_words.Count)];
            var y = Mathf.Lerp(spawnYRange.x, spawnYRange.y, (float)_rng.NextDouble());
            var pos = new Vector3(spawnX, y, 0f);
            //var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var e = enemyPrefab.GetObjectInPool<ZTypeEnemy>(pos, parent.transform);
            e.Init(word, isPowerUp);
            OnSpawned?.Invoke(e);
        }

        int GetMinWordLength() => Mathf.Min(3 + (int)(_gameTime / 30f), 5); // Tăng độ dài tối thiểu mỗi 30s
        int GetMaxWordLength() => Mathf.Min(5 + (int)(_gameTime / 20f), 10); // Tăng độ dài tối đa mỗi 20s
    }
}