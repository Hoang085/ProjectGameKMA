// Assets/Scripts/EnemyController.cs

using UnityEngine;
using TMPro;
using System;
using HHH.Common;

namespace HHH.MiniGame
{
    public class ZTypeEnemy : BaseMono
    {
        [Header("Refs")] 
        [SerializeField] private TextMeshPro wordLabel;
        [SerializeField] private SpriteRenderer body;

        [Header("Move")] 
        [SerializeField] private float speed = 1.2f;
        [SerializeField] private Vector2 direction = Vector2.down;

        [Header("Runtime")]
        public string Word { get; private set; }
        public int TypedIndex { get; private set; } = 0;
        public bool IsActiveTarget { get; set; } = false;

        public event Action<ZTypeEnemy> OnReachedBottom;
        public event Action<ZTypeEnemy> OnWordCompleted;

        Color _baseColor;

        void Awake()
        {
            if (!body) body = GetComponentInChildren<SpriteRenderer>();
            if (!wordLabel) wordLabel = GetComponentInChildren<TextMeshPro>();
            _baseColor = body ? body.color : Color.white;
        }

        public void Init(string word)
        {
            Word = word.ToLower();
            TypedIndex = 0;
            UpdateWordLabel();
            SetActiveVisual(false);
        }

        public override void FixedTick()
        {
            base.FixedTick();
            transform.Translate((Vector3)direction * speed * Time.deltaTime);
            
            if (transform.position.y < -5f)
                OnReachedBottom?.Invoke(this);
            
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
                // feedback sai ký tự (rung nhẹ)
                if (wordLabel) wordLabel.transform.localScale = Vector3.one * 1.08f;
                return false;
            }
        }

        public override void LateTick()
        {
            if (wordLabel)
                wordLabel.transform.localScale =
                    Vector3.Lerp(wordLabel.transform.localScale, Vector3.one, 10f * Time.deltaTime);
        }

        void UpdateWordLabel()
        {
            if (!wordLabel) return;
            // ví dụ: đúng được 3 ký tự -> <mark>cod</mark>e
            var head = Word.Substring(0, TypedIndex);
            var tail = Word.Substring(TypedIndex);
            wordLabel.text = $"<color=#6DF77A>{head}</color><color=#FFFFFF>{tail}</color>";
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