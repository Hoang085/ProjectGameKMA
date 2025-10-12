using System.Collections.Generic;
using System.Linq;
using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    public class ZTypeGameManager : BaseMono
    {
        [Header("Refs")] 
        public EnemySpawner spawner;
        public GameObject hud;

        [Header("Rules")] 
        public int lives = 3;
        public int scorePerWord = 100;
        public int scorePerLetter = 5;
        public int scorePerPowerUp = 500;

        readonly List<ZTypeEnemy> _enemies = new List<ZTypeEnemy>();
        ZTypeEnemy _active;

        public override void Initialize()
        {
            base.Initialize();
            
            hud.SetActive(false);
        }

        void OnEnable()
        {
            if (spawner) spawner.OnSpawned += RegisterEnemy;
        }

        void OnDisable()
        {
            if (spawner) spawner.OnSpawned -= RegisterEnemy;
        }

        public override void Tick()
        {
            base.Tick();
            HandleKeyboardInput();
        }

        void RegisterEnemy(ZTypeEnemy e)
        {
            _enemies.Add(e);
            e.OnWordCompleted += OnEnemyKilled;
            e.OnReachedBottom += OnEnemyReachedBottom;
        }

        void OnEnemyKilled(ZTypeEnemy e)
        {
            //hud?.AddScore(e.IsPowerUp ? scorePerPowerUp : scorePerWord);
            if (e.IsPowerUp)
            {
                // Kích hoạt EMP: tiêu diệt tất cả kẻ thù
                foreach (var enemy in _enemies.ToArray())
                    RemoveEnemy(enemy, destroyed: true);
                
                EnemySpawner.EnemyCount = 0;
            }
            else
            {
                RemoveEnemy(e, destroyed: true);
                EnemySpawner.EnemyCount--;
            }
        }

        void OnEnemyReachedBottom(ZTypeEnemy e)
        {
            lives--;
            //hud?.SetLives(lives);
            RemoveEnemy(e, destroyed: true);
            if (lives <= 0) GameOver();
        }

        void RemoveEnemy(ZTypeEnemy e, bool destroyed)
        {
            if (_active == e)
            {
                _active.SetAsActiveTarget(false);
                _active = null;
            }

            _enemies.Remove(e);
            if (destroyed && e) Destroy(e.gameObject);
        }

        void HandleKeyboardInput()
        {
            var input = Input.inputString;
            if (string.IsNullOrEmpty(input)) return;
            
            foreach (char c in input)
            {
                if (!IsAsciiLetter(c)) continue;

                if (_active == null)
                {
                    // Tìm enemy có từ bắt đầu bằng c, ưu tiên gần mép trái
                    var candidate = _enemies
                        .Where(en => en && en.Word.Length > en.TypedIndex && en.Word[en.TypedIndex] == c)
                        .OrderBy(en => en.transform.position.x) // x nhỏ hơn = gần mép trái
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        _active = candidate;
                        _active.SetAsActiveTarget(true);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (_active != null)
                {
                    bool ok = _active.TryTypeChar(c);
                    //if (ok) hud?.AddScore(scorePerLetter);

                    if (_active && _active.TypedIndex >= _active.Word.Length)
                    {
                        _active = null; // Reset sau khi hoàn thành từ
                    }
                }
            }
        }

        bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        void GameOver()
        {
            if (spawner) spawner.enabled = false;
            foreach (var e in _enemies.ToArray()) RemoveEnemy(e, destroyed: true);
            hud.SetActive(true);
            enabled = false;
        }
    }
}