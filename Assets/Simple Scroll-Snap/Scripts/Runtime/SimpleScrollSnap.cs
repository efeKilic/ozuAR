﻿// Simple Scroll-Snap - https://assetstore.unity.com/packages/tools/gui/simple-scroll-snap-140884
// Version: 1.1.6
// Author: Daniel Lochner

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DanielLochner.Assets.SimpleScrollSnap
{
    [AddComponentMenu("UI/Simple Scroll-Snap")]
    [RequireComponent(typeof(ScrollRect))]
    public class SimpleScrollSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        #region Fields
        public MovementType movementType = MovementType.Fixed;
        public MovementAxis movementAxis = MovementAxis.Horizontal;
        public bool automaticallyLayout = true;
        public SizeControl sizeControl = SizeControl.Fit;
        public Vector2 size = new Vector2(400, 250);
        public float automaticLayoutSpacing = 0.25f;
        public float leftMargin, rightMargin, topMargin, bottomMargin;
        public bool infinitelyScroll = false;
        public float infiniteScrollingEndSpacing = 0f;
        public int startingPanel = 0;
        public bool swipeGestures = true;
        public float minimumSwipeSpeed = 0f;
        public Button previousButton = null;
        public Button nextButton = null;
        public GameObject pagination = null;
        public bool toggleNavigation = true;
        public SnapTarget snapTarget = SnapTarget.Next;
        public float snappingSpeed = 10f;
        public float thresholdSnappingSpeed = -1f;
        public bool hardSnap = true;
        public UnityEvent onPanelChanged, onPanelSelecting, onPanelSelected, onPanelChanging;
        public List<TransitionEffect> transitionEffects = new List<TransitionEffect>();

        private bool dragging, selected = true, pressing;
        private float releaseSpeed;
        private Vector2 contentSize;
        private Direction releaseDirection;
        private Graphic[] graphics;
        private Canvas canvas;
        private RectTransform canvasRectTransform;
        private CanvasScaler canvasScaler;
        private ScrollRect scrollRect;
        #endregion

        #region Properties
        private RectTransform Content
        {
            get { return scrollRect.content; }
        }
        private RectTransform Viewport
        {
            get { return scrollRect.viewport; }
        }

        public int CurrentPanel { get; set; }
        public int TargetPanel { get; set; }
        public int NearestPanel { get; set; }

        public GameObject[] Panels { get; set; }
        public Toggle[] Toggles { get; set; }

        public int NumberOfPanels
        {
            get { return Content.childCount; }
        }
        #endregion

        #region Enumerators
        public enum MovementType
        {
            Fixed,
            Free
        }
        public enum MovementAxis
        {
            Horizontal,
            Vertical
        }
        public enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }
        public enum SnapTarget
        {
            Nearest,
            Previous,
            Next
        }
        public enum SizeControl
        {
            Manual,
            Fit
        }
        #endregion

        #region Methods
        private void Awake()
        {
            Initialize();

            if (Validate())
            {
                Setup();
            }
            else
            {
                throw new Exception("Invalid configuration.");
            }
        }
        private void Update()
        {
            if (NumberOfPanels == 0) return;

            OnSelectingAndSnapping();
            OnInfiniteScrolling();
            OnTransitionEffects();
            OnSwipeGestures();
        }
        #if UNITY_EDITOR
        private void OnValidate()
        {
            Initialize();
        }
        #endif

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (hardSnap)
            {
                scrollRect.inertia = true;
            }

            selected = false;
            dragging = true;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (dragging)
            {
                onPanelSelecting.Invoke();
            }
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;

            if (movementAxis == MovementAxis.Horizontal)
            {
                releaseDirection = Vector3.Dot(scrollRect.velocity, new Vector2(1f, 0f)) > 0 ? Direction.Right : Direction.Left;
            }
            else if (movementAxis == MovementAxis.Vertical)
            {
                releaseDirection = Vector3.Dot(scrollRect.velocity, new Vector2(0f, 1f)) > 0 ? Direction.Up : Direction.Down;
            }

            releaseSpeed = scrollRect.velocity.magnitude;
        }

        private void Initialize()
        {
            scrollRect = GetComponent<ScrollRect>();
            canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                canvasScaler = canvas.GetComponentInParent<CanvasScaler>();
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }
        }
        private bool Validate()
        {
            bool valid = true;

            if (pagination != null)
            {
                int numberOfToggles = pagination.transform.childCount;

                if (numberOfToggles != NumberOfPanels)
                {
                    Debug.LogError("<b>[SimpleScrollSnap]</b> The number of Toggles should be equivalent to the number of Panels. There are currently " + numberOfToggles + " Toggles and " + NumberOfPanels + " Panels. If you are adding Panels dynamically during runtime, please update your pagination to reflect the number of Panels you will have before adding.", gameObject);
                    valid = false;
                }
            }

            if (snappingSpeed < 0)
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> Snapping speed cannot be negative.", gameObject);
                valid = false;
            }

            return valid;
        }
        private void Setup()
        {
            if (NumberOfPanels == 0) return;

            // Canvas & Camera
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.planeDistance = (canvasRectTransform.rect.height / 2f) / Mathf.Tan((canvas.worldCamera.fieldOfView / 2f) * Mathf.Deg2Rad);
                if (canvas.worldCamera.farClipPlane < canvas.planeDistance)
                {
                    canvas.worldCamera.farClipPlane = Mathf.Ceil(canvas.planeDistance);
                }
            }

            // ScrollRect
            if (movementType == MovementType.Fixed)
            {
                scrollRect.horizontal = (movementAxis == MovementAxis.Horizontal);
                scrollRect.vertical = (movementAxis == MovementAxis.Vertical);
            }
            else
            {
                scrollRect.horizontal = scrollRect.vertical = true;
            }

            // Panels
            size = (sizeControl == SizeControl.Manual) ? size : new Vector2(GetComponent<RectTransform>().rect.width, GetComponent<RectTransform>().rect.height);

            Panels = new GameObject[NumberOfPanels];
            for (int i = 0; i < NumberOfPanels; i++)
            {
                Panels[i] = Content.GetChild(i).gameObject;
                RectTransform panelRectTransform = Panels[i].GetComponent<RectTransform>();

                if (movementType == MovementType.Fixed && automaticallyLayout)
                {
                    panelRectTransform.anchorMin = new Vector2(movementAxis == MovementAxis.Horizontal ? 0f : 0.5f, movementAxis == MovementAxis.Vertical ? 0f : 0.5f);
                    panelRectTransform.anchorMax = new Vector2(movementAxis == MovementAxis.Horizontal ? 0f : 0.5f, movementAxis == MovementAxis.Vertical ? 0f : 0.5f);

                    float x = (rightMargin + leftMargin) / 2f - leftMargin;
                    float y = (topMargin + bottomMargin) / 2f - bottomMargin;
                    Vector2 marginOffset = new Vector2(x / size.x, y / size.y);
                    panelRectTransform.pivot = new Vector2(0.5f, 0.5f) + marginOffset;
                    panelRectTransform.sizeDelta = size - new Vector2(leftMargin + rightMargin, topMargin + bottomMargin);

                    float panelPosX = (movementAxis == MovementAxis.Horizontal) ? i * (automaticLayoutSpacing + 1f) * size.x + (size.x / 2f) : 0f;
                    float panelPosY = (movementAxis == MovementAxis.Vertical) ? i * (automaticLayoutSpacing + 1f) * size.y + (size.y / 2f) : 0f;
                    panelRectTransform.anchoredPosition = new Vector2(panelPosX, panelPosY);
                }
            }

            // Content
            if (movementType == MovementType.Fixed)
            {
                // Automatic Layout
                if (automaticallyLayout)
                {
                    Content.anchorMin = new Vector2(movementAxis == MovementAxis.Horizontal ? 0f : 0.5f, movementAxis == MovementAxis.Vertical ? 0f : 0.5f);
                    Content.anchorMax = new Vector2(movementAxis == MovementAxis.Horizontal ? 0f : 0.5f, movementAxis == MovementAxis.Vertical ? 0f : 0.5f);
                    Content.pivot = new Vector2(movementAxis == MovementAxis.Horizontal ? 0f : 0.5f, movementAxis == MovementAxis.Vertical ? 0f : 0.5f);

                    Vector2 min = Panels[0].transform.position;
                    Vector2 max = Panels[NumberOfPanels - 1].transform.position;

                    float contentWidth = (movementAxis == MovementAxis.Horizontal) ? (NumberOfPanels * (automaticLayoutSpacing + 1f) * size.x) - (size.x * automaticLayoutSpacing) : size.x;
                    float contentHeight = (movementAxis == MovementAxis.Vertical) ? (NumberOfPanels * (automaticLayoutSpacing + 1f) * size.y) - (size.y * automaticLayoutSpacing) : size.y;
                    Content.sizeDelta = new Vector2(contentWidth, contentHeight);
                }

                // Infinite Scrolling
                if (infinitelyScroll)
                {
                    scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

                    contentSize = ((Vector2)Panels[NumberOfPanels - 1].transform.localPosition - (Vector2)Panels[0].transform.localPosition) + (Panels[NumberOfPanels - 1].GetComponent<RectTransform>().sizeDelta / 2f + Panels[0].GetComponent<RectTransform>().sizeDelta / 2f) + (new Vector2(movementAxis == MovementAxis.Horizontal ? infiniteScrollingEndSpacing * size.x : 0f, movementAxis == MovementAxis.Vertical ? infiniteScrollingEndSpacing * size.y : 0f));

                    if (movementAxis == MovementAxis.Horizontal)
                    {
                        contentSize += new Vector2(leftMargin + rightMargin, 0);
                    }
                    else
                    {
                        contentSize += new Vector2(0, topMargin + bottomMargin);
                    }

                    if (canvasScaler != null)
                    {
                        contentSize *= new Vector2(Screen.width / canvasScaler.referenceResolution.x, Screen.height / canvasScaler.referenceResolution.y);
                    }
                }
            }

            // Starting Panel
            float xOffset = (movementAxis == MovementAxis.Horizontal || movementType == MovementType.Free) ? Viewport.rect.width / 2f : 0f;
            float yOffset = (movementAxis == MovementAxis.Vertical || movementType == MovementType.Free) ? Viewport.rect.height / 2f : 0f;
            Vector2 offset = new Vector2(xOffset, yOffset);
            Content.anchoredPosition = -(Vector2)Panels[startingPanel].transform.localPosition + offset;
            CurrentPanel = TargetPanel = NearestPanel = startingPanel;

            // Previous Button
            if (previousButton != null)
            {
                previousButton.onClick.RemoveAllListeners();
                previousButton.onClick.AddListener(GoToPreviousPanel);
            }

            // Next Button
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(GoToNextPanel);
            }

            // Pagination
            if (pagination != null)
            {
                Toggles = pagination.GetComponentsInChildren<Toggle>();
                for (int i = 0; i < Toggles.Length; i++)
                {
                    if (Toggles[i] != null)
                    {
                        Toggles[i].isOn = (i == startingPanel);
                        Toggles[i].interactable = (i != TargetPanel);
                        int panelNum = i;

                        Toggles[i].onValueChanged.RemoveAllListeners();
                        Toggles[i].onValueChanged.AddListener(delegate
                        {
                            if (Toggles[panelNum].isOn && toggleNavigation)
                            {
                                GoToPanel(panelNum);
                            }
                        });
                    }
                }
            }
        }

        private Vector3 DisplacementFromCenter(Vector3 position)
        {
            return position - Viewport.position;
        }
        private int DetermineNearestPanel()
        {
            int panelNumber = NearestPanel;
            float[] distances = new float[NumberOfPanels];
            for (int i = 0; i < Panels.Length; i++)
            {
                distances[i] = DisplacementFromCenter(Panels[i].transform.position).magnitude;
            }
            float minDistance = Mathf.Min(distances);
            for (int i = 0; i < Panels.Length; i++)
            {
                if (minDistance == distances[i])
                {
                    panelNumber = i;
                    break;
                }
            }
            return panelNumber;
        }
        private void SelectTargetPanel()
        {
            NearestPanel = DetermineNearestPanel();

            float horizontalProjection = Vector3.Dot(DisplacementFromCenter(Panels[NearestPanel].transform.position), transform.right);
            float verticalProjection = Vector3.Dot(Panels[NearestPanel].transform.position, transform.up);

            if (snapTarget == SnapTarget.Nearest || releaseSpeed <= minimumSwipeSpeed)
            {
                GoToPanel(NearestPanel);
            }
            else if (snapTarget == SnapTarget.Previous)
            {
                if ((releaseDirection == Direction.Right && horizontalProjection < 0f) || (releaseDirection == Direction.Up && verticalProjection < 0f))
                {
                    GoToNextPanel();
                }
                else if ((releaseDirection == Direction.Left && horizontalProjection > 0f) || (releaseDirection == Direction.Down && verticalProjection > 0f))
                {
                    GoToPreviousPanel();
                }
                else
                {
                    GoToPanel(NearestPanel);
                }
            }
            else if (snapTarget == SnapTarget.Next)
            {
                if ((releaseDirection == Direction.Right && horizontalProjection > 0f) || (releaseDirection == Direction.Up && verticalProjection > 0f))
                {
                    GoToPreviousPanel();
                }
                else if ((releaseDirection == Direction.Left && horizontalProjection < 0f) || (releaseDirection == Direction.Down && verticalProjection < 0f))
                {
                    GoToNextPanel();
                }
                else
                {
                    GoToPanel(NearestPanel);
                }
            }
        }
        private void SnapToTargetPanel()
        {
            float xOffset = (movementAxis == MovementAxis.Horizontal || movementType == MovementType.Free) ? Viewport.rect.width / 2f : 0f;
            float yOffset = (movementAxis == MovementAxis.Vertical || movementType == MovementType.Free) ? Viewport.rect.height / 2f : 0f;
            Vector2 offset = new Vector2(xOffset, yOffset);

            Vector2 targetPosition = (-(Vector2)Panels[TargetPanel].transform.localPosition + offset);
            Content.anchoredPosition = Vector2.Lerp(Content.anchoredPosition, targetPosition, Time.unscaledDeltaTime * snappingSpeed);

            if (CurrentPanel != TargetPanel)
            {
                if (DisplacementFromCenter(Panels[TargetPanel].transform.position).magnitude < (Viewport.rect.width / 10f))
                {
                    CurrentPanel = TargetPanel;

                    onPanelChanged.Invoke();
                }
                else
                {
                    onPanelChanging.Invoke();
                }
            }
        }

        private void OnSelectingAndSnapping()
        {
            if (selected)
            {
                if (!((dragging || pressing) && swipeGestures))
                {
                    SnapToTargetPanel();
                }
            }
            else if (!dragging && (scrollRect.velocity.magnitude <= thresholdSnappingSpeed || thresholdSnappingSpeed == -1f))
            {
                SelectTargetPanel();
            }
        }
        private void OnInfiniteScrolling()
        {
            if (infinitelyScroll)
            {
                if (movementAxis == MovementAxis.Horizontal)
                {
                    for (int i = 0; i < NumberOfPanels; i++)
                    {
                        float horizontalProjection = Vector3.Dot(DisplacementFromCenter(Panels[i].transform.position), transform.right);

                        if (horizontalProjection > contentSize.x / 2f)
                        {
                            Panels[i].transform.position -= contentSize.x * transform.right;
                        }
                        else if (horizontalProjection < -1f * contentSize.x / 2f)
                        {
                            Panels[i].transform.position += contentSize.x * transform.right;
                        }
                    }
                }
                else if (movementAxis == MovementAxis.Vertical)
                {
                    for (int i = 0; i < NumberOfPanels; i++)
                    {
                        float verticalProjection = Vector3.Dot(DisplacementFromCenter(Panels[i].transform.position), transform.up);

                        if (verticalProjection > contentSize.y / 2f)
                        {
                            Panels[i].transform.position -= contentSize.y * transform.up;
                        }
                        else if (verticalProjection < -1f * contentSize.y / 2f)
                        {
                            Panels[i].transform.position += contentSize.y * transform.up;
                        }
                    }
                }
            }
        }
        private void OnTransitionEffects()
        {
            if (transitionEffects.Count == 0) return;

            foreach (GameObject panel in Panels)
            {
                foreach (TransitionEffect transitionEffect in transitionEffects)
                {
                    // Displacement
                    float displacement = 0f;
                    if (movementType == MovementType.Fixed)
                    {
                        if (movementAxis == MovementAxis.Horizontal)
                        {
                            displacement = Vector3.Dot(DisplacementFromCenter(panel.transform.position), transform.right);
                        }
                        else if (movementAxis == MovementAxis.Vertical)
                        {
                            displacement = Vector3.Dot(DisplacementFromCenter(panel.transform.position), transform.up);
                        }
                    }
                    else
                    {
                        displacement = DisplacementFromCenter(panel.transform.position).magnitude;
                    }

                    // Value
                    switch (transitionEffect.Label)
                    {
                        case "localPosition.z":
                            panel.transform.localPosition = new Vector3(panel.transform.localPosition.x, panel.transform.localPosition.y, transitionEffect.GetValue(displacement));
                            break;
                        case "localScale.x":
                            panel.transform.localScale = new Vector2(transitionEffect.GetValue(displacement), panel.transform.localScale.y);
                            break;
                        case "localScale.y":
                            panel.transform.localScale = new Vector2(panel.transform.localScale.x, transitionEffect.GetValue(displacement));
                            break;
                        case "localRotation.x":
                            panel.transform.localRotation = Quaternion.Euler(new Vector3(transitionEffect.GetValue(displacement), panel.transform.localEulerAngles.y, panel.transform.localEulerAngles.z));
                            break;
                        case "localRotation.y":
                            panel.transform.localRotation = Quaternion.Euler(new Vector3(panel.transform.localEulerAngles.x, transitionEffect.GetValue(displacement), panel.transform.localEulerAngles.z));
                            break;
                        case "localRotation.z":
                            panel.transform.localRotation = Quaternion.Euler(new Vector3(panel.transform.localEulerAngles.x, panel.transform.localEulerAngles.y, transitionEffect.GetValue(displacement)));
                            break;
                        case "color.r":
                            graphics = panel.GetComponentsInChildren<Graphic>();
                            foreach (Graphic graphic in graphics)
                            {
                                graphic.color = new Color(transitionEffect.GetValue(displacement), graphic.color.g, graphic.color.b, graphic.color.a);
                            }
                            break;
                        case "color.g":
                            graphics = panel.GetComponentsInChildren<Graphic>();
                            foreach (Graphic graphic in graphics)
                            {
                                graphic.color = new Color(graphic.color.r, transitionEffect.GetValue(displacement), graphic.color.b, graphic.color.a);
                            }
                            break;
                        case "color.b":
                            graphics = panel.GetComponentsInChildren<Graphic>();
                            foreach (Graphic graphic in graphics)
                            {
                                graphic.color = new Color(graphic.color.r, graphic.color.g, transitionEffect.GetValue(displacement), graphic.color.a);
                            }
                            break;
                        case "color.a":
                            graphics = panel.GetComponentsInChildren<Graphic>();
                            foreach (Graphic graphic in graphics)
                            {
                                graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, transitionEffect.GetValue(displacement));
                            }
                            break;
                    }
                }
            }
        }
        private void OnSwipeGestures()
        {
            if (swipeGestures)
            {
                scrollRect.horizontal = movementAxis == MovementAxis.Horizontal || movementType == MovementType.Free;
                scrollRect.vertical = movementAxis == MovementAxis.Vertical || movementType == MovementType.Free;
            }
            else
            {
                scrollRect.horizontal = scrollRect.vertical = !dragging;
            }
        }

        public void GoToPanel(int panelNumber)
        {
            TargetPanel = panelNumber;
            selected = true;
            onPanelSelected.Invoke();

            if (pagination != null)
            {
                for (int i = 0; i < Toggles.Length; i++)
                {
                    if (Toggles[i] != null)
                    {
                        Toggles[i].isOn = (i == TargetPanel);
                        Toggles[i].interactable = (i != TargetPanel);
                    }
                }
            }

            if (hardSnap)
            {
                scrollRect.inertia = false;
            }
        }
        public void GoToPreviousPanel()
        {
            NearestPanel = DetermineNearestPanel();
            if (NearestPanel != 0)
            {
                GoToPanel(NearestPanel - 1);
            }
            else
            {
                if (infinitelyScroll)
                {
                    GoToPanel(NumberOfPanels - 1);
                }
                else
                {
                    GoToPanel(NearestPanel);
                }
            }
        }
        public void GoToNextPanel()
        {
            NearestPanel = DetermineNearestPanel();
            if (NearestPanel != (NumberOfPanels - 1))
            {
                GoToPanel(NearestPanel + 1);
            }
            else
            {
                if (infinitelyScroll)
                {
                    GoToPanel(0);
                }
                else
                {
                    GoToPanel(NearestPanel);
                }
            }
        }

        public void AddToFront(GameObject panel)
        {
            Add(panel, 0);
        }
        public void AddToBack(GameObject panel)
        {
            Add(panel, NumberOfPanels);
        }
        public void Add(GameObject panel, int index)
        {
            if (NumberOfPanels != 0 && (index < 0 || index > NumberOfPanels))
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> Index must be an integer from 0 to " + NumberOfPanels + ".", gameObject);
                return;
            }
            else if (!automaticallyLayout)
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> \"Automatic Layout\" must be enabled for content to be dynamically added during runtime.");
                return;
            }

            panel = Instantiate(panel, Content, false);
            panel.transform.SetSiblingIndex(index);

            if (Validate())
            {
                if (TargetPanel <= index)
                {
                    startingPanel = TargetPanel;                 
                }
                else
                {
                    startingPanel = TargetPanel + 1;
                }
                Setup();
            }
        }

        public void RemoveFromFront()
        {
            Remove(0);
        }
        public void RemoveFromBack()
        {
            if (NumberOfPanels > 0)
            {
                Remove(NumberOfPanels - 1);
            }
            else
            {
                Remove(0);
            }
        }
        public void Remove(int index)
        {
            if (NumberOfPanels == 0)
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> There are no panels to remove.", gameObject);
                return;
            }
            else if (index < 0 || index > (NumberOfPanels - 1))
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> Index must be an integer from 0 to " + (NumberOfPanels - 1) + ".", gameObject);
                return;
            }
            else if (!automaticallyLayout)
            {
                Debug.LogError("<b>[SimpleScrollSnap]</b> \"Automatic Layout\" must be enabled for content to be dynamically removed during runtime.");
                return;
            }

            DestroyImmediate(Panels[index]);

            if (Validate())
            {
                if (TargetPanel == index)
                {
                    if (index == NumberOfPanels)
                    {
                        startingPanel = TargetPanel - 1;
                    }
                    else
                    {
                        startingPanel = TargetPanel;
                    }
                }
                else if (TargetPanel < index)
                {
                    startingPanel = TargetPanel;
                }
                else
                {
                    startingPanel = TargetPanel - 1;
                }
                Setup();
            }
        }

        public void AddVelocity(Vector2 velocity)
        {
            scrollRect.velocity += velocity;
            selected = false;
        }
        #endregion
    }
}