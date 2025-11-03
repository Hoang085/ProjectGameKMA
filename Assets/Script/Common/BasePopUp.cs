using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace HHH.Common
{
    public class BasePopUp : MonoBehaviour
    {
        [SerializeField] private List<Button> listButtonControl;
        [SerializeField] private bool enableGoldUI;
        [SerializeField] private PopupShowingType showingType = PopupShowingType.FadeInOut;
        [ShowIf("showingType", PopupShowingType.Slide), SerializeField] private Vector2 targetPos;
        [SerializeField] private GameObject blockRaycast;
        
        public PopupName currentScreen;
        private CanvasGroup _canvasGroup;
        private Tween _tween,_tween2;
        protected bool OnClick;
        public bool CanClick => !OnClick;
        public bool IsShow => _canvasGroup.alpha == 1 && IsClosing == false;
        public bool IsShowing { get; private set; }
        public bool IsClosing { get; private set; } 
        public bool EnableGoldUI => enableGoldUI;
        
        protected void Awake()
        {
            this.OnInitScreen();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        public virtual void OnInitScreen(){}

        public virtual void OnShowScreen()
        {
            if(blockRaycast != null)
                blockRaycast.SetActive(false);
            BlockMultyClick();
            
            // Notify GameUIManager that a popup is opening
            NotifyUIStateChanged(true);
            
            if (showingType == PopupShowingType.FadeInOut)
                OnFadeIn();
            else if (showingType == PopupShowingType.Slide)
                OnSlideUp();
            else
                ShowFullScreen();
        }

        public virtual void OnShowScreen(object arg)
        {
            BlockMultyClick();
            if(blockRaycast!=null)
                blockRaycast.SetActive(false);
            
            // Notify GameUIManager that a popup is opening
            NotifyUIStateChanged(true);
                
            if (showingType == PopupShowingType.FadeInOut)
                OnFadeIn();
            else if (showingType == PopupShowingType.Slide)
                OnSlideUp();
            else
                ShowFullScreen();
        }

        public virtual void OnShowScreen(object[] args)
        {
            BlockMultyClick();
            if(blockRaycast!=null)
                blockRaycast.SetActive(false);
            
            // Notify GameUIManager that a popup is opening
            NotifyUIStateChanged(true);
                
            if (showingType == PopupShowingType.FadeInOut)
                OnFadeIn();
            else if(showingType == PopupShowingType.Slide)
                OnSlideUp();
            else
                ShowFullScreen();
        }

        public virtual void OnCloseScreen()
        {
            if (showingType == PopupShowingType.FadeInOut)
                OnFadeOut();
            else if (showingType == PopupShowingType.Slide)
                OnSlideDown();
            else
                HideScreen();
            BlockMultyClick();
            
            // Notify GameUIManager that a popup is closing
            // Delay slightly to ensure animation completes
            DOVirtual.DelayedCall(0.3f, () => NotifyUIStateChanged(false)).SetUpdate(true);
        }

        public void OnDeActived()
        {
            this.transform.localScale = Vector3.zero;
            _canvasGroup.alpha = 0f;
        }

        private void ShowFullScreen()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _canvasGroup.alpha = 1;
            IsShowing = false;
            transform.localScale = Vector3.zero;
            if (enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(true);
        }

        private void HideScreen()
        {
            IsClosing = false;
            transform.localScale = new Vector3(0, 1, 1);
            gameObject.SetActive(false);
            _canvasGroup.alpha = 0;
            DOVirtual.DelayedCall(0.3f, () => { PopupManager.Ins.CheckResumeGame(); }).SetUpdate(true);
            IsClosing = false;
            if(enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(false);
        }

        private void OnFadeIn()
        {
            if(IsShowing)
                return;
            IsShowing = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _canvasGroup.alpha = 0.5f;
            transform.localScale = Vector3.one * 0.8f;
            _tween?.Kill();
            _tween2?.Kill();
            _tween = _canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                _canvasGroup.alpha = 1;
                IsShowing = false;
            });
            _tween2 = transform.DOScale(1, 0.2f).SetUpdate(true);
            if(enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(true);
        }

        private void OnFadeOut()
        {
            IsClosing = true;
            IsShowing = false;
            _tween?.Kill();
            _tween2?.Kill();
            _canvasGroup.alpha = 0.5f;
            _tween2 = transform.DOScale(1.15f, 0.15f).SetUpdate(true);
            _tween = _canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() =>
            {
                this.transform.localScale = new Vector3(0, 1, 1);
                gameObject.SetActive(false);
                _canvasGroup.alpha = 0;
                PopupManager.Ins.CheckResumeGame();
                IsClosing = false;
            });
            if(enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(false);
        }

        private void OnSlideUp()
        {
            if (IsShowing) return;

            IsShowing = true;
            Vector3 startPos = new Vector3(transform.localPosition.x, -1500f, transform.localPosition.z);
            transform.localPosition = startPos;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            Vector3 endPos = new Vector3(targetPos.x, targetPos.y, startPos.z);

            _canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
            _tween?.Kill();
            _tween2?.Kill();

            _tween = transform.DOLocalMove(endPos, 0.75f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    IsShowing = false;
                    transform.localPosition = endPos;
                });

            if (enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(true);
        }

        private void OnSlideDown()
        {
            if (IsClosing) return;

            IsClosing = true;
            IsShowing = false;
            _tween?.Kill();
            _tween2?.Kill();

            Vector3 currentPos = transform.localPosition;
            Vector3 endPos = new Vector3(0f, -1500f, currentPos.z);

            _tween = transform.DOLocalMove(endPos, 0.75f)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    transform.localPosition = endPos;
                    IsClosing = false;
                });

            if (enableGoldUI)
                PopupManager.Ins.ToggleGoldPanel(false);
        }


        public void BlockMultyClick()
        {
            OnClick = true;
            DOVirtual.DelayedCall(0.2f, () => OnClick = false);
            for (int i = 0; i < listButtonControl.Count; i++)
            {
                listButtonControl[i].interactable = false;
            }

            DOVirtual.DelayedCall(0.2f, () =>
            {
                for (int i = 0; i < listButtonControl.Count; i++)
                {
                    listButtonControl[i].interactable = true;
                }
            });
        }

        public void SetInteractableControlButton(bool value)
        {
            foreach (var button in listButtonControl)
            {
                button.interactable = value;
            }
        }

        public void BlockRayCast(bool isActive)
        {
            if (blockRaycast != null)
                blockRaycast.SetActive(isActive);
        }

        public void BlockRayCast(float timeBlock)
        {
            if (blockRaycast == null)
                return;
            blockRaycast.SetActive(true);
            DOVirtual.DelayedCall(timeBlock, () =>
            {
                blockRaycast.SetActive(false);
            }).SetUpdate(true);
        }

        /// <summary>
        /// Notify GameUIManager about popup state changes for player/camera control
        /// </summary>
        private void NotifyUIStateChanged(bool isOpening)
        {
            if (GameUIManager.Ins != null)
            {
                if (isOpening)
                {
                    GameUIManager.Ins.OnPopupOpened();
                }
                else
                {
                    GameUIManager.Ins.OnPopupClosed();
                }
            }
        }
    }
}