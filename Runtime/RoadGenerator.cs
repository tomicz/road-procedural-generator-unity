using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    [RequireComponent(typeof(LineRenderer))]
    public class RoadGenerator : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;

        private InputController _inputController;
        private List<Vector3> _points;

        private void Awake() => _inputController = new InputController();
        private void OnEnable()
        {
            _inputController.OnStartDrawing += OnStartDrawing;
            _inputController.OnEndDrawing += OnEndDrawing;
        }
        private void OnDisable()
        {
            _inputController.OnStartDrawing -= OnStartDrawing;
            _inputController.OnEndDrawing -= OnEndDrawing;
        }

        private void OnStartDrawing()
        {
            _lineRenderer.enabled = true;
            if (_points == null || _points.Count == 0)
                _points = new List<Vector3> { _inputController.StartPosition };
        }
        private void OnEndDrawing()
        {
            _points ??= new List<Vector3>();
            _points.Add(_inputController.EndPosition);
            if (!_inputController.IsDrawing)
            {
                _lineRenderer.enabled = false;
                _points = null;
            }
        }
        private void Update()
        {
            _inputController.HandleMouseDrag();
            if (!_inputController.IsDrawing)
                return;
            SyncLinePositions();
        }

        private void SyncLinePositions()
        {
            var pts = _points ?? new List<Vector3>();
            var count = pts.Count + 1;
            _lineRenderer.positionCount = count;
            for (int i = 0; i < pts.Count; i++)
                _lineRenderer.SetPosition(i, pts[i]);
            _lineRenderer.SetPosition(pts.Count, _inputController.CurrentPosition);
        }
    }
}