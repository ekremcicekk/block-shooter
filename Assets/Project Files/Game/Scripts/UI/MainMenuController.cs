using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace BlockShooter
{
    public enum MenuButtonType { Shop, Home, Achievement }

    [System.Serializable]
    public class MenuButtonData
    {
        public Button button;
        public RectTransform icon;
        public RectTransform textRect;
    }

    public class MainMenuController : MonoBehaviour
    {
        [Header("MenuBar Navigation")]
        public RectTransform selectedImg;
        public MenuButtonData shopBtnData;
        public MenuButtonData homeBtnData;
        public MenuButtonData arcBtnData;

        [Header("Sliding Panels")]
        [Tooltip("The Store page panel (slides in from left to center)")]
        public RectTransform storePage;
        [Tooltip("The Home page/level path panel (slides out from center to right)")]
        public RectTransform homePage;
        [Tooltip("The screen/canvas width used as the offset for sliding panels")]
        public float pageWidth = 1080f;

        private MenuButtonType _currentTab = MenuButtonType.Home;
        
        // Storing original icon Y positions for resetting
        private float _shopIconOriginalY;
        private float _homeIconOriginalY;

        private void Start()
        {
            // Store original icon positions
            if (shopBtnData != null && shopBtnData.icon != null)
                _shopIconOriginalY = shopBtnData.icon.anchoredPosition.y;
            if (homeBtnData != null && homeBtnData.icon != null)
                _homeIconOriginalY = homeBtnData.icon.anchoredPosition.y;

            // MenuBar buttons configuration
            if (shopBtnData != null && shopBtnData.button != null)
                shopBtnData.button.onClick.AddListener(() => SelectTab(MenuButtonType.Shop));

            if (homeBtnData != null && homeBtnData.button != null)
                homeBtnData.button.onClick.AddListener(() => SelectTab(MenuButtonType.Home));

            if (arcBtnData != null && arcBtnData.button != null)
                arcBtnData.button.onClick.AddListener(() => SelectTab(MenuButtonType.Achievement));

            // Setup initial position of panels without animation
            SelectTab(MenuButtonType.Home, animate: false);
        }

        public void SelectTab(MenuButtonType tabType, bool animate = true)
        {
            if (tabType == MenuButtonType.Achievement)
            {
                // Achievement button is a placeholder, do nothing as requested.
                return;
            }

            _currentTab = tabType;

            MenuButtonData activeData = (tabType == MenuButtonType.Shop) ? shopBtnData : homeBtnData;
            MenuButtonData inactiveData = (tabType == MenuButtonType.Shop) ? homeBtnData : shopBtnData;
            float activeOriginalIconY = (tabType == MenuButtonType.Shop) ? _shopIconOriginalY : _homeIconOriginalY;
            float inactiveOriginalIconY = (tabType == MenuButtonType.Shop) ? _homeIconOriginalY : _shopIconOriginalY;

            // 1. Move Selected_IMG indicator to the selected button horizontally
            if (selectedImg != null && activeData.button != null)
            {
                float targetX = activeData.button.GetComponent<RectTransform>().anchoredPosition.x;
                if (animate)
                {
                    selectedImg.DOKill();
                    selectedImg.DOAnchorPosX(targetX, 0.25f).SetEase(Ease.OutQuad);
                }
                else
                {
                    selectedImg.anchoredPosition = new Vector2(targetX, selectedImg.anchoredPosition.y);
                }
            }

            // 2. Animate Active Button
            if (activeData != null)
            {
                // Scale Menu_Icon up to 1.2 and move Y position to 40
                if (activeData.icon != null)
                {
                    activeData.icon.DOKill();
                    if (animate)
                    {
                        activeData.icon.DOScale(1.2f, 0.25f).SetEase(Ease.OutBack);
                        activeData.icon.DOAnchorPosY(40f, 0.25f).SetEase(Ease.OutQuad);
                    }
                    else
                    {
                        activeData.icon.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                        activeData.icon.anchoredPosition = new Vector2(activeData.icon.anchoredPosition.x, 40f);
                    }
                }

                // Show active text and animate Y up to -73
                if (activeData.textRect != null)
                {
                    activeData.textRect.gameObject.SetActive(true);
                    activeData.textRect.DOKill();
                    if (animate)
                    {
                        activeData.textRect.anchoredPosition = new Vector2(activeData.textRect.anchoredPosition.x, -120f);
                        activeData.textRect.DOAnchorPosY(-73f, 0.25f).SetEase(Ease.OutQuad);
                    }
                    else
                    {
                        activeData.textRect.anchoredPosition = new Vector2(activeData.textRect.anchoredPosition.x, -73f);
                    }
                }
            }

            // 3. Reset Inactive Button
            if (inactiveData != null)
            {
                // Scale Menu_Icon back to 1.0 and restore original Y position
                if (inactiveData.icon != null)
                {
                    inactiveData.icon.DOKill();
                    if (animate)
                    {
                        inactiveData.icon.DOScale(1.0f, 0.25f).SetEase(Ease.OutQuad);
                        inactiveData.icon.DOAnchorPosY(inactiveOriginalIconY, 0.25f).SetEase(Ease.OutQuad);
                    }
                    else
                    {
                        inactiveData.icon.localScale = Vector3.one;
                        inactiveData.icon.anchoredPosition = new Vector2(inactiveData.icon.anchoredPosition.x, inactiveOriginalIconY);
                    }
                }

                // Hide inactive text
                if (inactiveData.textRect != null)
                {
                    inactiveData.textRect.gameObject.SetActive(false);
                }
            }

            // 4. Slide transitions between Shop (Store) and Home page
            if (storePage != null && homePage != null)
            {
                storePage.DOKill();
                homePage.DOKill();

                // Dynamically calculate the sliding width based on the actual RectTransform width of this container or its parent
                float dynamicWidth = pageWidth;
                RectTransform myRect = transform as RectTransform;
                if (myRect != null && myRect.rect.width > 0)
                {
                    dynamicWidth = myRect.rect.width;
                }
                else if (transform.parent != null)
                {
                    RectTransform parentRect = transform.parent as RectTransform;
                    if (parentRect != null && parentRect.rect.width > 0)
                    {
                        dynamicWidth = parentRect.rect.width;
                    }
                }

                if (tabType == MenuButtonType.Shop)
                {
                    // Shop selected:
                    // - Home page slides out to the right (X: dynamicWidth)
                    // - Store page slides in from the left (X: 0)
                    storePage.gameObject.SetActive(true);
                    
                    if (animate)
                    {
                        homePage.DOAnchorPosX(dynamicWidth, 0.35f).SetEase(Ease.OutCubic);
                        storePage.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutCubic);
                    }
                    else
                    {
                        homePage.anchoredPosition = new Vector2(dynamicWidth, homePage.anchoredPosition.y);
                        storePage.anchoredPosition = new Vector2(0f, storePage.anchoredPosition.y);
                    }
                }
                else if (tabType == MenuButtonType.Home)
                {
                    // Home selected:
                    // - Home page slides back in to center (X: 0)
                    // - Store page slides back out to the left (X: -dynamicWidth)
                    homePage.gameObject.SetActive(true);

                    if (animate)
                    {
                        homePage.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutCubic);
                        storePage.DOAnchorPosX(-dynamicWidth, 0.35f).SetEase(Ease.OutCubic)
                            .OnComplete(() => {
                                if (_currentTab == MenuButtonType.Home)
                                    storePage.gameObject.SetActive(false);
                            });
                    }
                    else
                    {
                        homePage.anchoredPosition = new Vector2(0f, homePage.anchoredPosition.y);
                        storePage.anchoredPosition = new Vector2(-dynamicWidth, storePage.anchoredPosition.y);
                        storePage.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Public method that can be bound directly to the PLAY button onClick event in the inspector.
        /// </summary>
        public void StartGame()
        {
            DOTween.KillAll();
            SceneManager.LoadScene("Game");
        }
    }
}
