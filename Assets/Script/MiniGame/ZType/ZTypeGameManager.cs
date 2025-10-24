using System.Collections.Generic;
using System.Linq;
using HHH.Common;
using UnityEngine;

namespace HHH.MiniGame
{
    /// <summary>
    /// Minigame Z-Type.
    /// - Kế thừa MiniGameBase (đúng chữ ký).
    /// - Implement StartGame().
    /// - Reset runtime trong Start().
    /// - GameOver() gọi Finish(default) để không phụ thuộc field của MiniGameResult.
    /// </summary>
    public class ZTypeGameManager : MiniGameBase
    {
        [Header("Refs")]
        public EnemySpawner spawner;
        public GameObject hud;          // Panel "GAME OVER"
        public Animator playerAnimator; // Animator nhân vật (trigger "Type")

        [Header("Rules")]
        public int lives = 3;
        public int scorePerWord = 100;
        public int scorePerLetter = 5;
        public int scorePerPowerUp = 500;

        // Runtime
        private readonly List<ZTypeEnemy> _enemies = new List<ZTypeEnemy>();
        private ZTypeEnemy _active;

        // ===== Lifecycle =====

        // ĐÚNG chữ ký của MiniGameBase
        public override void Initialize(MiniGameContext ctx)
        {
            base.Initialize(ctx);
            if (hud) hud.SetActive(false);
        }

        // Đáp ứng abstract bắt buộc
        public override void StartGame()
        {
            // Khi bắt đầu game thật sự (từ loader), đảm bảo trạng thái sạch
            if (hud) hud.SetActive(false);
            lives = Mathf.Max(lives, 1);
            EnemySpawner.EnemyCount = 0;
            _enemies.Clear();
            _active = null;
            if (spawner) spawner.enabled = true;
        }

        private void Start()
        {
            // Lưới an toàn khi vào scene trực tiếp (không qua loader)
            if (hud) hud.SetActive(false);

            if (lives <= 0)
            {
                lives = 3;
                Debug.Log("[ZTypeGameManager] Reset lives về mặc định = 3");
            }

            EnemySpawner.EnemyCount = 0;
            _enemies.Clear();
            _active = null;

            foreach (var e in FindObjectsOfType<ZTypeEnemy>())
                Destroy(e.gameObject);
        }

        private void OnEnable()
        {
            if (spawner) spawner.OnSpawned += RegisterEnemy;
        }

        private void OnDisable()
        {
            if (spawner) spawner.OnSpawned -= RegisterEnemy;
        }

        public override void Tick()
        {
            base.Tick();
            HandleKeyboardInput();
        }

        // ===== Gameplay =====

        private void RegisterEnemy(ZTypeEnemy e)
        {
            if (!e) return;
            _enemies.Add(e);
            e.OnWordCompleted += OnEnemyKilled;
            e.OnReachedBottom += OnEnemyReachedBottom;
        }

        private void OnEnemyKilled(ZTypeEnemy e)
        {
            if (!e) return;

            if (e.IsPowerUp)
            {
                foreach (var enemy in _enemies.ToArray())
                    RemoveEnemy(enemy, destroyed: true);

                EnemySpawner.EnemyCount = 0;
            }
            else
            {
                RemoveEnemy(e, destroyed: true);
                EnemySpawner.EnemyCount = Mathf.Max(0, EnemySpawner.EnemyCount - 1);
            }
        }

        private void OnEnemyReachedBottom(ZTypeEnemy e)
        {
            if (lives <= 0) { RemoveEnemy(e, destroyed: true); return; }

            lives = Mathf.Max(0, lives - 1);
            RemoveEnemy(e, destroyed: true);

            if (lives <= 0)
                GameOver();
        }

        private void RemoveEnemy(ZTypeEnemy e, bool destroyed)
        {
            if (!e) return;

            if (_active == e)
            {
                _active.SetAsActiveTarget(false);
                _active = null;
            }

            _enemies.Remove(e);
            if (destroyed && e) Destroy(e.gameObject);
        }

        private void HandleKeyboardInput()
        {
            var input = Input.inputString;
            if (string.IsNullOrEmpty(input)) return;

            if (playerAnimator) playerAnimator.SetTrigger("Type");

            foreach (char c in input)
            {
                if (!IsAsciiLetter(c)) continue;

                if (_active == null)
                {
                    var candidate = _enemies
                        .Where(en => en && en.Word.Length > en.TypedIndex && en.Word[en.TypedIndex] == c)
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        _active = candidate;
                        _active.SetAsActiveTarget(true);
                    }
                    else continue;
                }

                if (_active != null)
                {
                    bool ok = _active.TryTypeChar(c);
                    if (_active && _active.TypedIndex >= _active.Word.Length)
                        _active = null;
                }
            }
        }

        private bool IsAsciiLetter(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private void GameOver()
        {
            if (spawner) spawner.enabled = false;

            foreach (var e in _enemies.ToArray())
                RemoveEnemy(e, destroyed: true);

            if (hud) hud.SetActive(true);

            Debug.Log("[ZTypeGameManager] GameOver - returning to GameScene...");

            // KHÔNG dùng field của MiniGameResult → truyền default
            Finish(default);

            enabled = false;
        }
    }
}
