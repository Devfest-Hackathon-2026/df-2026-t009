using UnityEngine;
using UnityEngine.EventSystems;

namespace Onsite.Map
{
    /// <summary>
    /// 人形を中心に、斜め上から見下ろすカメラ。
    /// 1本指ドラッグで回転、2本指ピンチ／ホイールで寄り引き、2本指の上下で傾き。
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 260f;
        public float minDistance = 60f, maxDistance = 1600f;
        public float pitch = 52f, minPitch = 20f, maxPitch = 85f;
        public float yaw = 0f;
        public float smooth = 8f;

        /// <summary>UI の上での操作は地図に渡さない。</summary>
        public System.Func<Vector2, bool> IsOverUi;

        Vector3 _focus;
        Vector2 _lastMouse;
        bool _dragging;
        float _lastPinch;

        void LateUpdate()
        {
            HandleInput();
            if (target != null) _focus = Vector3.Lerp(_focus, target.position, 1 - Mathf.Exp(-smooth * Time.deltaTime));
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var want = _focus + rot * new Vector3(0, 0, -distance);
            transform.SetPositionAndRotation(want, rot);
        }

        public void SnapToTarget()
        {
            if (target != null) _focus = target.position;
        }

        void HandleInput()
        {
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began) _lastPinch = d;
                else
                {
                    distance = Mathf.Clamp(distance * (_lastPinch / Mathf.Max(d, 1)), minDistance, maxDistance);
                    _lastPinch = d;
                    float dy = (a.deltaPosition.y + b.deltaPosition.y) * 0.5f;
                    pitch = Mathf.Clamp(pitch - dy * 0.12f, minPitch, maxPitch);
                }
                _dragging = false;
                return;
            }

            bool down = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);
            Vector2 pos = Input.mousePosition;
            if (down)
            {
                _dragging = IsOverUi == null || !IsOverUi(pos);
                _lastMouse = pos;
            }
            if (held && _dragging)
            {
                var delta = pos - _lastMouse;
                yaw += delta.x * 0.25f;
                if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.LeftShift)) pitch = Mathf.Clamp(pitch - delta.y * 0.2f, minPitch, maxPitch);
                _lastMouse = pos;
            }
            if (!held) _dragging = false;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && (IsOverUi == null || !IsOverUi(pos)))
                distance = Mathf.Clamp(distance * Mathf.Pow(0.88f, wheel), minDistance, maxDistance);
        }
    }
}
