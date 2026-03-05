using UnityEngine;
using UnityEngine.UI;
namespace Tomciz.RoadGenerator
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] private Button _buttonEnableBezier;
        [SerializeField] private Button _buttonEnableLinear;
        [SerializeField] private RoadGenerator _roadGenerator;

        private void Awake()
        {
            _buttonEnableBezier.onClick.AddListener(OnButtonEnableBezierClick);
            _buttonEnableLinear.onClick.AddListener(OnButtonEnableLinearClick);
        }

        private void OnButtonEnableBezierClick()
        {
            if (_roadGenerator != null)
                _roadGenerator.SetUseBezier(true);
        }

        private void OnButtonEnableLinearClick()
        {
            if (_roadGenerator != null)
                _roadGenerator.SetUseBezier(false);
        }
    }
}
