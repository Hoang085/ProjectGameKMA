using System.Collections.Generic;
using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    public class EnemySpawner : BaseMono
    {
        [Header("Config")]
        public WordListSO wordList;
        public ZTypeEnemy enemyPrefab;
        public float spawnInterval = 1.6f;
        public Vector2 spawnXRange = new Vector2(-7.5f, 7.5f);
        public float spawnY = 5.5f;

        float _t;
        List<string> _words;
        System.Random _rng = new System.Random();

        public System.Action<ZTypeEnemy> OnSpawned;

        public override void Initialize()
        {
            base.Initialize();
            _words = wordList ? wordList.GetWords() : new List<string>{"test","word","alpha","beta"};
        }

        public override void Tick()
        {
            base.Tick();
            
            _t += Time.deltaTime;
            if (_t >= spawnInterval)
            {
                _t = 0f;
                SpawnOne();
            }
        }

        void SpawnOne()
        {
            if (!enemyPrefab) return;
            string word = _words[_rng.Next(_words.Count)];
            var x = Mathf.Lerp(spawnXRange.x, spawnXRange.y, (float)_rng.NextDouble());
            var pos = new Vector3(x, spawnY, 0f);
            var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            e.Init(word);
            OnSpawned?.Invoke(e);
        }
    }
}