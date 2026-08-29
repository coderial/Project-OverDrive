using UnityEngine;
using ProjectOverdrive.Controllers;

namespace ProjectOverdrive.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target & Map")]
        [Tooltip("추적할 플레이어. 비워두면 자동으로 찾습니다.")]
        public Transform target;
        
        [Tooltip("맵의 크기 (가로, 세로). X, Z 스케일이 30이라면 30x30입니다.")]
        public Vector2 mapSize = new Vector2(30f, 30f);

        [Header("Camera Settings")]
        [Tooltip("카메라가 부드럽게 따라가는 속도")]
        public float smoothSpeed = 10f;
        
        [Tooltip("카메라의 고정 높이 (Y축)")]
        public float fixedHeight = 5f;

        private Camera _cam;
        private float _mapHalfWidth;
        private float _mapHalfHeight;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            _mapHalfWidth = mapSize.x / 2f;
            _mapHalfHeight = mapSize.y / 2f;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    return;
                }
            }

            // 카메라 화면의 가로세로 절반 크기 계산
            float camHalfHeight = _cam.orthographicSize;
            float camHalfWidth = camHalfHeight * _cam.aspect;

            // 플레이어의 위치를 따라가되, 맵 바깥으로 나가지 않도록 Clamp
            float minX = -_mapHalfWidth + camHalfWidth;
            float maxX = _mapHalfWidth - camHalfWidth;
            
            float minZ = -_mapHalfHeight + camHalfHeight;
            float maxZ = _mapHalfHeight - camHalfHeight;

            // 만약 맵 크기가 카메라 화면보다 작으면, 중앙에 고정
            if (minX > maxX) minX = maxX = 0;
            if (minZ > maxZ) minZ = maxZ = 0;

            float targetX = Mathf.Clamp(target.position.x, minX, maxX);
            float targetZ = Mathf.Clamp(target.position.z, minZ, maxZ);

            Vector3 desiredPosition = new Vector3(targetX, fixedHeight, targetZ);
            
            // 부드러운 이동 (선택사항, 즉시 이동하려면 transform.position = desiredPosition)
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}