using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace HHH.Common
{
    public class PopupManager : Singleton<PopupManager>
    {
        [SerializeField] private Dictionary<PopupName, string> scrDictionary;
        [SerializeField] private Transform defaultParent;
        [SerializeField] private Transform highParent;
        //[SerializeField] private TextNotice textNotice;
        //[SerializeField] private LoadingScene loadingScene;
        [SerializeField] private GameObject goldPanel;
        private Dictionary<PopupName, BasePopUp> _dictionScreen;
        public PopupName CurrentPopup;
        public PopupName HighPopup;
        private Tween _tweenActive, _tweenDeActive;

        public Transform DefaultParent
        {
            get => defaultParent;
        }

        public void Start()
        {
            _dictionScreen = new Dictionary<PopupName, BasePopUp>();
        }

        // public void ShowNotice(string s, NoticeColor color = NoticeColor.Red)
        // {
        //     textNotice.ShowNotice(s,color);
        // }

        // public void ShowLoadingScene(string mapName)
        // {
        //     ToggleGoldPanel(false);
        //     loadingScene.gameObject.SetActive(true);
        //     loadingScene.LoadSceneAsync(mapName,LoadingBgType.None);
        // }

        public GameObject GetPopup(PopupName popupName)
        {
            if (!_dictionScreen.ContainsKey(popupName))
                return null;
            GameObject obj = _dictionScreen[popupName].gameObject;
            return obj;
        }

        public void OnShowScreen(PopupName scr, ParentPopup parent = ParentPopup.Default)
        {
            this.OnCheckScreen(scr, parent);
            if (parent == ParentPopup.Default)
            {
                CurrentPopup = scr;
            }
            else
            {
                HighPopup = scr;
            }
            _dictionScreen[scr].OnShowScreen();
        }

        public void OnShowScreen(PopupName scr,object arg, ParentPopup parent = ParentPopup.Default)
        {
            this.OnCheckScreen(scr, parent);
            if (parent == ParentPopup.Default)
            {
                CurrentPopup = scr;
            }
            else
            {
                HighPopup = scr;
            }
            _dictionScreen[scr].OnShowScreen(arg);
        }

        public void OnCloseScreen(PopupName scr)
        {
            if (_dictionScreen.ContainsKey(scr) && !_dictionScreen[scr].IsClosing)
            {
                _dictionScreen[scr].OnCloseScreen();
            }

            if (CurrentPopup == scr)
            {
                CurrentPopup = PopupName.None;
            }else if (HighPopup == PopupName.None)
            {
                HighPopup = PopupName.None;
            }
        }

        public void OnDeActiveScreen(PopupName scr)
        {
            _dictionScreen[scr].OnDeActived();
        }

        public bool IsPopupShowing(PopupName scr)
        {
            if (_dictionScreen.ContainsKey(scr))
                return false;
            else
            {
                if (_dictionScreen[scr].IsShow || _dictionScreen[scr].IsShowing)
                    return true;
                else
                    return false;
            }
        }

        private void OnCheckScreen(PopupName scr, ParentPopup parent)
        {
            if (!_dictionScreen.ContainsKey(scr))
            {
                BasePopUp basePopup = this.OnCreateScreen(scr, parent);
                _dictionScreen.Add(scr, basePopup);
            }

            if (parent != ParentPopup.Hight && CurrentPopup != PopupName.None && CurrentPopup != scr)
            {
                if(_dictionScreen[CurrentPopup].IsShow || _dictionScreen[CurrentPopup].IsShowing)
                    OnCloseScreen(scr);
                CurrentPopup = PopupName.None;
            }
        }

        private BasePopUp OnCreateScreen(PopupName scr, ParentPopup parent = ParentPopup.Default)
        {
            GameObject prfScr = Resources.Load<GameObject>($"UI/{scrDictionary[scr]}");
            GameObject instance = Instantiate(prfScr, parent == ParentPopup.Default ? defaultParent : highParent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            BasePopUp basePopup = instance.GetComponent<BasePopUp>();
            return basePopup;
        }

        public void CheckResumeGame()
        {
            if (_dictionScreen == null)
            {
                /*if (HomeController.Instance != null)
                    HomeController.Instance.OnShowScreen();*/
                return;
            }

            foreach (KeyValuePair<PopupName, BasePopUp> popup in _dictionScreen)
            {
                if(popup.Value.IsShow || popup.Value.IsShowing)
                    return;
            }

            /*if (HomeController.Instance != null)
                HomeController.Instance.OnCloseScreen();*/
        }
        
        public void CloseCurrentPopup()
        {
            OnDeactiveAllScreen();
        }

        private void OnDeactiveAllScreen(PopupName scr = PopupName.None)
        {
            OnCloseScreen(HighPopup);
            OnCloseScreen(CurrentPopup);
            foreach (KeyValuePair<PopupName, BasePopUp> popup in _dictionScreen)
            {
                if (popup.Value.IsShow && scr != popup.Key && popup.Key != HighPopup && popup.Key != CurrentPopup)
                    popup.Value.OnCloseScreen();
            }
            CurrentPopup = PopupName.None;
            HighPopup = PopupName.None;
        }
        public void ToggleGoldPanel(bool isShow, bool forceDisable = false)
        {
            if (isShow)
            {
                _tweenDeActive?.Kill();
                goldPanel.SetActive(isShow);
                //_tweenActive = goldPanel.GetComponent<RectTransform>().DOAnchorPosY(-45f, 0.3f).SetUpdate(true);
            }
            else
            {
                if(CanHideGoldUI() || forceDisable)
                {
                    _tweenActive?.Kill();
                    goldPanel.SetActive(false);
                    // _tweenDeActive = goldPanel.GetComponent<RectTransform>().DOAnchorPosY(45f, 0.3f).SetUpdate(true).OnComplete(() =>
                    // {
                    //     goldPanel.SetActive(false);
                    // });
                }    
            }
        }

        private bool CanHideGoldUI()
        {
            if (DefaultParent.childCount == 0)
                return true;
            for(int i = DefaultParent.childCount - 1; i >=0 ; i--)
            {
                Transform child = DefaultParent.GetChild(i);
                if(child.gameObject.activeSelf)
                {
                    var popup = child.GetComponent<BasePopUp>();
                    if (popup != null && popup.EnableGoldUI)
                    {
                        return false;
                    }
                    else
                        return true;
                }    
            }
            return true;
        }    

        public bool GetGoldPanelStatus()
        {
            return goldPanel.activeSelf;
        }
        public void ToggleGoldBtnRaycast(bool enable)
        {
            //goldPanel.GetComponent<GoldGemPanelController>().ToggleGoldBtnRaycast(enable);
        }
    }
}