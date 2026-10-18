using UnityEngine;

public class Camera : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform target;

    [Header("カメラ位置")]
    [SerializeField] private float distance = 7f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float lookHeight = 1f;

    [Header("追従の滑らかさ")]
    [SerializeField] private float positionSmoothTime = 0.2f;
    [SerializeField] private float rotationSmoothSpeed = 5f;

    private Vector3 positionVelocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        // プレイヤーの後方・上方にカメラの目標位置を設定
        Vector3 desiredPosition =
            target.position
            - target.forward * distance
            + Vector3.up * height;

        // カメラの位置を滑らかに追従させる
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        // プレイヤーの少し上を見る
        Vector3 lookTarget =
            target.position + Vector3.up * lookHeight;

        Vector3 direction = lookTarget - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(direction);

            // カメラの向きを滑らかに変える
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }
    }
}
