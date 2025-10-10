using UnityEngine;
using TMPro;
using System;
using HHH.Common;

namespace HHH.MiniGame
{
    public class ZTypeEnemy : BaseMono
    {
        [Header("Refs")] 
        [SerializeField] private TextMeshProUGUI wordLabel;
        [SerializeField] private SpriteRenderer body;

        [Header("Move")] 
        [SerializeField] private float speed = 2f;
        [SerializeField] private Vector2 direction = Vector2.left; // Di chuyển sang trái

        [Header("Runtime")]
        public string Word { get; private set; }
        public int TypedIndex { get; private set; } = 0;
        public bool IsActiveTarget { get; set; } = false;
        public bool IsPowerUp { get; private set; } = false;

        public event Action<ZTypeEnemy> OnReachedBottom;
        public event Action<ZTypeEnemy> OnWordCompleted;

        Color _baseColor;
        float _shakeTime;

        void Awake()
        {
            if (!body) body = GetComponentInChildren<SpriteRenderer>();
            if (!wordLabel) wordLabel = GetComponentInChildren<TextMeshProUGUI>();
            _baseColor = body ? body.color : Color.white;
        }

        public void Init(string word, bool isPowerUp = false)
        {
            Word = word.ToLower();
            IsPowerUp = isPowerUp;
            TypedIndex = 0;
            UpdateWordLabel();
            SetActiveVisual(false);
        }

        public override void FixedTick()
        {
            base.FixedTick();
            transform.Translate((Vector3)direction * speed * Time.deltaTime);

            if (transform.position.y < -12f) //vị trí tàu người chơi
            {
                OnReachedBottom?.Invoke(this);
            }
            
            if (body)
                body.color = IsActiveTarget ? Color.Lerp(_baseColor, Color.white, 0.6f) : _baseColor;
        }

        public bool TryTypeChar(char c)
        {
            if (TypedIndex >= Word.Length) return false;
            c = char.ToLower(c);
            if (Word[TypedIndex] == c)
            {
                TypedIndex++;
                UpdateWordLabel();
                if (TypedIndex >= Word.Length)
                    OnWordCompleted?.Invoke(this);
                return true;
            }
            else
            {
                _shakeTime = 0.2f; // Rung 0.2s khi gõ sai
                if (body) body.color = Color.red; // Đổi màu đỏ tạm thời
                return false;
            }
        }

        public override void LateTick()
        {
            if (wordLabel)
                wordLabel.transform.localScale = Vector3.Lerp(wordLabel.transform.localScale, Vector3.one, 10f * Time.deltaTime);
            
            if (_shakeTime > 0)
            {
                _shakeTime -= Time.deltaTime;
                if (wordLabel)
                    wordLabel.transform.localPosition = new Vector3(
                        Mathf.Sin(Time.time * 50f) * 0.1f, // Rung mạnh hơn
                        wordLabel.transform.localPosition.y,
                        wordLabel.transform.localPosition.z
                    );
                if (_shakeTime <= 0 && body)
                    body.color = IsActiveTarget ? Color.Lerp(_baseColor, Color.white, 0.6f) : _baseColor;
            }
        }

        void UpdateWordLabel()
        {
            if (!wordLabel) return;
            var head = Word.Substring(0, TypedIndex);
            var tail = Word.Substring(TypedIndex);
            wordLabel.text = IsPowerUp
                ? $"<color=#FFAA00>{head}</color><color=#FFFF00>{tail}</color>" // Màu vàng cho power-up
                : $"<color=#6DF77A>{head}</color><color=#FFFFFF>{tail}</color>";
        }

        void SetActiveVisual(bool active)
        {
            IsActiveTarget = active;
            if (wordLabel)
                wordLabel.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            if (body)
                body.sortingOrder = active ? 10 : 0;
        }

        public void SetAsActiveTarget(bool value) => SetActiveVisual(value);
    }
}