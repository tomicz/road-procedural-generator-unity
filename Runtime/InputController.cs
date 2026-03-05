using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace Tomciz.RoadGenerator
{
    public class InputController
    {
        public event Action OnStartDrawing;
        public event Action OnEndDrawing;
        public Vector3 StartPosition { get; private set; }
        public Vector3 CurrentPosition { get; private set; }
        public Vector3 EndPosition { get; private set; }
        public bool IsDrawing { get; private set; }

        private bool _waitForRelease;

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public void HandleMouseDrag()
        {
            if (IsDrawing)
            {
                if (_waitForRelease)
                {
                    if (Input.GetMouseButtonUp(0))
                        _waitForRelease = false;
                    return;
                }
                CurrentPosition = GetMouseWorldPosition();
                if (Input.GetMouseButtonDown(0))
                {
                    if (IsPointerOverUI()) return;
                    EndPosition = CurrentPosition;
                    OnEndDrawing?.Invoke();
                    StartPosition = EndPosition;
                    _waitForRelease = true;
                    OnStartDrawing?.Invoke();
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    if (IsPointerOverUI()) return;
                    EndPosition = CurrentPosition;
                    IsDrawing = false;
                    _waitForRelease = true;
                    OnEndDrawing?.Invoke();
                }
            }
            else if (_waitForRelease)
            {
                if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
                    _waitForRelease = false;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI()) return;
                StartPosition = CurrentPosition = GetMouseWorldPosition();
                IsDrawing = true;
                _waitForRelease = false;
                OnStartDrawing?.Invoke();
            }
        }

        private Vector3 GetMouseWorldPosition() =>
            Camera.main is not { } cam
                ? Vector3.zero
                : Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit)
                    ? hit.point
                    : Vector3.zero;
    }
}