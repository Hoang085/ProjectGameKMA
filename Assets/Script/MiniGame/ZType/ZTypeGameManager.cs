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
        //public HudController hud;

        [Header("Rules")] public int lives = 3;
        public int scorePerWord = 100;
        public int scorePerLetter = 5;

        readonly List<ZTypeEnemy> _enemies = new List<ZTypeEnemy>();
        ZTypeEnemy _active;

        void OnEnable()
        {
            if (spawner) spawner.OnSpawned += RegisterEnemy;
        }

        void OnDisable()
        {
            if (spawner) spawner.OnSpawned -= RegisterEnemy;
        }

        void Update()
        {
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
            //hud?.AddScore(scorePerWord);
            RemoveEnemy(e, destroyed: true);
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
            // Lấy tất cả ký tự vừa gõ frame này
            var input = Input.inputString;
            if (string.IsNullOrEmpty(input)) return;

            foreach (char raw in input)
            {
                char c = raw;
                if (char.IsUpper(c)) c = char.ToLower(c);
                if (!IsAsciiLetter(c)) continue; // bỏ qua phím không phải chữ

                // Nếu chưa khóa mục tiêu → tìm enemy có từ bắt đầu bằng ký tự này (ưu tiên gần đáy)
                if (_active == null)
                {
                    var candidate = _enemies
                        .Where(en => en && en.Word.Length > 0 && en.Word[0] == c)
                        .OrderBy(en => en.transform.position.y) // y nhỏ hơn = gần đáy hơn
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        _active = candidate;
                        _active.SetAsActiveTarget(true);
                    }
                    else
                    {
                        // Không có từ nào bắt đầu bằng c → có thể thử "tự do": chọn kẻ địch chứa c ở vị trí hiện tại?
                        // Để đơn giản: bỏ qua.
                        continue;
                    }
                }

                // Có mục tiêu → gõ ký tự
                if (_active != null)
                {
                    bool ok = _active.TryTypeChar(c);
                    //if (ok) hud?.AddScore(scorePerLetter);

                    // Nếu enemy đã bị OnWordCompleted, _active sẽ được reset trong RemoveEnemy
                    if (_active && _active.TypedIndex >= _active.Word.Length)
                    {
                        // đảm bảo dọn dẹp (an toàn)
                        _active = null;
                    }
                }
            }
        }

        bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        void GameOver()
        {
            // Dừng spawn & đóng băng game đơn giản
            if (spawner) spawner.enabled = false;
            foreach (var e in _enemies.ToArray()) RemoveEnemy(e, destroyed: true);
            //hud?.ShowGameOver();
            enabled = false;
        }
    }
}