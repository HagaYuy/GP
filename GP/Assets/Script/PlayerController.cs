using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float brakeForce = 30f;

    [Header("旋回速度")]
    [SerializeField] private float turnSpeed = 100f;

    [Header("慣性制御")]
    [SerializeField] private float InertiaSuppression = 5f;

    private Rigidbody rb;

    private float moveInput;
    private float turnInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 入力を取得する
        moveInput = Input.GetAxis("Vertical");
        turnInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        Move();
        Turn();
        ApplyLateralFriction();
    }

    private void Move()
    {
        // 前進・後退
        if (moveInput > 0f)
        {
            rb.AddForce(
                transform.forward * moveInput * acceleration,
                ForceMode.Acceleration
            );
        }
        else if (moveInput < 0f)
        {
            // 走行中に後退入力をした場合はブレーキとして働く
            if (Vector3.Dot(rb.linearVelocity, transform.forward) > 0.5f)
            {
                rb.AddForce(
                    -transform.forward * brakeForce,
                    ForceMode.Acceleration
                );
            }
            else
            {
                rb.AddForce(
                    transform.forward * moveInput * acceleration,
                    ForceMode.Acceleration
                );
            }
        }

        // 最高速度を制限する（前後方向）
        Vector3 localVelocity = transform.InverseTransformDirection(
            rb.linearVelocity
        );

        localVelocity.z = Mathf.Clamp(
            localVelocity.z,
            -maxSpeed * 0.4f,
            maxSpeed
        );

        // 入力がない場合は慣性で減速する
        if (Mathf.Abs(moveInput) < 0.01f)
        {
            localVelocity.z = Mathf.MoveTowards(
                localVelocity.z,
                0f,
                InertiaSuppression * Time.fixedDeltaTime
            );
        }

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    private void Turn()
    {
        // 車体の前方向に対する速度を取得
        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        // 停止中は旋回しない
        if (Mathf.Abs(forwardSpeed) < 0.5f)
            return;

        // 速度に応じて旋回性能を調整
        float speedFactor = Mathf.Clamp01(
            Mathf.Abs(forwardSpeed) / 5f
        );

        // 後退中は旋回方向を反転
        float direction = Mathf.Sign(forwardSpeed);

        float turnAmount = turnInput
            * turnSpeed
            * speedFactor
            * direction
            * Time.fixedDeltaTime;

        rb.MoveRotation(
            rb.rotation * Quaternion.Euler(0f, turnAmount, 0f)
        );
    }

    private void ApplyLateralFriction()
    {
        // 車体から見た速度を取得
        Vector3 localVelocity = transform.InverseTransformDirection(
            rb.linearVelocity
        );

        // 横方向の速度を取得
        float sidewaysSpeed = localVelocity.x;

        // 横滑りを打ち消す力を加える
        rb.AddForce(
            -transform.right * sidewaysSpeed * InertiaSuppression,
            ForceMode.Acceleration
        );
    }
}
