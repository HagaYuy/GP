using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public class ObstacleDodge : MonoBehaviour
{
    [Header("障害物検知")]
    [SerializeField] private float detectionDistance = 25f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("回避設定")]
    [SerializeField] private KeyCode dodgeKey = KeyCode.LeftShift;
    [SerializeField] private float dodgeAcceleration = 80f;
    [SerializeField] private float dodgeDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 2f;

    [Header("ブースト設定")]
    [SerializeField] private float boostAcceleration = 35f;
    [SerializeField] private float boostDuration = 0.6f;
    private float dodgeTimer;
    private float boostTimer;
    private float cooldownTimer;
    private float dodgeDirection;

    [Header("回避中の衝突無効化")]
    [SerializeField] private float collisionProtectionDuration = 0.3f;
    private float collisionProtectionTimer;
    private Collider[] playerColliders;
    private Collider[] obstacleColliders;
    private bool collisionProtectionActive;

    private Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // プレイヤー自身のColliderを取得
        playerColliders = GetComponentsInChildren<Collider>();

        // 障害物のColliderを取得
        obstacleColliders = FindObjectsByType<Collider>(
            FindObjectsSortMode.None
        );
    }

    private void Update()
    {
        // クールダウン中は回避できない
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // Shiftを押した瞬間に障害物を確認
        if (Input.GetKeyDown(dodgeKey) &&
            cooldownTimer <= 0f &&
            dodgeTimer <= 0f &&
            boostTimer <= 0f)
        {
            TryDodge();
        }
        UpdateCollisionProtection();
    }

    private void FixedUpdate()
    {
        // 回避中は横方向へ加速
        if (dodgeTimer > 0f)
        {
            rb.AddForce(
                transform.right * dodgeDirection * dodgeAcceleration,
                ForceMode.Acceleration
            );

            dodgeTimer -= Time.fixedDeltaTime;
        }

        // 回避成功時のブースト
        if (boostTimer > 0f)
        {
            rb.AddForce(
                transform.forward * boostAcceleration,
                ForceMode.Acceleration
            );

            boostTimer -= Time.fixedDeltaTime;
        }
    }

    private void TryDodge()
    {
        Collider obstacle = FindNearestObstacle();

        // 近くに前方の障害物がなければ何もしない
        if (obstacle == null)
            return;

        Vector3 localPosition = transform.InverseTransformPoint(
            obstacle.ClosestPoint(transform.position)
        );

        // 障害物が右なら左へ、左なら右へ回避
        if (localPosition.x > 0.5f)
        {
            dodgeDirection = -1f;
        }
        else if (localPosition.x < -0.5f)
        {
            dodgeDirection = 1f;
        }
        else
        {
            // 正面にある場合は、空いている側を選ぶ
            float leftSpace = GetSideClearance(-1f);
            float rightSpace = GetSideClearance(1f);

            dodgeDirection = leftSpace >= rightSpace
                ? -1f
                : 1f;
        }

        // 回避とブーストを開始
        dodgeTimer = dodgeDuration;
        boostTimer = boostDuration;
        cooldownTimer = dodgeCooldown;

        // 回避中の衝突保護を開始
        StartCollisionProtection();
    }

    private void StartCollisionProtection()
    {
        collisionProtectionTimer = collisionProtectionDuration;

        if (collisionProtectionActive)
            return;

        collisionProtectionActive = true;

        foreach (Collider playerCol in playerColliders)
        {
            if (playerCol == null)
                continue;

            foreach (Collider obstacleCol in obstacleColliders)
            {
                if (obstacleCol == null)
                    continue;

                // プレイヤー自身や同じRigidbodyのColliderは除外
                if (obstacleCol == playerCol ||
                    obstacleCol.attachedRigidbody == rb)
                    continue;

                // 障害物Layerだけを対象にする
                if ((obstacleLayers.value &
                    (1 << obstacleCol.gameObject.layer)) == 0)
                    continue;

                Physics.IgnoreCollision(
                    playerCol,
                    obstacleCol,
                    true
                );
            }
        }
    }

    private void UpdateCollisionProtection()
    {
        if (!collisionProtectionActive)
            return;

        collisionProtectionTimer -= Time.deltaTime;

        if (collisionProtectionTimer > 0f)
            return;

        foreach (Collider playerCol in playerColliders)
        {
            if (playerCol == null)
                continue;

            foreach (Collider obstacleCol in obstacleColliders)
            {
                if (obstacleCol == null)
                    continue;

                if (obstacleCol == playerCol ||
                    obstacleCol.attachedRigidbody == rb)
                    continue;

                if ((obstacleLayers.value &
                    (1 << obstacleCol.gameObject.layer)) == 0)
                    continue;

                Physics.IgnoreCollision(
                    playerCol,
                    obstacleCol,
                    false
                );
            }
        }

        collisionProtectionActive = false;
    }

    private Collider FindNearestObstacle()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            detectionDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        Collider nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            // 自分自身のColliderは無視
            if (col.attachedRigidbody == rb)
                continue;

            Vector3 closestPoint = col.ClosestPoint(transform.position);
            Vector3 toObstacle = closestPoint - transform.position;

            // 機体の後方にある障害物は無視
            if (Vector3.Dot(transform.forward, toObstacle) <= 0f)
                continue;

            float distance = toObstacle.sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = col;
            }
        }

        return nearest;
    }

    private float GetSideClearance(float direction)
    {
        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = transform.right * direction;

        // 横方向に障害物があるか調べる
        if (Physics.Raycast(
            rayOrigin,
            rayDirection,
            out RaycastHit hit,
            8f,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody == rb)
                return 8f;

            return hit.distance;
        }

        return 8f;
    }

    private void OnDrawGizmosSelected()
    {
        // Sceneビューに検知距離を表示
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}
